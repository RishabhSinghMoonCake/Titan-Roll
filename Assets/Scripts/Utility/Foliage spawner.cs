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

    [Header("Organic Placement (Perlin Noise)")]
    [Tooltip("How many times the script will TRY to place a tree. (Increase this if the forest looks too thin).")]
    public int spawnAttempts = 1500;

    [Tooltip("Controls the size of the forest patches. Lower value = massive sprawling forests. Higher value = lots of tiny patches.")]
    public float noiseScale = 0.05f;

    [Tooltip("How strictly the trees are grouped. Higher value = tighter, denser clusters with more empty space between them.")]
    [Range(0f, 1f)]
    public float densityThreshold = 0.5f;

    [Header("Terrain Texture Detection")]
    [Tooltip("The Unity Terrain object to read textures from.")]
    public Terrain targetTerrain;
    [Tooltip("The index of the terrain texture layer you want to spawn on (0 is usually the first texture, 1 is the second, etc.).")]
    public int allowedTextureIndex = 0;
    [Tooltip("How strongly painted the texture needs to be (0.5 means at least 50% opacity).")]
    [Range(0.1f, 1f)]
    public float minTextureWeight = 0.5f;

    [ContextMenu("Generate Foliage")]
    public void GenerateFoliage()
    {
        if (treePrefab == null)
        {
            Debug.LogError("Foliage Spawner: Please assign a tree prefab!");
            return;
        }

        if (parentObject == null)
        {
            parentObject = this.transform;
        }

        int successfullySpawned = 0;

        // Generate random offsets so the forest layout is different every single time you click Generate
        float noiseOffsetX = Random.Range(-10000f, 10000f);
        float noiseOffsetZ = Random.Range(-10000f, 10000f);

        for (int i = 0; i < spawnAttempts; i++)
        {
            // Pick a random spot anywhere in the entire bounds
            float randomX = Random.Range(-areaWidth / 2f, areaWidth / 2f);
            float randomZ = Random.Range(-areaLength / 2f, areaLength / 2f);

            // --- 1. PERLIN NOISE CHECK ---
            // We read the "cloud map" at this exact coordinate
            float pX = (randomX + noiseOffsetX) * noiseScale;
            float pZ = (randomZ + noiseOffsetZ) * noiseScale;
            float noiseValue = Mathf.PerlinNoise(pX, pZ);

            // If the noise value is below your threshold, this area is a "clearing". Skip it!
            if (noiseValue < densityThreshold)
            {
                continue;
            }

            Vector3 rayStartPos = new Vector3(
                transform.position.x + randomX,
                transform.position.y + raycastHeight,
                transform.position.z + randomZ
            );

            if (Physics.Raycast(rayStartPos, Vector3.down, out RaycastHit hit, Mathf.Infinity, terrainLayer))
            {
                // --- 2. TEXTURE CHECK ---
                if (targetTerrain != null)
                {
                    float textureWeight = GetTextureWeightAt(hit.point);
                    if (textureWeight < minTextureWeight)
                    {
                        continue;
                    }
                }

                // 3. SPAWN
                GameObject newTree = Instantiate(treePrefab, hit.point, Quaternion.identity);
                newTree.transform.SetParent(parentObject);
                newTree.transform.up = hit.normal;

                successfullySpawned++;
            }
        }

        Debug.Log($"Successfully spawned {successfullySpawned} trees out of {spawnAttempts} attempts.");
    }

    private float GetTextureWeightAt(Vector3 worldPos)
    {
        if (targetTerrain == null || targetTerrain.terrainData == null) return 0f;

        TerrainData tData = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.transform.position;

        float mapX = (worldPos.x - terrainPos.x) / tData.size.x;
        float mapZ = (worldPos.z - terrainPos.z) / tData.size.z;

        int coordX = Mathf.FloorToInt(mapX * tData.alphamapWidth);
        int coordZ = Mathf.FloorToInt(mapZ * tData.alphamapHeight);

        if (coordX < 0 || coordZ < 0 || coordX >= tData.alphamapWidth || coordZ >= tData.alphamapHeight)
            return 0f;

        float[,,] splatmapData = tData.GetAlphamaps(coordX, coordZ, 1, 1);

        if (allowedTextureIndex < splatmapData.GetLength(2))
        {
            return splatmapData[0, 0, allowedTextureIndex];
        }

        return 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
        Vector3 center = new Vector3(transform.position.x, transform.position.y + (raycastHeight / 2), transform.position.z);
        Vector3 size = new Vector3(areaWidth, raycastHeight, areaLength);
        Gizmos.DrawWireCube(center, size);
    }
}