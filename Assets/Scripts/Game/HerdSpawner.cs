using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class HerdSpawner : MonoBehaviour
{
    public enum SpawnDirection { LeftToRight, RightToLeft }

    [Header("References")]
    [Tooltip("Drop all your different sheep/animal prefabs here!")]
    public HerdAnimal[] sheepPrefabs;

    [Header("Position & Setup")]
    public SpawnDirection direction = SpawnDirection.LeftToRight;

    [Tooltip("How far from the center (0) should they spawn and despawn? (e.g., 15 means they spawn at -15 and run to +15)")]
    public float spawnDistanceX = 15f;

    [Tooltip("The closest they can spawn to the camera (Z axis)")]
    public float minZ = 10f;
    [Tooltip("The furthest they can spawn from the camera (Z axis)")]
    public float maxZ = 50f;

    [Header("Herd Settings")]
    public float sheepSpeed = 8f;
    [Tooltip("How fast new sheep spawn")]
    public float spawnInterval = 0.3f;
    [Tooltip("How many to keep in memory at once")]
    public int poolCapacity = 20;

    private IObjectPool<HerdAnimal> _sheepPool;

    private void Start()
    {
        if (sheepPrefabs == null || sheepPrefabs.Length == 0)
        {
            Debug.LogError("HerdSpawner: You forgot to add sheep prefabs!");
            return;
        }

        _sheepPool = new ObjectPool<HerdAnimal>(
            createFunc: CreateNewSheep,
            actionOnGet: OnPullSheepFromPool,
            actionOnRelease: OnReturnSheepToPool,
            actionOnDestroy: OnDestroySheep,
            defaultCapacity: poolCapacity,
            maxSize: 50
        );

        StartCoroutine(SpawnHerdRoutine());
    }

    // --- POOL ACTIONS ---

    private HerdAnimal CreateNewSheep()
    {
        // THE MIXED BAG TRICK: Pick a random prefab from the array to spawn!
        int randomIndex = Random.Range(0, sheepPrefabs.Length);
        HerdAnimal sheep = Instantiate(sheepPrefabs[randomIndex], transform);
        return sheep;
    }

    private void OnPullSheepFromPool(HerdAnimal sheep)
    {
        sheep.gameObject.SetActive(true);

        // 1. Calculate Start and Target X based on our Dropdown choice
        float startX = (direction == SpawnDirection.LeftToRight) ? -spawnDistanceX : spawnDistanceX;
        float targetX = (direction == SpawnDirection.LeftToRight) ? spawnDistanceX : -spawnDistanceX;

        // 2. Pick a random lane (Z)
        float randomZ = Random.Range(minZ, maxZ);
        sheep.transform.position = new Vector3(startX, transform.position.y, randomZ);

        // 3. Aim the sheep exactly at the target on the other side
        Vector3 targetPos = new Vector3(targetX, transform.position.y, randomZ);
        sheep.transform.LookAt(targetPos);

        // 4. Kick off the movement
        sheep.Initialize(_sheepPool, sheepSpeed, targetX);
    }

    private void OnReturnSheepToPool(HerdAnimal sheep)
    {
        sheep.gameObject.SetActive(false);
    }

    private void OnDestroySheep(HerdAnimal sheep)
    {
        Destroy(sheep.gameObject);
    }

    // --- SPAWN LOOP ---

    private IEnumerator SpawnHerdRoutine()
    {
        while (true)
        {
            _sheepPool.Get();

            float randomInterval = spawnInterval + Random.Range(-0.1f, 0.1f);
            yield return new WaitForSeconds(randomInterval);
        }
    }
}