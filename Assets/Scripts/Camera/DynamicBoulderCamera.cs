using System.Collections;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class DynamicBoulderCamera : MonoBehaviour
{
    private CinemachineVirtualCamera _vcam;
    private Cinemachine3rdPersonFollow _thirdPerson;
    private CinemachineComposer _composer;

    [Header("Camera Settings (Level 1)")]
    public float idleCameraDistance = 4.5f;
    public Vector3 idleShoulderOffset = new Vector3(0f, 2.5f, 0f);

    public float flyingCameraDistance = 5.5f;
    public Vector3 flyingShoulderOffset = new Vector3(0f, 3.0f, 0f);

    [Tooltip("How high on the boulder the camera looks. Keeps it centered.")]
    public float baseLookOffsetY = 0.5f;

    [Tooltip("Multiplier for how far back the camera pushes at high levels")]
    public float distanceMultiplier = 3.0f;

    [Header("Soft Suspension & Follow")]
    public float flyingDampingX = 0.5f;
    public float flyingDampingY = 1.5f;
    public float flyingDampingZ = 0.5f;

    [Header("Launch Sequence Tuning")]
    public float lagDampingZ = 10f;
    [Tooltip("How long the camera hangs back before snapping to follow")]
    public float initialLagDelay = 0.2f;

    private Coroutine _launchCoroutine;
    private float _currentBoulderScale = 1.0f;
    private bool _isFlying = false;

    private void Awake()
    {
        _vcam = GetComponent<CinemachineVirtualCamera>();
        _thirdPerson = _vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        _composer = _vcam.GetCinemachineComponent<CinemachineComposer>();

        _isFlying = false;
        SetDamping(flyingDampingX, flyingDampingY, flyingDampingZ);
        ApplyOffset(idleCameraDistance, idleShoulderOffset);
    }

    public void UpdateCameraDistance(float currentBoulderScale)
    {
        _currentBoulderScale = currentBoulderScale;
        float targetDist = _isFlying ? flyingCameraDistance : idleCameraDistance;
        Vector3 targetShoulder = _isFlying ? flyingShoulderOffset : idleShoulderOffset;
        ApplyOffset(targetDist, targetShoulder);
    }

    public void TriggerLaunchSequence()
    {
        if (_launchCoroutine != null) StopCoroutine(_launchCoroutine);
        _launchCoroutine = StartCoroutine(LaunchSequenceRoutine());
    }

    private IEnumerator LaunchSequenceRoutine()
    {
        if (_thirdPerson == null) yield break;

        _isFlying = true;

        // 1. THE LAG: Make the camera sluggish while the boulder blasts off
        SetDamping(flyingDampingX, flyingDampingY, lagDampingZ);
        yield return new WaitForSeconds(initialLagDelay);

        // 2. THE SNAP: Instantly restore standard damping and apply flying offsets
        SetDamping(flyingDampingX, flyingDampingY, flyingDampingZ);
        ApplyOffset(flyingCameraDistance, flyingShoulderOffset);
    }

    private void SetDamping(float x, float y, float z)
    {
        if (_thirdPerson == null) return;
        _thirdPerson.Damping = new Vector3(x, y, z);
    }

    private void ApplyOffset(float baseDist, Vector3 baseShoulder)
    {
        if (_thirdPerson == null) return;

        float extraScale = Mathf.Max(0, _currentBoulderScale - 1f);
        float dynamicPush = Mathf.Sqrt(extraScale) * distanceMultiplier;

        _thirdPerson.CameraDistance = baseDist + dynamicPush;
        _thirdPerson.ShoulderOffset = baseShoulder;

        if (_composer != null)
        {
            Vector3 newLookOffset = _composer.m_TrackedObjectOffset;
            newLookOffset.y = baseLookOffsetY + (extraScale * 0.4f);
            _composer.m_TrackedObjectOffset = newLookOffset;
        }
    }
}