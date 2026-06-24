using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CinemachineDynamicScaler : MonoBehaviour
{
    [Header("Offset Settings")]
    [Tooltip("The camera's offset when the target is at scale 1.")]
    public Vector3 baseOffset = new Vector3(0f, 2f, -5f);

    [Header("Scaling Math")]
    [Tooltip("How aggressively the camera pulls back as the object grows.")]
    public float scaleMultiplier = 1.5f;

    private CinemachineVirtualCamera vcam;
    private CinemachineTransposer transposer;
    private Transform currentTarget;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
        transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();

        if (transposer == null)
        {
            Debug.LogError($"[{gameObject.name}] Missing Transposer! Set the VCam 'Body' to 'Transposer'.");
        }
    }

    public void SetTarget(Transform newTarget)
    {
        currentTarget = newTarget;

        // Tell Cinemachine to follow and look at the actual object
        vcam.Follow = currentTarget;
        vcam.LookAt = currentTarget;

        UpdateScaleOffset();
    }

    private void LateUpdate()
    {
        if (currentTarget != null && transposer != null)
        {
            UpdateScaleOffset();
        }
    }

    private void UpdateScaleOffset()
    {
        // 1. Get the max scale of the target
        float maxScale = Mathf.Max(currentTarget.lossyScale.x, currentTarget.lossyScale.y, currentTarget.lossyScale.z);
        float safeScale = Mathf.Max(1f, maxScale);

        // 2. Apply Square Root falloff
        float rootScale = Mathf.Sqrt(safeScale);
        float finalScaleFactor = 1f + (rootScale - 1f) * scaleMultiplier;

        // 3. Directly push the math into Cinemachine's native Follow Offset
        transposer.m_FollowOffset = baseOffset * finalScaleFactor;
    }
}