#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class DestructibleTools : EditorWindow
{
    [MenuItem("Tools/Plant Destructibles On Terrain")] 
    public static void PlantDestructibles()
    {
        Destructible[] allDestructibles = FindObjectsOfType<Destructible>();

        if (allDestructibles.Length == 0)
        {
            Debug.LogWarning("No Destructible objects found in the scene.");
            return;
        }

        int terrainLayerIndex = LayerMask.NameToLayer("Terrain");
        LayerMask mask = terrainLayerIndex != -1 ? (1 << terrainLayerIndex) : Physics.DefaultRaycastLayers;

        int successfulSnaps = 0;

        Undo.SetCurrentGroupName("Plant Destructibles");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Destructible dest in allDestructibles)
        {
            Transform t = dest.transform;

            Vector3 rayOrigin = new Vector3(t.position.x, 500f, t.position.z);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1000f, mask))
            {
                // Record the object BEFORE we change its position and rotation
                Undo.RecordObject(t, "Plant Destructible");

                // 1. Move the object to the ground
                t.position = hit.point;

                // 2. Align the object's rotation to the terrain's slope
                // We use Vector3.up as the base to ensure it preserves your original Y-axis rotation
                t.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * t.rotation;

                EditorUtility.SetDirty(t);

                successfulSnaps++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"<color=green>Successfully planted and aligned {successfulSnaps} out of {allDestructibles.Length} Destructibles on the terrain!</color>");
    }
}
#endif