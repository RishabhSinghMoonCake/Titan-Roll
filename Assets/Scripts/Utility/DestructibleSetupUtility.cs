using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DestructibleSetupUtility : MonoBehaviour
{
    [Header("Setup Configuration")]
    [Range(1, 50)]
    [Tooltip("The Target Mass Level that will be assigned to every child object.")]
    public int targetMassLevelToApply = 1;

    [Range(0f, 1f)]
    [Tooltip("The maximum damage penalty to assign to every child object.")]
    public float maxDamagePenaltyToApply = 1.0f;

    [Tooltip("The particle effect to assign to the dust effect prefab on every child object.")]
    public GameObject dustEffectPrefab;

    [ContextMenu("Setup Child Destructibles")]
    public void SetupChildren()
    {
        // Find all MeshRenderers in this object and its children[cite: 7]
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        int setupCount = 0;

#if UNITY_EDITOR
        // Group the undo action so Ctrl+Z removes all components at once[cite: 7]
        Undo.SetCurrentGroupName("Setup Child Destructibles");
        int undoGroup = Undo.GetCurrentGroup();
#endif

        foreach (MeshRenderer mr in renderers)
        {
            // Skip the parent object itself if it happens to have a MeshRenderer[cite: 7]
            if (mr.gameObject == this.gameObject) continue;

            GameObject childObj = mr.gameObject;

            // 1. Add Box Collider and set IsTrigger to TRUE[cite: 7]
            BoxCollider boxCol = childObj.GetComponent<BoxCollider>();
            if (boxCol == null)
            {
#if UNITY_EDITOR
                boxCol = Undo.AddComponent<BoxCollider>(childObj);
#else
                boxCol = childObj.AddComponent<BoxCollider>();
#endif
            }
            boxCol.isTrigger = true;

            // 2. REMOVE Rigidbody if it exists
            Rigidbody rb = childObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(rb);
#else
                DestroyImmediate(rb);
#endif
            }

            // 3. Add Destructible Script and apply the synced variables[cite: 7]
            Destructible destructible = childObj.GetComponent<Destructible>();
            if (destructible == null)
            {
#if UNITY_EDITOR
                destructible = Undo.AddComponent<Destructible>(childObj);
#else
                destructible = childObj.AddComponent<Destructible>();
#endif
            }

            // Apply all the exposed variables to sync with Destructible
            destructible.targetMassLevel = targetMassLevelToApply;
            destructible.maxDamagePenalty = maxDamagePenaltyToApply;
            destructible.dustEffectPrefab = dustEffectPrefab;

#if UNITY_EDITOR
            // Tell Unity this object changed so it knows to save the scene[cite: 7]
            EditorUtility.SetDirty(childObj);
#endif

            setupCount++;
        }

#if UNITY_EDITOR
        Undo.CollapseUndoOperations(undoGroup);
#endif

        Debug.Log($"<color=green>Successfully setup {setupCount} child objects with Trigger Colliders, Destructible scripts, synced damage/particles, and removed Rigidbodies!</color>");
    }
}