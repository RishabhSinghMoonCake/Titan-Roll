using UnityEngine;

public class HerdSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drop all your different sheep/animal prefabs here!")]
    public GameObject[] animalPrefabs;

    [Header("Position & Setup")]
    public Transform startPoint;
    public Transform endPoint;

    [Tooltip("The closest they can spawn to the camera (Z axis)")]
    public float minZ = 10f;
    [Tooltip("The furthest they can spawn from the camera (Z axis)")]
    public float maxZ = 50f;

    [Header("Herd Settings")]
    public float minSpeed = 6f;
    public float maxSpeed = 10f;

    [Tooltip("Target time between spawns")]
    public float baseSpawnInterval = 0.5f;

    [Tooltip("How many animals exist in memory at once")]
    public int poolCapacity = 20;

    // The lightweight data container (No MonoBehaviour overhead!)
    private class AnimalData
    {
        public Transform transform;
        public float speed;
        public bool isActive;
    }

    private AnimalData[] _herd;
    private float _spawnTimer = 0f;
    private float _currentSpawnInterval = 0f;
    private bool _movingRight;

    private void Start()
    {
        if (animalPrefabs == null || animalPrefabs.Length == 0) return;
        if (startPoint == null || endPoint == null) return;

        // Determine direction once so we don't have to calculate it every frame
        _movingRight = startPoint.position.x < endPoint.position.x;
        _currentSpawnInterval = baseSpawnInterval;

        InitializePool();
    }

    private void InitializePool()
    {
        _herd = new AnimalData[poolCapacity];

        for (int i = 0; i < poolCapacity; i++)
        {
            // Pick a random prefab for the mixed bag effect
            GameObject prefab = animalPrefabs[Random.Range(0, animalPrefabs.Length)];

            // Instantiate it hidden
            GameObject go = Instantiate(prefab, transform);
            go.SetActive(false);

            _herd[i] = new AnimalData
            {
                transform = go.transform,
                speed = 0f,
                isActive = false
            };
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // --- 1. SPAWN LOGIC ---
        _spawnTimer += dt;
        if (_spawnTimer >= _currentSpawnInterval)
        {
            _spawnTimer = 0f;
            // Add a tiny bit of randomness so they don't spawn like perfect robots
            _currentSpawnInterval = baseSpawnInterval + Random.Range(-0.1f, 0.1f);

            ActivateNextAvailableAnimal();
        }

        // --- 2. MOVEMENT LOGIC ---
        float endX = endPoint.position.x;

        for (int i = 0; i < _herd.Length; i++)
        {
            AnimalData animal = _herd[i];
            if (!animal.isActive) continue;

            // Move them forward along their local Z axis (since we rotated them to face the target)
            animal.transform.position += animal.transform.forward * animal.speed * dt;

            // Check if they crossed the finish line
            bool finished = _movingRight
                ? animal.transform.position.x >= endX
                : animal.transform.position.x <= endX;

            if (finished)
            {
                animal.isActive = false;
                animal.transform.gameObject.SetActive(false);
            }
        }
    }

    private void ActivateNextAvailableAnimal()
    {
        // Find the first sleeping animal in our array
        for (int i = 0; i < _herd.Length; i++)
        {
            AnimalData animal = _herd[i];
            if (!animal.isActive)
            {
                // Wake it up
                animal.isActive = true;
                animal.speed = Random.Range(minSpeed, maxSpeed); // Give them organic, slightly different speeds

                // 1. Calculate Start & Target X
                float startX = startPoint.position.x;
                float targetX = endPoint.position.x;

                // 2. Pick a random Z lane
                float randomZ = Random.Range(minZ, maxZ);

                // 3. Position the animal at the start line
                animal.transform.position = new Vector3(startX, startPoint.position.y, randomZ);

                // 4. Aim them precisely at the finish line on their lane
                Vector3 targetPos = new Vector3(targetX, endPoint.position.y, randomZ);
                animal.transform.LookAt(targetPos);

                // 5. Turn on the visuals
                animal.transform.gameObject.SetActive(true);

                // Break out of the loop since we found and launched one
                return;
            }
        }
    }
}