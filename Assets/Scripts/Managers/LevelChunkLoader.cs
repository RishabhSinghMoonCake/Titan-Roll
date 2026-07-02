using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LevelChunkLoader : MonoBehaviour
{
    [Header("Prefabs to Spawn")]
    [Tooltip("Drag the prefabs you want to load here. They will spawn at their originally saved coordinates.")]
    public GameObject[] prefabsToSpawn;

    private bool _hasSpawned = false;

    private void Awake()
    {
        // Safety check to ensure the collider is set as a trigger
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Prevent it from spawning multiple times if the boulder hits it twice
        if (_hasSpawned) return;

        if (other.CompareTag("Boulder"))
        {
            _hasSpawned = true;

            // Loop through the array and spawn each prefab
            foreach (GameObject prefab in prefabsToSpawn)
            {
                if (prefab != null)
                {
                    // Calling Instantiate with just the prefab uses its saved position and rotation!
                    Instantiate(prefab);
                }
            }

            Debug.Log($"<color=green>[PREFAB LOADER]</color> Crossed trigger! Spawned {prefabsToSpawn.Length} prefabs at their default locations.");
        }
    }
}