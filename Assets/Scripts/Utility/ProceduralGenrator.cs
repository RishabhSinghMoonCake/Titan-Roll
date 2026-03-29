using UnityEngine;

public class ProceduralGenerator : MonoBehaviour
{
    [Header("--- S-Pattern Settings ---")]
    public SPatternSettings sPatternSettings;

    [Header("--- Farm Grid Settings ---")]
    public FarmGridSettings farmGridSettings;

    [System.Serializable]
    public class SPatternSettings
    {
        [Tooltip("The prefab to use for the snake body.")]
        public GameObject bodyPrefab;
        [Tooltip("Total distance the pattern will travel forward (Z-axis).")]
        public float totalDistance = 50f;
        [Tooltip("How wide the S-curve swings left and right (X-axis).")]
        public float curveWidth = 5f;
        [Tooltip("How tightly the S-curve loops.")]
        public float curveFrequency = 0.2f;
        [Tooltip("Distance between each spawned prefab.")]
        public float spacing = 1f;
        [Tooltip("Custom rotation")]
        public Vector3 prefabRot = Vector3.zero;
    }

    [System.Serializable]
    public class FarmGridSettings
    {
        [Tooltip("The size of the crop grid (Z x Z).")]
        public int gridSize = 5;
        [Tooltip("Distance between each crop/item.")]
        public float cellSpacing = 2f;

        [Space(10)]
        [Tooltip("Place exactly Z number of prefabs here. Each row will use one of these.")]
        public GameObject[] rowPrefabs;
    }

    // ==========================================
    // S-PATTERN GENERATION
    // ==========================================

    [ContextMenu("Generate S-Pattern")]
    public void GenerateSPattern()
    {
        if (sPatternSettings.bodyPrefab == null)
        {
            Debug.LogWarning("S-Pattern: No Body Prefab assigned!");
            return;
        }

        Transform container = ClearAndCreateContainer("SPattern_Container");

        // Center the starting point locally
        Vector3 localStartPos = Vector3.zero;

        for (float z = 0; z <= sPatternSettings.totalDistance; z += sPatternSettings.spacing)
        {
            float x = Mathf.Sin(z * sPatternSettings.curveFrequency) * sPatternSettings.curveWidth;

            Vector3 worldPos = transform.TransformPoint(localStartPos + new Vector3(x, 0, z));

            SpawnPrefab(sPatternSettings.bodyPrefab, worldPos, Quaternion.Euler(sPatternSettings.prefabRot), container);
        }

    }

    // ==========================================
    // FARM GRID GENERATION
    // ==========================================

    [ContextMenu("Generate Farm Grid")]
    public void GenerateFarmGrid()
    {
        Transform container = ClearAndCreateContainer("FarmGrid_Container");

        // Pre-calculate crop area boundaries so the grid generates perfectly centered on this GameObject
        float gridWidth = (farmGridSettings.gridSize - 1) * farmGridSettings.cellSpacing;
        Vector3 startLocalCropPos = new Vector3(-gridWidth / 2f, 0, -gridWidth / 2f);

        GenerateCrops(container, startLocalCropPos);

        Debug.Log("Farm Grid Generated successfully with packed Prefabs!");
    }

    private void GenerateCrops(Transform parentContainer, Vector3 startLocal)
    {
        int zSize = farmGridSettings.gridSize;

        for (int z = 0; z < zSize; z++)
        {
            // Select the prefab for the current row
            GameObject rowPrefab = null;
            if (farmGridSettings.rowPrefabs != null && farmGridSettings.rowPrefabs.Length > 0)
            {
                rowPrefab = farmGridSettings.rowPrefabs[z % farmGridSettings.rowPrefabs.Length];
            }

            if (rowPrefab == null) continue;

            for (int x = 0; x < zSize; x++)
            {
                // Calculate position relative to the starting corner
                Vector3 currentLocalPos = startLocal + new Vector3(x * farmGridSettings.cellSpacing, 0, z * farmGridSettings.cellSpacing);
                Vector3 worldPos = transform.TransformPoint(currentLocalPos);

                SpawnPrefab(rowPrefab, worldPos, transform.rotation, parentContainer);
            }
        }
    }

    // ==========================================
    // UTILITY & PREFAB HANDLING
    // ==========================================

    private Transform ClearAndCreateContainer(string containerName)
    {
        Transform oldContainer = transform.Find(containerName);
        if (oldContainer != null)
        {
            DestroyImmediate(oldContainer.gameObject);
        }

        Transform container = new GameObject(containerName).transform;
        container.SetParent(this.transform);
        container.localPosition = Vector3.zero;
        container.localRotation = Quaternion.identity;
        container.localScale = Vector3.one;
        return container;
    }

    /// <summary>
    /// Safely instantiates objects while preserving prefab connections in the Editor.
    /// Supports Undo.
    /// </summary>
    private GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
#if UNITY_EDITOR
        // If we are in the Editor and NOT in Play Mode, use PrefabUtility to preserve packed state
        if (!Application.isPlaying)
        {
            GameObject instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            instance.transform.rotation = rotation;

            // Register this action so you can use Ctrl+Z to undo the entire generation!
            UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Procedural Generation");
            return instance;
        }
#endif
        // Fallback for Play Mode / Builds
        return Instantiate(prefab, position, rotation, parent);
    }
}