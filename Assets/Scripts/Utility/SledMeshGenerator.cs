using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SledMeshGenerator : MonoBehaviour
{
    [Header("Mesh Size")]
    public int width = 1000;
    public int length = 1000;

    [Header("Resolution")]
    public int xSegments = 200;
    public int zSegments = 200;

    [Header("Overall Slope")]
    public float startHeight = 80f;
    public float endHeight = 20f;

    [Header("Long Hills")]
    public float hill1Amplitude = 8f;
    public float hill1Length = 300f;

    [Header("Medium Hills")]
    public float hill2Amplitude = 2f;
    public float hill2Length = 140f;

    [Header("Side Variation")]
    public float sideAmplitude = 1.5f;
    public float sideLength = 400f;

    [ContextMenu("Generate")]
    public void Generate()
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat =
            UnityEngine.Rendering.IndexFormat.UInt32;

        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Vector2> uvs = new();

        for (int z = 0; z <= zSegments; z++)
        {
            float z01 = z / (float)zSegments;

            float downhill =
                Mathf.Lerp(startHeight, endHeight, z01);

            for (int x = 0; x <= xSegments; x++)
            {
                float x01 = x / (float)xSegments;

                float worldX = x01 * width;
                float worldZ = z01 * length;

                float longHill =
                    Mathf.Sin(worldZ * Mathf.PI * 2f / hill1Length)
                    * hill1Amplitude;

                float mediumHill =
                    Mathf.Sin(worldZ * Mathf.PI * 2f / hill2Length + 1.4f)
                    * hill2Amplitude;

                float side =
                    Mathf.Sin(worldX * Mathf.PI * 2f / sideLength)
                    * sideAmplitude;

                float y =
                    downhill +
                    longHill +
                    mediumHill +
                    side;

                vertices.Add(new Vector3(worldX, y, worldZ));
                uvs.Add(new Vector2(x01, z01));
            }
        }

        int vertsPerRow = xSegments + 1;

        for (int z = 0; z < zSegments; z++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int a = z * vertsPerRow + x;
                int b = a + 1;
                int c = a + vertsPerRow;
                int d = c + 1;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);

                triangles.Add(b);
                triangles.Add(c); 
                triangles.Add(d);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}