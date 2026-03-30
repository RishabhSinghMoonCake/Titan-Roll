using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeBoulder : MonoBehaviour
{
    public System.Action OnRunFinished;

    [Header("Arcade Tuning")]
    public float gravityMultiplier = 2.5f;
    public float slopeAcceleration = 20f;
    public float flatGroundDeceleration = 1.5f;
    public float brakingThreshold = 10f;
    public float brakingForce = 15f;

    [Header("Steering Tuning")]
    public float steeringForce = 40f;    // How hard we push sideways
    public float sideFriction = 5f;      // "Grip" (Higher = less drift/heavier feel)
    public float steeringDelay = 0.5f;   // Seconds to wait after launch before steering works

    private Rigidbody rb;
    private bool isLaunched = false;
    private float currentSpeedKmh;
    private float timeSinceLaunch = 0f;
    private float maxStartLaunchSpeedMs;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;
        rb.angularDrag = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Removed rb.mass = 50f; 
        // GameLevelManager now sets mass dynamically based on upgrades!

        rb.isKinematic = true;
    }

    /// <summary>
    /// Called by GameLevelManager when the player taps the screen.
    /// </summary>
    public void Launch(float launchSpeedKmh)
    {
        isLaunched = true;
        timeSinceLaunch = 0f;
        rb.isKinematic = false;

        float speedMs = launchSpeedKmh / 3.6f;
        maxStartLaunchSpeedMs = speedMs;

        // Slight upward angle for a nice pop off the launch pad
        Vector3 launchDir = (transform.forward + (Vector3.up * 0.15f)).normalized;
        rb.velocity = launchDir * speedMs;
    }

    /// <summary>
    /// Called by GameLevelManager every frame while launched.
    /// </summary>
    public void Steer(float input)
    {
        if (!isLaunched || timeSinceLaunch < steeringDelay) return;

        // Scale steering force based on current speed. 
        // If we are moving very slow, steering is less effective.
        float speedFactor = Mathf.InverseLerp(0, maxStartLaunchSpeedMs, rb.velocity.magnitude);

        Vector3 steerDir = Vector3.right * input * steeringForce;
        rb.AddForce(steerDir * speedFactor, ForceMode.Acceleration);
    }

    void FixedUpdate()
    {
        if (!isLaunched) return;

        timeSinceLaunch += Time.fixedDeltaTime;
        currentSpeedKmh = rb.velocity.magnitude * 3.6f;

        // 1. Gravity & Forward Logic
        rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        HandleSlopes();

        // 2. LATERAL FRICTION (The "Weight" Logic)
        // Kills sideways velocity (X) but keeps forward velocity (Z)
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
        float sidewaysDrag = -localVel.x * sideFriction;
        rb.AddForce(transform.right * sidewaysDrag, ForceMode.Acceleration);

        // 3. Stop Logic (Run ends if we are too slow after the initial launch window)
        if (currentSpeedKmh < brakingThreshold && timeSinceLaunch > 2.0f)
        {
            ApplyBraking();
        }
    }

    void HandleSlopes()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2.0f))
        {
            Vector3 slopeDir = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

            if (slopeDir.y < 0)
                rb.AddForce(slopeDir * slopeAcceleration, ForceMode.Acceleration); // Downhill speedup
            else
                rb.AddForce(-rb.velocity.normalized * flatGroundDeceleration, ForceMode.Acceleration); // Flat/Uphill drag
        }
    }

    void ApplyBraking()
    {
        rb.AddForce(-rb.velocity.normalized * brakingForce, ForceMode.Acceleration);

        if (currentSpeedKmh < 1f)
        {
            isLaunched = false;
            rb.isKinematic = true;
            Debug.Log("ArcadeBoulder: Run Finished smoothly.");
            OnRunFinished?.Invoke();
        }
    }

    // --- COLLISION UTILITIES ---

    public float GetCurrentSpeedMs() => rb.velocity.magnitude;

    /// <summary>
    /// Called by destructible objects when smashed.
    /// </summary>
    public void ApplyImpactSlowdown(float speedLossKmh)
    {
        float newSpeedKmh = currentSpeedKmh - speedLossKmh;

        // Safety Clamp: Don't drop below 5km/h so we don't get stuck inside debris
        if (newSpeedKmh < 5f) newSpeedKmh = 5f;

        rb.velocity = rb.velocity.normalized * (newSpeedKmh / 3.6f);
    }

    /// <summary>
    /// Called by solid obstacles (walls, mountains).
    /// </summary>
    public void ApplyBonk()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Slight bounce back visual feedback
        rb.AddForce(-transform.forward * 10f, ForceMode.Impulse);

        isLaunched = false;
        Debug.Log("ArcadeBoulder: BONK! Hit a solid wall.");
        OnRunFinished?.Invoke();
    }
}