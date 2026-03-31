using System.Collections;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class DynamicBoulderCamera : MonoBehaviour
{
    private CinemachineVirtualCamera _vcam;
    private CinemachineTransposer _transposer;
    private CinemachineComposer _composer;

    [Header("Camera Offsets (Size 1)")]
    [Tooltip("Where the camera sits while waiting on the pad")]
    public Vector3 idleFollowOffset = new Vector3(0f, 5f, -8f);

    [Tooltip("Where the camera locks in AFTER the launch sequence catches up")]
    public Vector3 flyingFollowOffset = new Vector3(0f, 6f, -12f);

    public float baseLookOffsetY = 1f;
    public float distanceMultiplier = 2.0f;

    [Header("Damping (Tight Follow)")]
    public float flyingDampingX = 0.5f;
    public float flyingDampingY = 0.1f;
    public float flyingDampingZ = 0.5f;

    [Header("Launch Sequence Tuning")]
    public float lagDampingZ = 10f;
    public float initialLagDelay = 0.2f;
    public float catchUpDuration = 1.5f;

    private Coroutine _launchCoroutine;
    private float _currentBoulderScale = 1.0f;
    private bool _isFlying = false;

    private void Awake()
    {
        _vcam = GetComponent<CinemachineVirtualCamera>();
        _transposer = _vcam.GetCinemachineComponent<CinemachineTransposer>();
        _composer = _vcam.GetCinemachineComponent<CinemachineComposer>();

        // Set to Idle state by default
        _isFlying = false;
        SetDamping(flyingDampingX, flyingDampingY, flyingDampingZ);
        ApplyOffset(idleFollowOffset);
    }

    /// <summary>
    /// Handles the scaling offset so the boulder doesn't clip the lens.
    /// </summary>
    public void UpdateCameraDistance(float currentBoulderScale)
    {
        _currentBoulderScale = currentBoulderScale;

        // Reapply the correct offset based on our current state (Idle vs Flying)
        Vector3 targetOffset = _isFlying ? flyingFollowOffset : idleFollowOffset;
        ApplyOffset(targetOffset);
    }

    /// <summary>
    /// Call this from GameLevelManager the moment the boulder is fired.
    /// </summary>
    public void TriggerLaunchSequence()
    {
        if (_launchCoroutine != null) StopCoroutine(_launchCoroutine);
        _launchCoroutine = StartCoroutine(LaunchSequenceRoutine());
    }

    private IEnumerator LaunchSequenceRoutine()
    {
        if (_transposer == null) yield break;

        _isFlying = true;

        // 1. THE SNAP: Instantly make the camera sluggish to create the lag effect
        SetDamping(flyingDampingX, flyingDampingY, lagDampingZ);

        // 2. THE LAG: Wait while the boulder shoots out
        yield return new WaitForSeconds(initialLagDelay);

        // 3. THE CATCH UP: Smoothly transition Damping AND Offset to the Flying state
        float elapsedTime = 0f;
        float startZDamping = _transposer.m_ZDamping;

        // Calculate what our actual starting offset was, taking current size into account
        Vector3 startOffset = _transposer.m_FollowOffset;

        // Calculate what our perfect flying offset should be right now
        float extraScale = _currentBoulderScale - 1f;
        Vector3 targetFlyingOffset = flyingFollowOffset + (flyingFollowOffset.normalized * (extraScale * distanceMultiplier));

        while (elapsedTime < catchUpDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / catchUpDuration;

            // SmoothStep makes the transition ease-in and ease-out (feels cinematic)
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Lerp Damping
            float currentZDamping = Mathf.Lerp(startZDamping, flyingDampingZ, smoothT);
            SetDamping(flyingDampingX, flyingDampingY, currentZDamping);

            // Lerp Offset
            _transposer.m_FollowOffset = Vector3.Lerp(startOffset, targetFlyingOffset, smoothT);

            yield return null;
        }

        // 4. LOCKED IN: Ensure we hit the exact final values
        SetDamping(flyingDampingX, flyingDampingY, flyingDampingZ);
        ApplyOffset(flyingFollowOffset);
    }

    // --- HELPER METHODS ---

    private void SetDamping(float x, float y, float z)
    {
        if (_transposer == null) return;
        _transposer.m_XDamping = x;
        _transposer.m_YDamping = y;
        _transposer.m_ZDamping = z;
    }

    private void ApplyOffset(Vector3 baseTargetOffset)
    {
        if (_transposer == null) return;

        float extraScale = _currentBoulderScale - 1f;
        Vector3 pushDirection = baseTargetOffset.normalized;

        _transposer.m_FollowOffset = baseTargetOffset + (pushDirection * (extraScale * distanceMultiplier));

        if (_composer != null)
        {
            Vector3 newLookOffset = _composer.m_TrackedObjectOffset;
            newLookOffset.y = baseLookOffsetY * _currentBoulderScale;
            _composer.m_TrackedObjectOffset = newLookOffset;
        }
    }
}