using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;

public class SplineRoadGenerator : MonoBehaviour
{
    [Header("Core Components")]
    [Tooltip("Reference to the Spline Container.")]
    public SplineContainer spline;
    [Tooltip("Parent transform for generated objects.")]
    public Transform container;

    [Header("Global Settings")]
    [Tooltip("Total width of the road mesh.")]
    public float roadWidth = 8f;
    [Tooltip("Distance over which two biomes might mix at boundaries.")]
    public float biomeBlendDistance = 25f;

    // Enums
    public enum GroundZone { Z0_LeftEdge, Z1_LeftMid, Z2_Center, Z3_RightMid, Z4_RightEdge }
    public enum PropRotationMode { SplineAligned, UprightRandomY, FullRandom, FaceRoadCenter }

    // Settings Classes
    [System.Serializable]
    public class SideSettings
    {
        [Tooltip("Prefabs to spawn on the side (e.g., Mountains, Walls).")]
        public GameObject[] prefabs;
        [Tooltip("Distance between side objects (lower = denser).")]
        public float spacing = 20f;
        [Tooltip("Offset relative to the spline center. X is automatically mirrored for Left/Right.")]
        public Vector3 positionOffset;
        [Tooltip("Base rotation offset.")]
        public Vector3 rotationOffset;
        [Tooltip("Random rotation variance.")]
        public Vector3 randomRotationOffset;
    }

    [System.Serializable]
    public class PropRule
    {
        [Tooltip("Objects to spawn on the road surface.")]
        public GameObject[] prefabs;
        [Tooltip("Probability of spawning (0 to 1).")]
        [Range(0, 1)] public float density = 0.2f;
        [Tooltip("Distance between spawn attempts.")]
        public float stepSize = 5f;

        [Tooltip("Which lanes/zones this prop can appear in.")]
        public GroundZone[] allowedZones;

        [Header("Orientation")]
        public PropRotationMode rotationMode;
        public Vector3 randomRotation;
        public bool alignToSurface;
        public float surfaceRayHeight = 10f;

        [Header("Clustering")]
        public bool clustered;
        public int clusterSize = 5;
        public float clusterRadius = 5f;
    }

    [System.Serializable]
    public class Biome
    {
        [Tooltip("Name for identification.")]
        public string name;
        [Tooltip("Length of this biome along the spline in meters.")]
        public float length = 100f;

        [Header("Road Visuals")]
        public GameObject[] roadPrefabs;

        [Header("Side Decoration")]
        public SideSettings leftSide;
        public SideSettings rightSide;

        [Header("Gameplay Props")]
        public List<PropRule> propRules;
    }

    public List<Biome> biomes;

    [ContextMenu("Generate World")]
    public void Generate()
    {
        // 1. Validation & Cleanup
        if (container == null) container = transform;
        if (spline == null) { Debug.LogError("Spline Container is missing!"); return; }

        while (container.childCount > 0)
            DestroyImmediate(container.GetChild(0).gameObject);

        float totalSplineLength = spline.CalculateLength();
        float currentDist = 0f;

        // 2. Iterate Biomes
        foreach (var biome in biomes)
        {
            if (currentDist >= totalSplineLength) break;

            float biomeStart = currentDist;
            float biomeEnd = Mathf.Min(currentDist + biome.length, totalSplineLength);

            // Create a parent for this biome to keep hierarchy clean
            GameObject biomeParent = new GameObject(biome.name);
            biomeParent.transform.parent = container;

            // -- Generation Steps --
            GenerateRoadSegment(biome, biomeStart, biomeEnd, totalSplineLength, biomeParent.transform);

            // Pass 'true' for left, 'false' for right
            GenerateSideDecor(biome.leftSide, biomeStart, biomeEnd, totalSplineLength, true, biomeParent.transform);
            GenerateSideDecor(biome.rightSide, biomeStart, biomeEnd, totalSplineLength, false, biomeParent.transform);

            GeneratePropsForBiome(biome, biomeStart, biomeEnd, totalSplineLength, biomeParent.transform);

            currentDist += biome.length;
        }
    }

    // -------------------------------------------------------------------------
    // ROAD GENERATION
    // -------------------------------------------------------------------------
    void GenerateRoadSegment(Biome biome, float startDist, float endDist, float totalLen, Transform parent)
    {
        float pointer = startDist;

        while (pointer < endDist)
        {
            // Pick current biome prefab
            GameObject prefab = biome.roadPrefabs[UnityEngine.Random.Range(0, biome.roadPrefabs.Length)];

            // Handle Blending: Chance to pick NEXT biome's road if near the end
            if (endDist - pointer < biomeBlendDistance)
            {
                int currentIdx = biomes.IndexOf(biome);
                if (currentIdx < biomes.Count - 1)
                {
                    float blendChance = 1f - ((endDist - pointer) / biomeBlendDistance);
                    if (UnityEngine.Random.value < blendChance)
                    {
                        var nextBiome = biomes[currentIdx + 1];
                        if (nextBiome.roadPrefabs.Length > 0)
                            prefab = nextBiome.roadPrefabs[UnityEngine.Random.Range(0, nextBiome.roadPrefabs.Length)];
                    }
                }
            }

            MeshFilter mf = prefab.GetComponent<MeshFilter>();
            if (mf == null) return;

            float meshLen = mf.sharedMesh.bounds.size.z;

            // Deform and place
            DeformRoadMesh(prefab, pointer, totalLen, parent);

            pointer += meshLen;
        }
    }

    void DeformRoadMesh(GameObject prefab, float distOnSpline, float totalSplineLen, Transform parent)
    {
        GameObject go = new GameObject("RoadSeg");
        go.transform.parent = parent;

        MeshRenderer sourceMR = prefab.GetComponent<MeshRenderer>();
        MeshRenderer newMR = go.AddComponent<MeshRenderer>();
        newMR.sharedMaterials = sourceMR.sharedMaterials;

        MeshFilter sourceMF = prefab.GetComponent<MeshFilter>();
        MeshFilter newMF = go.AddComponent<MeshFilter>();

        Mesh newMesh = new Mesh();
        Vector3[] sourceVerts = sourceMF.sharedMesh.vertices;
        Vector3[] newVerts = new Vector3[sourceVerts.Length];

        for (int i = 0; i < sourceVerts.Length; i++)
        {
            Vector3 v = sourceVerts[i];
            float vertDist = distOnSpline + v.z;
            float t = Mathf.Clamp01(vertDist / totalSplineLen);

            if (spline.Evaluate(t, out float3 pos, out float3 tan, out float3 up))
            {
                Quaternion rot = Quaternion.LookRotation(tan, up);
                newVerts[i] = (Vector3)pos + (rot * new Vector3(v.x, v.y, 0));
            }
        }

        newMesh.vertices = newVerts;
        newMesh.triangles = sourceMF.sharedMesh.triangles;
        newMesh.uv = sourceMF.sharedMesh.uv;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        newMF.mesh = newMesh;
        go.AddComponent<MeshCollider>();
    }

    // -------------------------------------------------------------------------
    // SIDE DECORATION (FIXED)
    // -------------------------------------------------------------------------
    void GenerateSideDecor(SideSettings settings, float startDist, float endDist, float totalLen, bool isLeft, Transform parent)
    {
        if (settings == null || settings.prefabs.Length == 0 || settings.spacing <= 0.1f) return;

        float pointer = startDist;

        while (pointer < endDist)
        {
            float t = Mathf.Clamp01(pointer / totalLen);

            if (spline.Evaluate(t, out float3 pos, out float3 tan, out float3 up))
            {
                Quaternion splineRot = Quaternion.LookRotation(tan, up);

                // --- FIX: Force correct side ---
                // We use Mathf.Abs to ensure the base offset is positive, 
                // then Negate it if isLeft is true.
                float sideMultiplier = isLeft ? -1f : 1f;
                float finalX = Mathf.Abs(settings.positionOffset.x) * sideMultiplier;

                Vector3 calculatedOffset = new Vector3(finalX, settings.positionOffset.y, settings.positionOffset.z);

                GameObject prefab = settings.prefabs[UnityEngine.Random.Range(0, settings.prefabs.Length)];
                GameObject obj = Instantiate(prefab, parent);

                obj.transform.position = (Vector3)pos + (splineRot * calculatedOffset);

                Vector3 randRot = new Vector3(
                    UnityEngine.Random.Range(-settings.randomRotationOffset.x, settings.randomRotationOffset.x),
                    UnityEngine.Random.Range(-settings.randomRotationOffset.y, settings.randomRotationOffset.y),
                    UnityEngine.Random.Range(-settings.randomRotationOffset.z, settings.randomRotationOffset.z)
                );

                obj.transform.rotation = splineRot * Quaternion.Euler(settings.rotationOffset + randRot);
            }

            pointer += settings.spacing;
        }
    }

    // -------------------------------------------------------------------------
    // PROP GENERATION
    // -------------------------------------------------------------------------
    void GeneratePropsForBiome(Biome biome, float startDist, float endDist, float totalLen, Transform parent)
    {
        if (biome.propRules == null) return;

        foreach (var rule in biome.propRules)
        {
            // Reset pointer to start of biome for every rule
            float pointer = startDist;

            while (pointer < endDist)
            {
                if (UnityEngine.Random.value <= rule.density)
                {
                    GroundZone zone = rule.allowedZones[UnityEngine.Random.Range(0, rule.allowedZones.Length)];
                    Vector3 splinePos = GetZonePosition(pointer, zone, totalLen);

                    if (rule.clustered)
                        SpawnPropCluster(rule, splinePos, pointer, totalLen, parent);
                    else
                        SpawnSingleProp(rule, splinePos, pointer, totalLen, parent);
                }
                pointer += rule.stepSize;
            }
        }
    }

    void SpawnPropCluster(PropRule rule, Vector3 centerPos, float splineDist, float totalLen, Transform parent)
    {
        for (int i = 0; i < rule.clusterSize; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * rule.clusterRadius;
            Vector3 offsetPos = centerPos + new Vector3(circle.x, 0, circle.y);
            SpawnSingleProp(rule, offsetPos, splineDist, totalLen, parent);
        }
    }

    void SpawnSingleProp(PropRule rule, Vector3 approxPos, float dist, float totalLen, Transform parent)
    {
        Vector3 finalPos = approxPos;
        Vector3 surfaceNormal = Vector3.up;

        if (rule.alignToSurface)
        {
            Ray ray = new Ray(approxPos + Vector3.up * rule.surfaceRayHeight, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                finalPos = hit.point;
                surfaceNormal = hit.normal;
            }
        }

        GameObject prefab = rule.prefabs[UnityEngine.Random.Range(0, rule.prefabs.Length)];
        GameObject obj = Instantiate(prefab, finalPos, Quaternion.identity, parent);

        float t = Mathf.Clamp01(dist / totalLen);
        Vector3 splineTan = spline.EvaluateTangent(t);
        Vector3 splinePos = spline.EvaluatePosition(t);

        Quaternion finalRot = Quaternion.identity;

        switch (rule.rotationMode)
        {
            case PropRotationMode.SplineAligned:
                finalRot = Quaternion.LookRotation(splineTan, surfaceNormal);
                break;
            case PropRotationMode.UprightRandomY:
                finalRot = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
                break;
            case PropRotationMode.FullRandom:
                finalRot = Quaternion.Euler(UnityEngine.Random.insideUnitSphere * 360);
                break;
            case PropRotationMode.FaceRoadCenter:
                Vector3 dirToCenter = (splinePos - finalPos).normalized;
                dirToCenter.y = 0;
                finalRot = Quaternion.LookRotation(dirToCenter, surfaceNormal);
                break;
        }

        if (rule.rotationMode != PropRotationMode.FullRandom && rule.randomRotation != Vector3.zero)
        {
            Vector3 rand = new Vector3(
                UnityEngine.Random.Range(-rule.randomRotation.x, rule.randomRotation.x),
                UnityEngine.Random.Range(-rule.randomRotation.y, rule.randomRotation.y),
                UnityEngine.Random.Range(-rule.randomRotation.z, rule.randomRotation.z)
            );
            finalRot *= Quaternion.Euler(rand);
        }

        obj.transform.rotation = finalRot;
    }

    Vector3 GetZonePosition(float dist, GroundZone zone, float totalLen)
    {
        float t = Mathf.Clamp01(dist / totalLen);
        if (spline.Evaluate(t, out float3 pos, out float3 tan, out float3 up))
        {
            Quaternion rot = Quaternion.LookRotation(tan, up);
            Vector3 right = rot * Vector3.right;

            float zoneWidth = roadWidth / 5f;
            int zoneIndex = (int)zone;

            float startX = -(roadWidth / 2f) + (zoneIndex * zoneWidth);
            float randomX = startX + UnityEngine.Random.Range(0, zoneWidth);

            return (Vector3)pos + (right * randomX);
        }
        return Vector3.zero;
    }
}