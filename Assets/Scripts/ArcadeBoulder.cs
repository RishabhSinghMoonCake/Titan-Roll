using TMPro;
using UnityEngine;
using DG.Tweening;
using Cinemachine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeBoulder : MonoBehaviour
{
    public System.Action OnRunFinished;

    [Header("Arcade Tuning")]
    public float gravityMultiplier = 2.5f;
    public float sideFriction = 5f;
    public float steeringForce = 40f;
    public float steeringDelay = 0.5f;

    [Header("Slope Physics")]
    [Tooltip("How drastically hills affect speed. Higher = faster downhills, slower uphills.")]
    public float slopeInfluenceFactor = 40f;

    [Header("Upgrades (Applied via S-Curve)")]
    public float staminaDrainMultiplier = 1f;
    public float momentumRecoveryForce = 15f;

    [Header("Stamina System")]
    public float currentStamina;
    private float startingStamina;
    [Range(0.1f, 0.5f)]
    public float brakingZonePercentage = 0.25f;
    public float maxBrakeForce = 25f;

    private Rigidbody rb;
    private bool isLaunched = false;
    private float currentSpeedKmh;
    private float timeSinceLaunch = 0f;
    private float maxStartLaunchSpeedMs;
    private float stuckTimer = 0f;

    private Vector3 intendedHeading;

    [Header("UI & Distance")]
    [SerializeField] private TextMeshProUGUI distanceDisplay;

    [Header("Camera Tracking")]
    public Transform cameraTarget;

    [Header("Visuals")]
    public Transform visualMesh;
    public float visualRollSpeedMultiplier = 50f;

    private float _startZ;
    private int _lastDisplayedDistance = -1;
    private int _nextPopDistance = 500;

    private CinemachineImpulseSource _impulseSource;

    public static ArcadeBoulder Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;
        rb.angularDrag = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.isKinematic = true;
        rb.maxAngularVelocity = 150f;

        _impulseSource = GetComponent<CinemachineImpulseSource>();

        if (distanceDisplay != null) distanceDisplay.alpha = 0f;
    }

    public void ApplyUpgrades(int massLevel)
    {
        float t = Mathf.Clamp01((massLevel - 1) / 19f); // Level 1 to 20
        float sCurve = Mathf.SmoothStep(0f, 1f, t);

        // 1. STAMINA DRAIN (Curves from 1.0x down to 0.5x)
        staminaDrainMultiplier = Mathf.Lerp(1f, 0.5f, sCurve);

        // 2. CRUISE CONTROL PUSH (Curves from 10 force up to 25 force)
        momentumRecoveryForce = Mathf.Lerp(10f, 25f, sCurve);

        Debug.Log($"Upgrades Applied! Level: {massLevel} | Stamina Drain Multiplier: {staminaDrainMultiplier:F2}x");
    }

    public void Launch(float launchSpeedKmh, float powerPercentage = 1f)
    {
        isLaunched = true;
        timeSinceLaunch = 0f;
        stuckTimer = 0f;
        rb.isKinematic = false;

        transform.rotation = Quaternion.identity;
        rb.angularVelocity = Vector3.zero;

        float baseStamina = GameLevelManager.Instance.GetLaunchStamina();
        float staminaMultiplier = Mathf.Max(powerPercentage, 0.15f);
        currentStamina = baseStamina * staminaMultiplier;
        startingStamina = currentStamina;

        float speedMs = launchSpeedKmh / 3.6f;
        maxStartLaunchSpeedMs = speedMs;

        Vector3 launchDir = (Vector3.forward + (Vector3.up * 0.15f)).normalized;
        rb.velocity = launchDir * speedMs;

        intendedHeading = Vector3.forward;

        _startZ = transform.position.z;
        _lastDisplayedDistance = -1;
        _nextPopDistance = 500;

        if (distanceDisplay != null)
        {
            distanceDisplay.DOKill();
            distanceDisplay.transform.DOKill(true);
            distanceDisplay.text = "0 m";
            distanceDisplay.transform.localScale = Vector3.one;
            distanceDisplay.color = Color.white;
            distanceDisplay.DOFade(1f, 0.5f);
        }
    }

    private void Update()
    {
        if (!isLaunched || distanceDisplay == null) return;

        float currentDistance = transform.position.z - _startZ;
        if (currentDistance < 0) currentDistance = 0;

        int currentDistanceInt = Mathf.FloorToInt(currentDistance);

        if (currentDistanceInt > _lastDisplayedDistance)
        {
            _lastDisplayedDistance = currentDistanceInt;
            UpdateDistanceUI(currentDistanceInt);
        }

        if (visualMesh != null && rb.velocity.sqrMagnitude > 0.1f)
        {
            float speed = rb.velocity.magnitude;
            float rotationStep = (speed / visualMesh.lossyScale.x) * visualRollSpeedMultiplier * Time.deltaTime;
            Vector3 rollAxis = Vector3.Cross(Vector3.up, rb.velocity.normalized);
            visualMesh.Rotate(rollAxis, rotationStep, Space.World);
        }
    }

    private void LateUpdate()
    {
        if (!isLaunched || cameraTarget == null) return;

        // Remove the manual Y-lerping. 
        // We now let Cinemachine's Y-Damping act as our soft suspension!
        cameraTarget.position = transform.position;

        if (rb.velocity.sqrMagnitude > 1f)
        {
            // USE TRUE 3D VELOCITY: This allows the target to pitch UP and DOWN hills.
            Quaternion targetRotation = Quaternion.LookRotation(rb.velocity.normalized);

            // Turn the target smoothly, and let Cinemachine trail behind it organically
            cameraTarget.rotation = Quaternion.Slerp(cameraTarget.rotation, targetRotation, Time.deltaTime * 8f);
        }
    }

    private void UpdateDistanceUI(int distance)
    {
        if (distance < 1000)
        {
            distanceDisplay.text = $"{distance} m";
        }
        else
        {
            float km = distance / 1000f;
            distanceDisplay.text = $"{km:F1} km";
        }

        if (distance >= _nextPopDistance)
        {
            _nextPopDistance += 500;
            distanceDisplay.transform.DOKill(true);
            distanceDisplay.transform.localScale = Vector3.one;
            distanceDisplay.transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0.5f), 0.5f, vibrato: 5, elasticity: 1f);
            distanceDisplay.DOColor(new Color(1f, 0.8f, 0f, 1f), 0.15f).SetLoops(2, LoopType.Yoyo);
        }
    }

    public void Steer(float input)
    {
        if (!isLaunched || timeSinceLaunch < steeringDelay) return;

        float turnAmount = input * steeringForce * Time.deltaTime;
        Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);

        intendedHeading = turnRotation * intendedHeading;
        rb.velocity = turnRotation * rb.velocity;
    }

    void FixedUpdate()
    {
        if (!isLaunched) return;

        if (float.IsNaN(rb.velocity.x)) rb.velocity = Vector3.zero;

        timeSinceLaunch += Time.fixedDeltaTime;
        currentSpeedKmh = rb.velocity.magnitude * 3.6f;

        if (rb.velocity.z < -0.2f)
        {
            ApplyBraking();
            return;
        }

        // --- SLOPE DETECTION SYSTEM ---
        Vector3 currentNormal = Vector3.up;
        float rayDist = transform.localScale.y * 1.2f; // Scale safe raycast
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayDist))
        {
            currentNormal = hit.normal;
        }

        // Calculates how steep the hill is. 
        // Positive = Downhill, Negative = Uphill, 0 = Flat Ground
        float slopeDot = Vector3.Dot(intendedHeading.normalized, currentNormal);

        // This calculates the perfect forward direction angled ALONG the slope
        Vector3 slopeForward = Vector3.ProjectOnPlane(intendedHeading, currentNormal).normalized;

        float currentY = rb.velocity.y;
        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float flatSpeedMs = flatVelocity.magnitude;
        float flatSpeedKmh = flatSpeedMs * 3.6f;

        // HORIZONTAL-ONLY ARCADE GRIP
        if (flatSpeedMs > 0.5f)
        {
            Vector3 flatHeading = new Vector3(intendedHeading.x, 0, intendedHeading.z).normalized;
            Vector3 desiredFlatVelocity = flatHeading * flatSpeedMs;

            Vector3 newFlatVelocity = Vector3.Lerp(flatVelocity, desiredFlatVelocity, Time.fixedDeltaTime * sideFriction);
            rb.velocity = new Vector3(newFlatVelocity.x, currentY, newFlatVelocity.z);
        }

        // Base Gravity
        rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);

        // --- APPLY NATURAL SLOPE FORCES ---
        if (Mathf.Abs(slopeDot) > 0.05f)
        {
            // If downhill, slopeDot is positive -> pushes the boulder forward and down.
            // If uphill, slopeDot is negative -> pushes the boulder backward, naturally slowing it.
            rb.AddForce(slopeForward * (slopeInfluenceFactor * slopeDot), ForceMode.Acceleration);
        }

        float dynamicBrakeThreshold = Mathf.Max(startingStamina * brakingZonePercentage, 0.01f);

        // --- STAMINA DRAIN ---
        if (currentStamina > 0)
        {
            currentStamina -= Time.fixedDeltaTime * staminaDrainMultiplier;
            if (currentStamina < 0) currentStamina = 0;
        }

        // --- SMART CRUISE CONTROL ---
        // If we hit a steep uphill (slopeDot < -0.1f), the cruise control shuts off 
        // to allow gravity to naturally win and slow the boulder down.
        bool isSteepUphill = slopeDot < -0.1f;

        if (currentStamina > dynamicBrakeThreshold && !isSteepUphill)
        {
            if (flatSpeedMs < maxStartLaunchSpeedMs)
            {
                // Pushes ALONG the slope, avoiding terrain friction
                rb.AddForce(slopeForward * momentumRecoveryForce, ForceMode.Acceleration);
            }
        }

        // --- SMOOTH BRAKING (Final 25% Zone) ---
        if (currentStamina <= dynamicBrakeThreshold && currentStamina > 0f)
        {
            float rawIntensity = 1f - (currentStamina / dynamicBrakeThreshold);
            float smoothIntensity = Mathf.SmoothStep(0f, 1f, rawIntensity);

            float currentBrakeForce = maxBrakeForce * smoothIntensity;

            if (rb.velocity.sqrMagnitude > 0.01f && !float.IsNaN(currentBrakeForce))
            {
                // Braking always pushes against true velocity to stop safely
                rb.AddForce(-rb.velocity.normalized * currentBrakeForce, ForceMode.Acceleration);
            }
        }

        // --- FINAL STOP ---
        if (currentStamina == 0f)
        {
            if (rb.velocity.sqrMagnitude > 0.01f)
            {
                rb.AddForce(-rb.velocity.normalized * maxBrakeForce, ForceMode.Acceleration);
            }
        }

        // --- THE HILL STRUGGLE (Adjusted Anti-Crawl) ---
        // Lowered to 4 km/h so the boulder can dramatically struggle up a hill without dying instantly.
        if (flatSpeedKmh < 4f && timeSinceLaunch > 1.5f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= 0.5f)
            {
                ApplyBraking();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    void ApplyBraking()
    {
        isLaunched = false;
        rb.isKinematic = true;
        HideDistanceDisplay();

        Debug.Log("ArcadeBoulder: Run Finished smoothly.");
        OnRunFinished?.Invoke();
    }

    public float GetCurrentSpeedMs() => rb.velocity.magnitude;

    public void ApplyImpactSlowdown(float damagePercentage)
    {
        if (damagePercentage <= 0f) return;

        float newSpeedKmh = currentSpeedKmh * (1f - damagePercentage);
        if (newSpeedKmh < 5f) newSpeedKmh = 5f;

        rb.velocity = rb.velocity.normalized * (newSpeedKmh / 3.6f);

        float staminaPenalty = currentStamina * damagePercentage;
        currentStamina -= staminaPenalty;
        if (currentStamina < 0) currentStamina = 0;

        if (_impulseSource != null)
        {
            if (damagePercentage >= 0.20f)
            {
                float shakeForce = Mathf.Lerp(0.1f, 0.25f, damagePercentage);
                _impulseSource.GenerateImpulse(shakeForce);
            }
            else if (damagePercentage >= 0.05f)
            {
                float shakeForce = Mathf.Lerp(0.02f, 0.06f, damagePercentage);
                _impulseSource.GenerateImpulse(shakeForce);
            }
        }

        if (damagePercentage > 0.15f)
        {
            StartCoroutine(HitStopRoutine(damagePercentage));
        }

        Debug.Log($"<color=orange>[IMPACT]</color> Damage: {damagePercentage * 100f:F1}% | Speed: {newSpeedKmh:F0} km/h");
    }

    private System.Collections.IEnumerator HitStopRoutine(float damagePercentage)
    {
        Time.timeScale = 0.1f;
        float freezeDuration = 0.05f + (damagePercentage * 0.1f);
        yield return new WaitForSecondsRealtime(freezeDuration);
        Time.timeScale = 1f;
    }

    public void ApplyBonk()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(-Vector3.forward * 10f, ForceMode.Impulse);

        isLaunched = false;
        HideDistanceDisplay();

        Debug.Log("ArcadeBoulder: BONK! Hit a solid wall.");
        OnRunFinished?.Invoke();
    }

    private void HideDistanceDisplay()
    {
        if (distanceDisplay != null)
        {
            distanceDisplay.DOKill();
            distanceDisplay.transform.DOKill(true);
            distanceDisplay.DOFade(0f, 0.5f);
        }
    }
}