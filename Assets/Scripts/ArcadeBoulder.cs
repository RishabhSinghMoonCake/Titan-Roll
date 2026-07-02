using TMPro;
using UnityEngine;
using DG.Tweening;
using Cinemachine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeBoulder : MonoBehaviour
{
    public System.Action OnRunFinished;

    [Header("Arcade Tuning")]
    public float gravityMultiplier = 3.5f;
    public float sideFriction = 8f;
    public float steeringForce = 45f;
    public float steeringDelay = 0.5f;
    [Tooltip("Keeps the vehicle glued down to the slope transitions smoothly.")]
    public float groundStickyForce = 25f;

    [Header("Physics")]
    public LayerMask groundLayer = ~0;

    [Header("Slope Interaction")]
    public float normalSmoothSpeed = 10f;
    public float slopeInfluenceFactor = 45f;

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
    private Vector3 _smoothedNormal = Vector3.up;
    private bool isGrounded = false;

    [Header("UI & Distance")]
    [SerializeField] private TextMeshProUGUI distanceDisplay;
    [SerializeField] private TextMeshProUGUI speedDisplay;
    [SerializeField] private TextMeshProUGUI kmhText;

    [Tooltip("How fast the speedometer text catches up to the actual speed.")]
    public float speedUIDampening = 5f;
    [Tooltip("Divides the actual speed for UI display so the numbers aren't massive.")]
    public float speedUIDivisor = 2f; // Tweak this in the inspector!

    private float _displayedSpeed = 0f; // Tracks the smoothed UI number

    [Header("Visuals")]
    public Transform visualMesh;
    public float visualRollSpeedMultiplier = 50f;
    [Tooltip("Speed at which the visual container snaps its up-axis to the ground normal.")]
    public float visualNormalSnapSpeed = 12f;

    private float _startZ;
    private int _lastDisplayedDistance = -1;
    private int _nextPopDistance = 500;

    private CinemachineImpulseSource _impulseSource;

    private float _currentSteerInput; // Caches the input for FixedUpdate

    public static ArcadeBoulder Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;
        rb.angularDrag = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // CRITICAL for smooth tracking
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.freezeRotation = true; // Changed to TRUE: Keeps arcade physics completely stable
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.isKinematic = true;

        _impulseSource = GetComponent<CinemachineImpulseSource>();

        if (distanceDisplay != null) distanceDisplay.alpha = 0f;
        if (speedDisplay != null) speedDisplay.alpha = 0f;
        if(kmhText != null) kmhText.alpha = 0f;
    }

    public void ApplyUpgrades(int massLevel)
    {
        float t = Mathf.Clamp01((massLevel - 1) / 19f);
        float sCurve = Mathf.SmoothStep(0f, 1f, t);

        staminaDrainMultiplier = Mathf.Lerp(1f, 0.5f, sCurve);
        momentumRecoveryForce = Mathf.Lerp(10f, 25f, sCurve);
    }

    public void Launch(float launchSpeedKmh, float powerPercentage = 1f)
    {
        isLaunched = true;
        timeSinceLaunch = 0f;
        stuckTimer = 0f;
        rb.isKinematic = false;

        transform.rotation = Quaternion.identity;
        rb.angularVelocity = Vector3.zero;
        _smoothedNormal = Vector3.up;

        float baseStamina = GameLevelManager.Instance != null ? GameLevelManager.Instance.GetLaunchStamina() : 100f;
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

        if (speedDisplay != null)
        {
            _displayedSpeed = 0f;
            speedDisplay.DOKill();
            speedDisplay.text = "0 km/h";
            speedDisplay.color = Color.white;
            speedDisplay.DOFade(1f, 0.5f);
            kmhText.DOKill();
            kmhText.text = "km/h";
            kmhText.color = Color.white;
            kmhText.DOFade(1f, 0.5f);
        }
    }

    private void Update()
    {
        if (!isLaunched) return;

        // --- DISTANCE UI CALCULATION ---
        if (distanceDisplay != null)
        {
            float currentDistance = transform.position.z - _startZ;
            if (currentDistance < 0) currentDistance = 0;

            int currentDistanceInt = Mathf.FloorToInt(currentDistance);

            if (currentDistanceInt > _lastDisplayedDistance)
            {
                _lastDisplayedDistance = currentDistanceInt;
                UpdateDistanceUI(currentDistanceInt);
            }
        }

        if (speedDisplay != null)
        {
            // 1. Get the actual speed and scale it down for the UI
            float targetDisplaySpeed = (rb.velocity.magnitude * 3.6f) / speedUIDivisor;

            // 2. Smoothly transition the displayed speed toward the target speed
            _displayedSpeed = Mathf.Lerp(_displayedSpeed, targetDisplaySpeed, Time.deltaTime * speedUIDampening);

            // 3. Update the text (added " km/h" back in to match your Launch method)
            speedDisplay.text = $"{Mathf.FloorToInt(_displayedSpeed)}";
        }
    }

    private void UpdateDistanceUI(int distance)
    {
        distanceDisplay.text = $"{distance} m";
        
       

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

        // Only store the input. NEVER apply velocity in Update!
        _currentSteerInput = input;
    }

    void FixedUpdate()
    {
        if (!isLaunched) return;

        // Catch any existing NaNs (Your original safety net)
        if (float.IsNaN(rb.velocity.x) || float.IsNaN(rb.velocity.y) || float.IsNaN(rb.velocity.z))
        {
            rb.velocity = Vector3.forward * maxStartLaunchSpeedMs;
        }

        timeSinceLaunch += Time.fixedDeltaTime;
        currentSpeedKmh = rb.velocity.magnitude * 3.6f;

        if (rb.velocity.z < -0.2f)
        {
            ApplyBraking();
            return;
        }

        // ----------------------------
        // STEERING LOGIC (Moved here to prevent NaN errors!)
        // ----------------------------
        if (Mathf.Abs(_currentSteerInput) > 0.01f)
        {
            float turnAmount = _currentSteerInput * steeringForce * Time.fixedDeltaTime;

            // Safety check: ensure the normal isn't completely zero
            Vector3 safeNormal = _smoothedNormal.sqrMagnitude > 0.01f ? _smoothedNormal.normalized : Vector3.up;
            Quaternion turnRotation = Quaternion.AngleAxis(turnAmount, safeNormal);

            intendedHeading = turnRotation * intendedHeading;

            // Safety check: Do not normalize a dead-stop velocity vector!
            if (rb.velocity.sqrMagnitude > 0.001f)
            {
                float speed = rb.velocity.magnitude;
                rb.velocity = turnRotation * rb.velocity.normalized * speed;
            }
        }

        // Reset the input so it doesn't infinitely steer if you lift your finger
        _currentSteerInput = 0f;

        // ----------------------------
        // GROUND NORMAL DETECTION
        // ----------------------------
        float boulderRadius = transform.localScale.y * 0.5f;
        Vector3 currentNormal = Vector3.up;

        // Slightly long raycast to guarantee connection across steep downward drops
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, boulderRadius + 2.5f, groundLayer))
        {
            isGrounded = true;
            _smoothedNormal = Vector3.Slerp(_smoothedNormal, hit.normal, Time.fixedDeltaTime * normalSmoothSpeed);
            currentNormal = _smoothedNormal;
        }
        else
        {
            isGrounded = false;
            _smoothedNormal = Vector3.Slerp(_smoothedNormal, Vector3.up, Time.fixedDeltaTime * normalSmoothSpeed);
            currentNormal = _smoothedNormal;
        }

        // ----------------------------
        // GRAVITY & DRIVING LOGIC
        // ----------------------------
        Vector3 gravityForce = Physics.gravity * gravityMultiplier;
        rb.AddForce(gravityForce, ForceMode.Acceleration);

        // Sticky Downward force ensures the rigid body behaves smoothly over sudden hill apexes
        if (isGrounded)
        {
            rb.AddForce(-currentNormal * groundStickyForce, ForceMode.Acceleration);
        }

        Vector3 heading = intendedHeading;
        if (heading.sqrMagnitude < 0.001f) heading = Vector3.forward;
        heading.Normalize();

        // Calculate direct left/right alignment based on the slope surface
        Vector3 rightDir = Vector3.Cross(currentNormal, heading).normalized;
        float lateralSpeed = Vector3.Dot(rb.velocity, rightDir);
        rb.AddForce(-rightDir * (lateralSpeed * sideFriction), ForceMode.Acceleration);

        // Project forward direction entirely parallel onto the slope layout
        Vector3 slopeForward = Vector3.ProjectOnPlane(heading, currentNormal).normalized;
        float slopeDot = Vector3.Dot(slopeForward, Vector3.up);

        if (slopeDot < -0.05f) // Changed to only trigger on DOWNHILLS
        {
            // Downhill gravity assist. Uphills now rely entirely on entry momentum.
            rb.AddForce(slopeForward * (-slopeInfluenceFactor * slopeDot), ForceMode.Acceleration);
        }

        // --- STAMINA & ARCADE MOTOR LOGIC ---
        float dynamicBrakeThreshold = Mathf.Max(startingStamina * brakingZonePercentage, 0.01f);

        if (currentStamina > 0f)
        {
            currentStamina -= Time.fixedDeltaTime * staminaDrainMultiplier;
            currentStamina = Mathf.Max(0f, currentStamina);
        }

        bool isSteepUphill = slopeDot > 0.1f;

        if (currentStamina > dynamicBrakeThreshold && !isSteepUphill)
        {
            if (rb.velocity.magnitude < maxStartLaunchSpeedMs)
            {
                rb.AddForce(slopeForward * momentumRecoveryForce, ForceMode.Acceleration);
            }
        }

        if (currentStamina <= dynamicBrakeThreshold && currentStamina > 0f)
        {
            float rawIntensity = 1f - (currentStamina / dynamicBrakeThreshold);
            float brakeForce = maxBrakeForce * Mathf.SmoothStep(0f, 1f, rawIntensity);

            if (rb.velocity.sqrMagnitude > 0.01f)
            {
                rb.AddForce(-rb.velocity.normalized * brakeForce, ForceMode.Acceleration);
            }
        }

        if (currentStamina <= 0f && rb.velocity.sqrMagnitude > 0.01f)
        {
            rb.AddForce(-rb.velocity.normalized * maxBrakeForce, ForceMode.Acceleration);
        }

        // Stuck Detection
        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (flatVelocity.magnitude * 3.6f < 4f && timeSinceLaunch > 1.5f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= 0.5f) ApplyBraking();
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

        if (speedDisplay != null)
        {
            speedDisplay.DOKill();
            speedDisplay.DOFade(0f, 0.5f); // Fades the alpha

            // Tell DOTween to animate our tracker variable down to 0 over 0.5 seconds,
            // and update the text component on every step of that animation.
            DOTween.To(() => _displayedSpeed, x =>
            {
                _displayedSpeed = x;
                speedDisplay.text = $"{Mathf.FloorToInt(_displayedSpeed)}";
            }, 0f, 0.5f);

            if (kmhText != null)
            {
                kmhText.DOKill();
                kmhText.DOFade(0f, 0.5f);
            }
        }
    }
}