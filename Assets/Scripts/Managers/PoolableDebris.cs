using UnityEngine;

public class PoolableDebris : MonoBehaviour, IPooledObject
{
    // Struct to store original transforms of child pieces
    private struct ChildTransform
    {
        public Vector3 localPos;
        public Quaternion localRot;
        public Vector3 localScale;
    }

    private ChildTransform[] originalTransforms;
    private Rigidbody[] childRBs;

    private bool isInitialized = false;

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // 1. Cache Rigidbodies
        childRBs = GetComponentsInChildren<Rigidbody>();

        // 2. Cache Original "Perfect" Positions of all children
        int childCount = transform.childCount;
        originalTransforms = new ChildTransform[childCount];

        for (int i = 0; i < childCount; i++)
        {
            Transform t = transform.GetChild(i);
            originalTransforms[i] = new ChildTransform
            {
                localPos = t.localPosition,
                localRot = t.localRotation,
                localScale = t.localScale
            };
        }

        isInitialized = true;
    }

    public void OnObjectSpawn()
    {
        if (!isInitialized) Initialize();

        // 1. Reset Parent Scale (in case Fader shrunk it)
        transform.localScale = Vector3.one;

        // 2. Reset Children to "Perfect Shape"
        for (int i = 0; i < transform.childCount; i++)
        {
            if (i >= originalTransforms.Length) break;

            Transform t = transform.GetChild(i);
            t.localPosition = originalTransforms[i].localPos;
            t.localRotation = originalTransforms[i].localRot;
            t.localScale = originalTransforms[i].localScale;
            t.gameObject.SetActive(true);
        }

        // 3. Reset Physics (Stop moving)
        foreach (Rigidbody rb in childRBs)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false; // Ensure physics is on
        }
    }
}