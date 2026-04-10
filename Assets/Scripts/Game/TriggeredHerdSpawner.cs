using UnityEngine;

public class TriggeredHerdSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject[] animalPrefabs;
    public int minSpawnCount = 10;
    public int maxSpawnCount = 20;
    [Tooltip("How far left and right they can spawn from the center")]
    public float spawnWidthX = 10f;

    [Header("Z-Axis Waypoints")]
    [Tooltip("The closest they will spawn during the 'Warm Spawn'")]
    public float warmSpawnStartZ = 50f;
    [Tooltip("The furthest they will spawn during the 'Warm Spawn'")]
    public float warmSpawnEndZ = 80f;
    [Tooltip("When the boulder crosses this Z, the animals start running")]
    public float triggerZ = 30f;
    [Tooltip("When the animals cross this Z, they disappear")]
    public float runEndZ = 200f;

    [Header("Movement Dynamics")]
    public float minSpeed = 15f;
    public float maxSpeed = 22f;
    [Tooltip("How wide they wander left and right")]
    public float meanderWidth = 2.5f;
    [Tooltip("How fast they weave left and right")]
    public float meanderSpeed = 2f;

    // High-performance tools to talk to the shader
    private MaterialPropertyBlock _propBlock;
    private static readonly int RandomOffsetID = Shader.PropertyToID("_RandomOffset");

    // A lightweight container. Much cheaper for mobile than a MonoBehaviour!
    private class AnimalData
    {
        public Transform transform;
        public float speed;
        public float baseX;        // Their starting lane
        public float timeOffset;   // So they don't all weave at the exact same time
        public float meanderFreq;
        public bool isFinished;
    }

    private AnimalData[] _herd;
    private bool _hasTriggered = false;
    private int _finishedCount = 0;

    private void Start()
    {
        if (animalPrefabs == null || animalPrefabs.Length == 0) return;

        // Initialize the block once
        _propBlock = new MaterialPropertyBlock();

        InitializeWarmSpawn();
    }

    private void InitializeWarmSpawn()
    {
        int spawnCount = Random.Range(minSpawnCount, maxSpawnCount + 1);
        _herd = new AnimalData[spawnCount];

        for (int i = 0; i < spawnCount; i++)
        {
            // 1. Pick a random animal prefab
            GameObject prefab = animalPrefabs[Random.Range(0, animalPrefabs.Length)];

            // 2. Pick random coordinates within the Warm Spawn box
            float randomX = Random.Range(-spawnWidthX, spawnWidthX);
            float randomZ = Random.Range(warmSpawnStartZ, warmSpawnEndZ);
            Vector3 spawnPos = new Vector3(randomX, transform.position.y, randomZ);

            // 3. Instantiate the animal
            GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

            // --- THE BULLETPROOF SHADER FIX ---
            // Get all renderers on this specific chicken (in case it has multiple parts)
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

            // Generate ONE random offset for this whole chicken
            float uniqueOffset = Random.Range(0f, 100f);

            foreach (Renderer r in renderers)
            {
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(RandomOffsetID, uniqueOffset);
                r.SetPropertyBlock(_propBlock);
            }
            // ----------------------------------

            // 4. Force them to look directly at the Origin (0,0,0)
            Vector3 lookDir = Vector3.zero - spawnPos;
            lookDir.y = 0; // Keep it perfectly flat on the ground
            if (lookDir != Vector3.zero)
            {
                go.transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // 5. Store the mathematical data in our hyper-fast array
            _herd[i] = new AnimalData
            {
                transform = go.transform,
                speed = Random.Range(minSpeed, maxSpeed),
                baseX = randomX,
                timeOffset = Random.Range(0f, 100f),
                meanderFreq = Random.Range(meanderSpeed * 0.8f, meanderSpeed * 1.2f),
                isFinished = false
            };
        }
    }

    private void Update()
    {
        // --- PHASE 1: WAITING FOR THE BOULDER ---
        if (!_hasTriggered)
        {
            // You mentioned you set up an ArcadeBoulder Instance! We check it here.
            if (ArcadeBoulder.Instance != null && ArcadeBoulder.Instance.transform.position.z >= triggerZ)
            {
                _hasTriggered = true;
            }
            return; // Don't do any math until triggered
        }

        // --- PHASE 2: THE RUN ---

        // Stop calculating if everyone has finished the race
        if (_finishedCount >= _herd.Length) return;

        // Cache time variables outside the loop for extreme performance
        float time = Time.time;
        float dt = Time.deltaTime;

        for (int i = 0; i < _herd.Length; i++)
        {
            AnimalData animal = _herd[i];
            if (animal.isFinished) continue;

            Vector3 currentPos = animal.transform.position;

            // Calculate forward movement (+Z)
            float nextZ = currentPos.z + (animal.speed * dt);

            // Check if they reached the end
            if (nextZ >= runEndZ)
            {
                animal.isFinished = true;
                animal.transform.gameObject.SetActive(false); // Hide them to save rendering
                _finishedCount++;
                continue;
            }

            // Calculate the meander (Wandering X) using a smooth Sine wave
            float nextX = animal.baseX + Mathf.Sin(time * animal.meanderFreq + animal.timeOffset) * meanderWidth;

            Vector3 nextPos = new Vector3(nextX, currentPos.y, nextZ);

            // Smoothly rotate to face the exact meandering path they are walking
            Vector3 moveDirection = nextPos - currentPos;
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                animal.transform.rotation = Quaternion.LookRotation(moveDirection);
            }

            // Apply the final position
            animal.transform.position = nextPos;
        }
    }
}