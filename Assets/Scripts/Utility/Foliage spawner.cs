using UnityEngine;

public class FoliageSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The tree prefab you want to spawn.")]
    public GameObject treePrefab;
    [Tooltip("The parent object to keep the hierarchy clean. Leaves as 'this' if empty.")]
    public Transform parentObject;
    [Tooltip("Set this to the layer your terrain is on (e.g., 'Terrain').")]
    public LayerMask terrainLayer;

    [Header("Spawn Area Bounds")]
    public float areaWidth = 100f;
    public float areaLength = 100f;
    [Tooltip("How high up the raycast should start looking for terrain.")]
    public float raycastHeight = 50f;

    [Header("Foliage Parameters")]
    public int numberOfTrees = 200;
    [Range(0f, 1f)]
    [Tooltip("Chance to spawn a tree per iteration (1 = 100% chance, 0.5 = 50% chance).")]
    public float randomnessChance = 1f;

    [ContextMenu("Generate Foliage")]
    public void GenerateFoliage()
    {
        if (treePrefab == null)
        {
            Debug.LogError("Foliage Spawner: Please assign a tree prefab!");
            return;
        }

        // Default to the object holding the script if no parent is assigned
        if (parentObject == null)
        {
            parentObject = this.transform;
        }

        int successfullySpawned = 0;

        for (int i = 0; i < numberOfTrees; i++)
        {
            // 1. Check randomness chance
            if (Random.value > randomnessChance)
            {
                continue; // Skip this tree
            }

            // 2. Pick a random X and Z coordinate within the bounds
            float randomX = Random.Range(-areaWidth / 2f, areaWidth / 2f);
            float randomZ = Random.Range(-areaLength / 2f, areaLength / 2f);

            // 3. Set the starting point for the raycast (high up in the air)
            Vector3 rayStartPos = new Vector3(
                transform.position.x + randomX,
                transform.position.y + raycastHeight,
                transform.position.z + randomZ
            );

            // 4. Raycast downwards to check for terrain
            if (Physics.Raycast(rayStartPos, Vector3.down, out RaycastHit hit, Mathf.Infinity, terrainLayer))
            {
                // 5. Instantiate the prefab at the hit point
                GameObject newTree = Instantiate(treePrefab, hit.point, Quaternion.identity);

                // 6. Parent the tree
                newTree.transform.SetParent(parentObject);

                // 7. Align the tree to the terrain normal, keeping the prefab's internal structure intact
                newTree.transform.up = hit.normal;

                successfullySpawned++;
            }
        }

        Debug.Log($"Successfully spawned {successfullySpawned} trees.");
    }

    // Draws a green box in the Scene view to help you visualize the spawn area
    private void OnDrawGizmosSelected() 
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        Vector3 center = new Vector3(transform.position.x, transform.position.y + (raycastHeight / 2), transform.position.z);
        Vector3 size = new Vector3(areaWidth, raycastHeight, areaLength);
        Gizmos.DrawWireCube(center, size);
    }
}