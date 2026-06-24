using UnityEngine;

public class TerrainPlaneFlatten : MonoBehaviour
{
    public Terrain terrain;

    [Header("Plane Definition")]
    public Transform startPoint;
    public Transform endPoint;

    [ContextMenu("Flatten Between Points")]
    public void Flatten()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        if (startPoint == null || endPoint == null)
        {
            Debug.LogError("Assign startPoint and endPoint");
            return;
        }

        TerrainData data = terrain.terrainData;

        int resolution = data.heightmapResolution;

        float[,] heights =
            new float[resolution, resolution];

        Vector3 terrainOrigin =
            terrain.transform.position;

        Vector3 start =
            startPoint.position;

        Vector3 end =
            endPoint.position;

        Vector3 direction =
            end - start;

        float length =
            direction.magnitude;

        direction.Normalize();

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float localX =
                    x / (float)(resolution - 1) *
                    data.size.x;

                float localZ =
                    z / (float)(resolution - 1) *
                    data.size.z;

                Vector3 worldPos =
                    terrainOrigin +
                    new Vector3(localX, 0, localZ);

                Vector3 fromStart =
                    worldPos - start;

                float distanceAlong =
                    Vector3.Dot(fromStart, direction);

                float t =
                    Mathf.Clamp01(distanceAlong / length);

                float height =
                    Mathf.Lerp(start.y, end.y, t);

                heights[z, x] =
                    (height - terrainOrigin.y)
                    / data.size.y;
            }
        }

        data.SetHeights(0, 0, heights);

        Debug.Log("Terrain flattened to plane between points.");
    }
}