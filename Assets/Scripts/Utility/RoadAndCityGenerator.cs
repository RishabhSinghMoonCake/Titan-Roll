using UnityEngine;
using System.Collections.Generic;

public class StraightRoadAndCityGenerator : MonoBehaviour
{
    [Header("Line Path Configuration")]
    [Tooltip("Assign an empty GameObject where the road begins")]
    public Transform startPoint;
    [Tooltip("Assign an empty GameObject where the road ends")]
    public Transform endPoint;

    [Header("Road Prefab Generation")]
    public GameObject roadPrefab;
    public float roadPieceLength = 10f;
    public bool snapRoadToTerrain = true;

    [Header("Dense City Generation")]
    public GameObject[] buildingPrefabs;
    public float buildingSpacing = 5f;
    public float sideOffset = 6f;

    [Header("Terrain Alignment")]
    public LayerMask terrainLayer = ~0;
    public float raycastStartHeight = 100f;
    public float raycastDepth = 500f;

    [Header("Debugging")]
    public bool showDebugGizmos = true;
    [SerializeField, HideInInspector]
    private List<Vector3> debugBuildingSpawns = new List<Vector3>();

    private GameObject generationContainer;

    [ContextMenu("Generate Straight Road & City")]
    public void Generate()
    {
        if (startPoint == null || endPoint == null)
        {
            Debug.LogWarning("Please assign both a Start Point and an End Point.");
            return;
        }

        debugBuildingSpawns.Clear();

        if (generationContainer != null) DestroyImmediate(generationContainer);
        generationContainer = new GameObject("Straight_Environment_Root");
        generationContainer.transform.position = Vector3.zero;
        generationContainer.transform.rotation = Quaternion.identity;

        // Calculate absolute path direction and distance
        Vector3 direction = (endPoint.position - startPoint.position).normalized;
        float totalDistance = Vector3.Distance(startPoint.position, endPoint.position);

        // Calculate the vector pointing exactly right of our forward direction
        Vector3 rightNormal = Vector3.Cross(Vector3.up, direction).normalized;

        if (roadPrefab != null) SpawnRoadPieces(direction, totalDistance);
        if (buildingPrefabs != null && buildingPrefabs.Length > 0) SpawnDenseBuildings(direction, rightNormal, totalDistance);
    }

    private void SpawnRoadPieces(Vector3 forwardNormal, float totalDistance)
    {
        GameObject roadContainer = new GameObject("Road_Container");
        roadContainer.transform.SetParent(generationContainer.transform, true);

        float currentDistance = 0f;

        while (currentDistance <= totalDistance)
        {
            Vector3 spawnCenter = startPoint.position + (forwardNormal * currentDistance);
            Vector3 upNormal = Vector3.up;

            if (snapRoadToTerrain)
            {
                Vector3 rayStart = spawnCenter + Vector3.up * raycastStartHeight;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDepth, terrainLayer))
                {
                    spawnCenter = hit.point;
                    upNormal = hit.normal;
                }
            }

            // Project forward along the slope to prevent sideways twisting
            Vector3 projectedForward = Vector3.ProjectOnPlane(forwardNormal, upNormal).normalized;
            Quaternion rotation = Quaternion.LookRotation(projectedForward, upNormal);
            rotation = Quaternion.Euler(rotation.eulerAngles.x, 0f, 0f); // Lock Z rotation to prevent twisting

            GameObject road = Instantiate(roadPrefab, spawnCenter, rotation);
            road.transform.SetParent(roadContainer.transform, true);

            currentDistance += roadPieceLength;
        }
    }

    private void SpawnDenseBuildings(Vector3 forwardNormal, Vector3 rightNormal, float totalDistance)
    {
        GameObject cityContainer = new GameObject("City_Container");
        cityContainer.transform.SetParent(generationContainer.transform, true);

        float currentDistance = 0f;

        while (currentDistance <= totalDistance)
        {
            Vector3 centerPos = startPoint.position + (forwardNormal * currentDistance);

            Vector3 leftPos = centerPos - (rightNormal * sideOffset);
            Vector3 rightPos = centerPos + (rightNormal * sideOffset);

            SpawnBuildingOnTerrain(leftPos, rightNormal, Vector3.up, true, cityContainer.transform);
            SpawnBuildingOnTerrain(rightPos, rightNormal, Vector3.up, false, cityContainer.transform);

            currentDistance += buildingSpacing;
        }
    }

    private void SpawnBuildingOnTerrain(Vector3 topPosition, Vector3 roadRightNormal, Vector3 defaultUpNormal, bool isLeft, Transform parent)
    {
        Vector3 finalPos = topPosition;
        Vector3 finalUpNormal = defaultUpNormal;

        Vector3 rayStart = topPosition + Vector3.up * raycastStartHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDepth, terrainLayer))
        {
            finalPos = hit.point;
            finalUpNormal = hit.normal;
        }

        // Log the final calculated position for the visualizer
        debugBuildingSpawns.Add(finalPos);

        // Left side looks Right. Right side looks Left.
        Vector3 directionToFaceRoad = isLeft ? roadRightNormal : -roadRightNormal;
        Vector3 projectedForward = Vector3.ProjectOnPlane(directionToFaceRoad, finalUpNormal).normalized;
        Quaternion finalRotation = Quaternion.LookRotation(projectedForward, finalUpNormal);

        GameObject prefab = buildingPrefabs[UnityEngine.Random.Range(0, buildingPrefabs.Length)];
        GameObject building = Instantiate(prefab, finalPos, finalRotation);

        building.transform.SetParent(parent, true);
    }

    void OnDrawGizmos()
    {
        // Draw a cyan line between start and end point in the editor
        if (startPoint != null && endPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(startPoint.position, endPoint.position);
        }

        if (!showDebugGizmos || debugBuildingSpawns == null) return;

        Gizmos.color = Color.magenta;
        foreach (var pos in debugBuildingSpawns)
        {
            Gizmos.DrawSphere(pos, 2f);
        }
    }
}