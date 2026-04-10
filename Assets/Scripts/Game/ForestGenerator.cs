using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    [Header("Core Settings")]
    [Tooltip("Drop your trees, bushes, and rocks here. It will randomly mix them!")]
    public GameObject[] treePrefabs;

    [Tooltip("How far left and right the forest extends (e.g., 20 means from -20 to 20)")]
    public float widthX = 20f;
    public float startZ = 0f;
    public float endZ = 100f;

    [Header("Tree Aesthetics")]
    public float minScale = 0.8f;
    public float maxScale = 1.3f;

    [Header("Feature 1: The S-Shape Clear Path")]
    [Tooltip("The total width of the empty path carved through the forest")]
    public float clearPathWidth = 8f;
    [Tooltip("How far left and right the path sweeps")]
    public float sAmplitude = 12f;
    [Tooltip("How tight the S-curves are")]
    public float sFrequency = 0.05f;

    [Header("Feature 2: Organic Background Noise")]
    public float organicGridSpacing = 2.5f;
    public float noiseScale = 0.1f;
    [Range(0.1f, 0.9f)] public float treeDensityThreshold = 0.45f;

    [Header("Feature 3: Dense Clumps (Groves)")]
    public int numberOfClumps = 15;
    public int minClumpSize = 4;
    public int maxClumpSize = 12;
    public float clumpRadius = 4.5f;

    [ContextMenu("Generate Mixed Forest")]
    public void GenerateForest()
    {
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogWarning("ForestGenerator: Please assign at least one tree prefab!");
            return;
        }

        ClearForest();

        // Random offsets so the organic forest looks completely different every click
        float randomOffsetX = Random.Range(-1000f, 1000f);
        float randomOffsetZ = Random.Range(-1000f, 1000f);

        // --- STEP 1: GENERATE ORGANIC BACKGROUND ---
        for (float z = startZ; z <= endZ; z += organicGridSpacing)
        {
            for (float x = -widthX; x <= widthX; x += organicGridSpacing)
            {
                // CRITICAL: If this point is inside our S-Path, skip it entirely!
                if (IsPointInPath(x, z)) continue;

                // Calculate Perlin Noise for natural clearings and thickets
                float pX = (x + randomOffsetX) * noiseScale;
                float pZ = (z + randomOffsetZ) * noiseScale;

                if (Mathf.PerlinNoise(pX, pZ) > treeDensityThreshold)
                {
                    // Add slight random jitter so it doesn't look like a perfect grid
                    float jitterX = Random.Range(-organicGridSpacing * 0.4f, organicGridSpacing * 0.4f);
                    float jitterZ = Random.Range(-organicGridSpacing * 0.4f, organicGridSpacing * 0.4f);

                    SpawnTree(new Vector3(x + jitterX, transform.position.y, z + jitterZ));
                }
            }
        }

        // --- STEP 2: GENERATE CLUMPS ---
        for (int i = 0; i < numberOfClumps; i++)
        {
            float centerX = Random.Range(-widthX, widthX);
            float centerZ = Random.Range(startZ, endZ);
            Vector3 clumpCenter = new Vector3(centerX, transform.position.y, centerZ);

            int treesInClump = Random.Range(minClumpSize, maxClumpSize + 1);

            for (int t = 0; t < treesInClump; t++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * clumpRadius;
                float tX = clumpCenter.x + randomCircle.x;
                float tZ = clumpCenter.z + randomCircle.y;

                // Ensure the clump doesn't spill out of bounds
                tX = Mathf.Clamp(tX, -widthX, widthX);
                tZ = Mathf.Clamp(tZ, startZ, endZ);

                // CRITICAL: If the clump tree falls inside the path, destroy the tree!
                if (IsPointInPath(tX, tZ)) continue;

                SpawnTree(new Vector3(tX, transform.position.y, tZ));
            }
        }
    }

    [ContextMenu("Clear Forest")]
    public void ClearForest()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    // --- THE PATH CARVING MATH ---

    private bool IsPointInPath(float x, float z)
    {
        // 1. Find where the exact center of the S-curve is at this specific Z depth
        float sCurveCenterX = Mathf.Sin(z * sFrequency) * sAmplitude;

        // 2. Check the horizontal distance from our tree to the center of the curve
        float distanceFromCenter = Mathf.Abs(x - sCurveCenterX);

        // 3. If the tree is within half the path width, it is INSIDE the path. (Return true to skip spawning)
        return distanceFromCenter < (clearPathWidth * 0.5f);
    }

    // --- UTILITY ---

    private void SpawnTree(Vector3 position)
    {
        GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
        GameObject tree = Instantiate(prefab, position, Quaternion.identity, transform);

        // Randomize the Y rotation (0-360) so clones look unique
        tree.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        // Randomize scale
        float randomScale = Random.Range(minScale, maxScale);
        tree.transform.localScale = new Vector3(randomScale, randomScale, randomScale);
    }
}