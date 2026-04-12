using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeBoulder : MonoBehaviour
{
    public System.Action OnRunFinished;

    [Header("Arcade Tuning")]
    public float gravityMultiplier = 2.5f;
    public float sideFriction = 5f;
    public float steeringForce = 40f;
    public float steeringDelay = 0.5f;

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
        // THE FIX: Unlock the rotation speed limit! (Default is only 7)
        rb.maxAngularVelocity = 150f;
    }

    public void Launch(float launchSpeedKmh)
    {
        isLaunched = true;
        timeSinceLaunch = 0f;
        stuckTimer = 0f;
        rb.isKinematic = false;

        transform.rotation = Quaternion.identity;
        rb.angularVelocity = Vector3.zero;

        currentStamina = GameLevelManager.Instance.GetLaunchStamina();
        startingStamina = currentStamina;

        float speedMs = launchSpeedKmh / 3.6f;
        maxStartLaunchSpeedMs = speedMs;

        Vector3 launchDir = (Vector3.forward + (Vector3.up * 0.15f)).normalized;
        rb.velocity = launchDir * speedMs;
    }

    public void Steer(float input)
    {
        if (!isLaunched || timeSinceLaunch < steeringDelay) return;

        float safeMaxSpeed = Mathf.Max(maxStartLaunchSpeedMs, 1f);
        float currentMag = float.IsNaN(rb.velocity.magnitude) ? 0f : rb.velocity.magnitude;

        // NEW: Force the scale to always be a positive number
        float sizeCompensator = Mathf.Abs(transform.localScale.x);

        float speedFactor = Mathf.Lerp(0.5f, 1.0f, currentMag / safeMaxSpeed);

        Vector3 steerDir = Vector3.right * input * (steeringForce * sizeCompensator);

        if (!float.IsNaN(steerDir.x) && !float.IsNaN(speedFactor))
        {
            rb.AddForce(steerDir * speedFactor, ForceMode.Acceleration);
        }
    }

    void FixedUpdate()
    {
        if (!isLaunched) return;

        // If velocity somehow got corrupted by an outside force, reset it immediately
        if (float.IsNaN(rb.velocity.x)) rb.velocity = Vector3.zero;

        timeSinceLaunch += Time.fixedDeltaTime;
        currentSpeedKmh = rb.velocity.magnitude * 3.6f;

        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float flatSpeedKmh = flatVelocity.magnitude * 3.6f;

        if (flatSpeedKmh < 2.5f)
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

        rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        // --- THE FIXED LATERAL FRICTION ---
        // We use World X (rb.velocity.x) instead of the tumbling local axes.
        // This guarantees friction only stops sideways sliding, and never pulls the ball backward or right!
        float sidewaysVelocity = rb.velocity.x;
        float sidewaysDrag = -sidewaysVelocity * sideFriction;
        rb.AddForce(Vector3.right * sidewaysDrag, ForceMode.Acceleration);

        if (currentStamina > 0)
        {
            currentStamina -= Time.fixedDeltaTime;
            if (currentStamina < 0) currentStamina = 0;
        }

        // --- THE NaN FIX ---
        // Ensure dynamicBrakeThreshold is never 0, preventing division by zero.
        float dynamicBrakeThreshold = Mathf.Max(startingStamina * brakingZonePercentage, 0.01f);

        if (currentStamina <= dynamicBrakeThreshold)
        {
            float rawIntensity = 1f - (currentStamina / dynamicBrakeThreshold);
            float smoothIntensity = Mathf.SmoothStep(0f, 1f, rawIntensity);

            float currentBrakeForce = maxBrakeForce * smoothIntensity;

            if (flatVelocity.sqrMagnitude > 0.01f && !float.IsNaN(currentBrakeForce))
            {
                rb.AddForce(-flatVelocity.normalized * currentBrakeForce, ForceMode.Acceleration);
            }
        }

        if (currentStamina == 0f)
        {
            if (flatVelocity.sqrMagnitude > 0.01f)
            {
                rb.AddForce(-flatVelocity.normalized * maxBrakeForce, ForceMode.Acceleration);
            }

            if (flatSpeedKmh < 5f)
            {
                ApplyBraking();
            }
        }
    }

    void ApplyBraking()
    {
        isLaunched = false;
        rb.isKinematic = true;
        Debug.Log("ArcadeBoulder: Run Finished smoothly.");
        OnRunFinished?.Invoke();
    }

    public float GetCurrentSpeedMs() => rb.velocity.magnitude;

    public void ApplyImpactSlowdown(float damagePercentage)
    {
        float newSpeedKmh = currentSpeedKmh * (1f - damagePercentage);
        if (newSpeedKmh < 5f) newSpeedKmh = 5f;

        rb.velocity = rb.velocity.normalized * (newSpeedKmh / 3.6f);

        float staminaPenalty = currentStamina * damagePercentage;
        currentStamina -= staminaPenalty;
        if (currentStamina < 0) currentStamina = 0;

        Debug.Log($"<color=orange>[IMPACT]</color> Damage Taken: <b>{damagePercentage * 100f:F1}%</b> | Speed dropped to: {newSpeedKmh:F0} km/h | Stamina Lost: {staminaPenalty:F1}s");
    }

    public void ApplyBonk()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(-Vector3.forward * 10f, ForceMode.Impulse);

        isLaunched = false;
        Debug.Log("ArcadeBoulder: BONK! Hit a solid wall.");
        OnRunFinished?.Invoke();
    }
}