using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class SledTerrainGenerator : MonoBehaviour
{
    public Terrain terrain;

    [Header("Base Height")]
    public float startHeight = 200f;
    public float endHeight = 150f;

    [Header("Features")]
    public int featureCount = 30;

    [Tooltip("Minimum feature radius in meters")]
    public float minFeatureSize = 100f;

    [Tooltip("Maximum feature radius in meters")]
    public float maxFeatureSize = 600f;

    [Header("Hills")]
    public float minHillHeight = 40f;
    public float maxHillHeight = 180f;

    [Header("Basins")]
    public float minValleyDepth = 40f;
    public float maxValleyDepth = 220f;

    [Header("Distribution")]
    [Range(0f, 1f)]
    public float hillChance = 0.5f;

    [Header("Random")]
    public int seed = 12345;

    class TerrainFeature
    {
        public float centerX;
        public float centerZ;

        public float radiusX;
        public float radiusZ;

        public float height;
    }

    [ContextMenu("Generate Terrain")]
    public void Generate()
    {
        if (terrain == null)
            terrain = GetComponent<Terrain>();

        TerrainData data = terrain.terrainData;

        int res = data.heightmapResolution;

        float[,] heights = new float[res, res];

        Random.InitState(seed);

        List<TerrainFeature> features =
            new List<TerrainFeature>();

        // Create hills and valleys
        for (int i = 0; i < featureCount; i++)
        {
            bool hill =
                Random.value < hillChance;

            TerrainFeature feature =
                new TerrainFeature();

            feature.centerX =
                Random.Range(0f, data.size.x);

            feature.centerZ =
                Random.Range(0f, data.size.z);

            feature.radiusX =
                Random.Range(
                    minFeatureSize,
                    maxFeatureSize);

            feature.radiusZ =
                Random.Range(
                    minFeatureSize,
                    maxFeatureSize);

            if (hill)
            {
                feature.height =
                    Random.Range(
                        minHillHeight,
                        maxHillHeight);
            }
            else
            {
                feature.height =
                    -Random.Range(
                        minValleyDepth,
                        maxValleyDepth);
            }

            features.Add(feature);
        }

        for (int z = 0; z < res; z++)
        {
            float z01 =
                z / (float)(res - 1);

            float worldZ =
                z01 * data.size.z;

            float baseHeight =
                Mathf.Lerp(
                    startHeight,
                    endHeight,
                    z01);

            for (int x = 0; x < res; x++)
            {
                float x01 =
                    x / (float)(res - 1);

                float worldX =
                    x01 * data.size.x;

                float offset = 0f;

                foreach (var feature in features)
                {
                    float dx =
                        (worldX - feature.centerX) /
                        feature.radiusX;

                    float dz =
                        (worldZ - feature.centerZ) /
                        feature.radiusZ;

                    float distance =
                        Mathf.Sqrt(
                            dx * dx +
                            dz * dz);

                    if (distance > 1f)
                        continue;

                    // Smooth cosine falloff
                    float influence =
                        0.5f +
                        0.5f *
                        Mathf.Cos(
                            distance *
                            Mathf.PI);

                    offset +=
                        feature.height *
                        influence;
                }

                float finalHeight =
                    baseHeight + offset;

                heights[z, x] =
                    Mathf.Clamp01(
                        finalHeight /
                        data.size.y);
            }
        }

        data.SetHeights(0, 0, heights);
    }
}