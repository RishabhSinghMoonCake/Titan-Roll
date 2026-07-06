using UnityEngine;

public class SmoothVisualGlide : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Assign your main ArcadeBoulder Rigidbody object here.")]
    [SerializeField] private Rigidbody physicsProxy;

    [Tooltip("Assign the cameraTarget Transform that Cinemachine tracks.")]
    [SerializeField] private Transform cameraTarget;

    [Header("Softness Tuning")]
    [Tooltip("How fast the visual Y-axis catches up to the ground changes.")]
    [SerializeField] private float verticalSmoothSpeed = 10f;

    [Tooltip("How fast the visual X/Z axes catch up when moving outside the side deadzone.")]
    [SerializeField] private float horizontalSmoothSpeed = 15f;

    [Tooltip("Base slope rotation blending speed when moving slowly.")]
    [SerializeField] private float rotationSmoothSpeed = 5f;

    [Header("Dynamic Speed Scaling")]
    [Tooltip("How much of the boulder's velocity magnitude is added directly to tracking speeds to prevent high-speed lag.")]
    [Range(0f, 1f)]
    [SerializeField] private float speedScalingFactor = 0.35f;

    [Header("Micro-Bump & Jitter Rejection")]
    [Tooltip("Any horizontal (X/Z) physics jitter smaller than this distance (in meters) will be completely ignored.")]
    [SerializeField] private float horizontalDeadzone = 0.12f;

    [Tooltip("Any vertical (Y) physics jitter smaller than this distance (in meters) will be ignored.")]
    [SerializeField] private float verticalDeadzone = 0.15f;

    [Tooltip("Angles smaller than this (in degrees) won't trigger rapid rotation adjustments.")]
    [SerializeField] private float angularDeadzone = 3.0f;

    [Header("Nested Rolling Mesh")]
    [Tooltip("Assign the child mesh object that physically tumbles/rolls.")]
    [SerializeField] private Transform rollingMesh;
    [SerializeField] private float rollSpeedMultiplier = 50f;

    private Vector3 _lastPosition;
    private float _targetY;
    private Vector3 _targetHorizontalPos;
    private Quaternion _targetRotation;

    private float _dynamicPositionSpeed;

    private void Start()
    {
        if (physicsProxy == null)
        {
            Debug.LogError("SmoothVisualGlide: Please assign the Physics Proxy Rigidbody!", this);
            enabled = false;
            return;
        }

        transform.position = physicsProxy.position;
        transform.rotation = physicsProxy.rotation;

        _lastPosition = transform.position;
        _targetY = physicsProxy.position.y;
        _targetHorizontalPos = new Vector3(physicsProxy.position.x, 0f, physicsProxy.position.z);
        _targetRotation = physicsProxy.rotation;
    }

    private void Update()
    {
        if (physicsProxy == null) return;

        if (transform.localScale != physicsProxy.transform.localScale)
        {
            transform.localScale = physicsProxy.transform.localScale;
        }

        // --- DYNAMIC SPEED PERCENTAGE CALCULATION ---
        float proxySpeed = physicsProxy.velocity.magnitude;
        _dynamicPositionSpeed = verticalSmoothSpeed + (proxySpeed * speedScalingFactor);
        float dynamicHorizontalSpeed = horizontalSmoothSpeed + (proxySpeed * speedScalingFactor);
        float dynamicRotationSpeed = rotationSmoothSpeed + (proxySpeed * speedScalingFactor * 0.5f);

        Vector3 proxyPosWithOffset = physicsProxy.position;

        // --- FIX: HORIZONTAL SIDEWAY JITTER REJECTION ---
        Vector3 currentHorizontalPos = new Vector3(proxyPosWithOffset.x, 0f, proxyPosWithOffset.z);

        // Calculate the distance vector between where the physics ball is horizontally and our smoothed visual position
        Vector3 horizontalDelta = currentHorizontalPos - _targetHorizontalPos;

        // If the side physics rattle is larger than the deadzone, shift the window target
        if (horizontalDelta.magnitude > horizontalDeadzone)
        {
            // Push the target position so the visual softly trailing handles it instead of snapping snap-to-snap
            _targetHorizontalPos = currentHorizontalPos - (horizontalDelta.normalized * horizontalDeadzone);
        }

        // Smoothly blend horizontal tracking to remove any remaining microscopic ticks
        float horizontalT =
    1f - Mathf.Exp(-dynamicHorizontalSpeed * Time.deltaTime);

        Vector3 smoothedHorizontal =
            Vector3.Lerp(
                new Vector3(transform.position.x, 0f, transform.position.z),
                _targetHorizontalPos,
                horizontalT);

        // --- VERTICAL BUMP REJECTION ---
        // If the boulder is falling through the air, bypass the deadzone freeze completely to prevent stair-step jitter!
        if (physicsProxy.velocity.y < -0.1f)
        {
            _targetY = proxyPosWithOffset.y;
        }
        else
        {
            float verticalDelta = proxyPosWithOffset.y - _targetY;
            if (Mathf.Abs(verticalDelta) > verticalDeadzone)
            {
                // Smooth window-shifting for ground bumps to prevent hard snapping
                _targetY = proxyPosWithOffset.y - (Mathf.Sign(verticalDelta) * verticalDeadzone);
            }
        }

        // Prevent dropping below the actual physical position 
        float absoluteFloorY = physicsProxy.position.y;
        if (_targetY < absoluteFloorY)
        {
            _targetY = absoluteFloorY;
        }

        float verticalT =
    1f - Mathf.Exp(-_dynamicPositionSpeed * Time.deltaTime);

        float smoothedY =
            Mathf.Lerp(
                transform.position.y,
                _targetY,
                verticalT);

        // --- FINAL POSITION COMPOSITION ---
        transform.position = new Vector3(smoothedHorizontal.x, smoothedY, smoothedHorizontal.z);

        // Calculate visual velocity off the smoothed tracking data
        Vector3 visualVelocity = (transform.position - _lastPosition) / Time.deltaTime;
        _lastPosition = transform.position;

        // --- ANGULAR REJECTION FOR THE VISUAL MODEL ---
        if (physicsProxy.velocity.sqrMagnitude > 0.1f)
        {
            if (Quaternion.Angle(_targetRotation, physicsProxy.rotation) > angularDeadzone)
            {
                _targetRotation = physicsProxy.rotation;
            }

            float rotationT =
    1f - Mathf.Exp(-dynamicRotationSpeed * Time.deltaTime);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    _targetRotation,
                    rotationT);
        }

        // --- INTERNAL MESH SPIN ---
        if (rollingMesh != null && visualVelocity.sqrMagnitude > 0.1f)
        {
            float speed = visualVelocity.magnitude;
            float radius = transform.localScale.y * 0.5f;
            float rotationStep = (speed / radius) * rollSpeedMultiplier * Time.deltaTime;

            Vector3 rollAxis = Vector3.Cross(transform.up, visualVelocity.normalized);
            rollingMesh.Rotate(rollAxis, rotationStep, Space.World);
        }
    }

    private void LateUpdate()
    {
        if (physicsProxy == null || cameraTarget == null) return;

        cameraTarget.position = transform.position;

        if (physicsProxy.velocity.sqrMagnitude > 1f)
        {
            Vector3 travelDirection = physicsProxy.velocity;
            travelDirection.y = 0f;

            if (travelDirection.sqrMagnitude > 0.1f)
            {
                Quaternion flatHeadingRotation = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
                float cameraT =
    1f - Mathf.Exp(-_dynamicPositionSpeed * Time.deltaTime);

                cameraTarget.rotation =
                    Quaternion.Slerp(
                        cameraTarget.rotation,
                        flatHeadingRotation,
                        cameraT);
            }
        }
    }
}