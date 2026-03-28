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
    public float maxSteeringSpeed = 15f; // Cap sideways speed
    public float sideFriction = 5f;      // "Grip" (Higher = less drift/heavier feel)
    public float steeringDelay = 0.5f;   // Seconds to wait after launch before steering works

    private Rigidbody rb;
    private bool isLaunched = false;
    private float currentSpeedKmh;
    private float timeSinceLaunch = 0f;

    private float MaxStartLaunchSpeedMs;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;
        rb.angularDrag = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.mass = 50f;
        rb.isKinematic = true;
    }

    private void Start()
    {
        MaxStartLaunchSpeedMs = PlayerDataManager.Instance.GetTotalLaunchSpeed() / 3.6f;
    }

    public void Launch(float speedKmh)
    {
        isLaunched = true;
        timeSinceLaunch = 0f; // Reset Timer
        rb.isKinematic = false;

        float speedMs = speedKmh / 3.6f;
        Vector3 launchDir = (transform.forward + (Vector3.up * 0.15f)).normalized;
        rb.velocity = launchDir * speedMs; // Use rb.velocity in Unity < 6
        MaxStartLaunchSpeedMs = speedMs;
    }

    // NEW: Called by GameLevelManager every frame
    public void Steer(float input)
    {
        if (!isLaunched) return;
        if (timeSinceLaunch < steeringDelay) return; // The Delay

        // 1. Apply Sideways Force (World Space X)
        // We use ForceMode.Acceleration so Mass doesn't mess up the sensitivity
        Vector3 steerDir = Vector3.right * input * steeringForce;
        rb.AddForce(steerDir * Mathf.InverseLerp(0,MaxStartLaunchSpeedMs,rb.velocity.magnitude), ForceMode.Acceleration);
    }

    void FixedUpdate()
    {
        if (!isLaunched) return;

        timeSinceLaunch += Time.fixedDeltaTime;
        currentSpeedKmh = rb.velocity.magnitude * 3.6f; // Use rb.velocity

        // 1. Gravity & Forward Logic
        rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        HandleSlopes();

        // 2. LATERAL FRICTION (The "Weight" Logic)
        // We want to kill sideways velocity (X) but keep forward velocity (Z)
        // This makes the steering feel "tight" instead of "floaty"
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);

        // Apply drag ONLY to the X axis (Sideways)
        float sidewaysDrag = -localVel.x * sideFriction;

        // Apply back as World Force
        rb.AddForce(transform.right * sidewaysDrag, ForceMode.Acceleration);

        // 3. Stop Logic
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
            if (slopeDir.y < 0) rb.AddForce(slopeDir * slopeAcceleration, ForceMode.Acceleration);
            else rb.AddForce(-rb.velocity.normalized * flatGroundDeceleration, ForceMode.Acceleration);
        }
    }

    void ApplyBraking()
    {
        rb.AddForce(-rb.velocity.normalized * brakingForce, ForceMode.Acceleration);
        if (currentSpeedKmh < 1f)
        {
            isLaunched = false;
            rb.isKinematic = true;
            Debug.Log("Run Finished");
            OnRunFinished?.Invoke();
        }
    }

    // Helper to get raw speed for momentum calc
    public float GetCurrentSpeedMs()
    {
        return rb.velocity.magnitude;
    }

    /// <summary>
    /// Called when we smash through an object. 
    /// Subtracts specific speed instantly but keeps momentum flowing.
    /// </summary>
    public void ApplyImpactSlowdown(float speedLossKmh)
    {
        // 1. Calculate new speed
        float currentSpeedKmh = rb.velocity.magnitude * 3.6f;
        float newSpeedKmh = currentSpeedKmh - speedLossKmh;

        // 2. Safety Clamp
        // Never drop below 5km/h on a break (so you don't get stuck inside the debris)
        if (newSpeedKmh < 5f) newSpeedKmh = 5f;

        // 3. Apply
        Vector3 direction = rb.velocity.normalized;
        rb.velocity = direction * (newSpeedKmh / 3.6f);
    }

    /// <summary>
    /// Called when we hit something too hard to break.
    /// Acts like a solid wall collision.
    /// </summary>
    public void ApplyBonk()
    {
        // 1. Stop dead
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 2. Slight bounce back (Visual feedback)
        rb.AddForce(-transform.forward * 10f, ForceMode.Impulse);

        // 3. End the run
        isLaunched = false;

        Debug.Log("BONK! Run Failed.");
        OnRunFinished?.Invoke(); // Trigger Game Over screen
    }
}