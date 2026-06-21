using UnityEngine;

public class TerrainSmoother : MonoBehaviour
{
    public Terrain terrain;

    [Range(1, 10)]
    public int iterations = 3;

    [Range(1, 8)]
    public int radius = 2;

    [ContextMenu("Smooth Terrain")]
    public void SmoothTerrain()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        TerrainData data = terrain.terrainData;

        int width = data.heightmapResolution;
        int height = data.heightmapResolution;

        float[,] heights = data.GetHeights(0, 0, width, height);

        for (int iter = 0; iter < iterations; iter++)
        {
            float[,] newHeights = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    int count = 0;

                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            int nx = Mathf.Clamp(x + ox, 0, width - 1);
                            int ny = Mathf.Clamp(y + oy, 0, height - 1);

                            sum += heights[nx, ny];
                            count++;
                        }
                    }

                    newHeights[x, y] = sum / count;
                }
            }

            heights = newHeights;
        }

        data.SetHeights(0, 0, heights);

        Debug.Log("Terrain smoothed.");
    }
}