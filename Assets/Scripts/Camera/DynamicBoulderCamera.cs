using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class DynamicBoulderCamera : MonoBehaviour
{
    private CinemachineVirtualCamera _vcam;
    private CinemachineTransposer _transposer;
    private CinemachineComposer _composer;

    [Header("Base Camera Settings")]
    [Tooltip("The ideal offset when the boulder is at Size 1")]
    public Vector3 baseFollowOffset = new Vector3(0f, 5f, -10f);

    [Tooltip("How high to look above the parent pivot at Size 1")]
    public float baseLookOffsetY = 1f;

    [Header("Scaling Factor")]
    [Tooltip("How much extra distance to add per 1 unit of boulder scale")]
    public float distanceMultiplier = 2.0f;

    private void Awake()
    {
        _vcam = GetComponent<CinemachineVirtualCamera>();
        _transposer = _vcam.GetCinemachineComponent<CinemachineTransposer>();
        _composer = _vcam.GetCinemachineComponent<CinemachineComposer>();
    }

    /// <summary>
    /// Called by GameLevelManager whenever a mass upgrade is applied.
    /// </summary>
    public void UpdateCameraDistance(float currentBoulderScale)
    {
        if (_transposer == null || _composer == null) return;

        // Calculate how much bigger the boulder is than the base size
        float extraScale = currentBoulderScale - 1f;

        // Push the camera further away along its diagonal vector
        Vector3 pushDirection = baseFollowOffset.normalized;
        _transposer.m_FollowOffset = baseFollowOffset + (pushDirection * (extraScale * distanceMultiplier));

        // Adjust the LookAt focus so it stays centered on the larger mesh
        Vector3 newLookOffset = _composer.m_TrackedObjectOffset;
        newLookOffset.y = baseLookOffsetY * currentBoulderScale;
        _composer.m_TrackedObjectOffset = newLookOffset;
    }
}