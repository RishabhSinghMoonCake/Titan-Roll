using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeBoulder : MonoBehaviour
{
    public System.Action OnRunFinished;

    [Header("Arcade Tuning")]
    public float gravityMultiplier = 2.5f;
    public float flatGroundDeceleration = 8f; // Now acts as universal ground friction
    public float brakingThreshold = 10f;
    public float brakingForce = 15f;

    [Header("Steering Tuning")]
    public float steeringForce = 40f;
    public float sideFriction = 5f;
    public float steeringDelay = 0.5f;

    private Rigidbody rb;
    private bool isLaunched = false;
    private float currentSpeedKmh;
    private float timeSinceLaunch = 0f;
    private float maxStartLaunchSpeedMs;


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
    }

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

    public void Steer(float input)
    {
        if (!isLaunched || timeSinceLaunch < steeringDelay) return;

        // Scale steering force based on current speed. 
        float speedFactor = Mathf.InverseLerp(0, maxStartLaunchSpeedMs, rb.velocity.magnitude);

        Vector3 steerDir = Vector3.right * input * steeringForce;
        rb.AddForce(steerDir * speedFactor, ForceMode.Acceleration);
    }

    void FixedUpdate()
    {
        if (!isLaunched) return;

        timeSinceLaunch += Time.fixedDeltaTime;
        currentSpeedKmh = rb.velocity.magnitude * 3.6f;

        // 1. Gravity, Air Resistance, & Forward Logic
        rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        rb.AddForce(-rb.velocity.normalized * 1.5f, ForceMode.Acceleration);

        HandleGroundFriction();

        // 2. LATERAL FRICTION (The "Weight" Logic)
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
        float sidewaysDrag = -localVel.x * sideFriction;
        rb.AddForce(transform.right * sidewaysDrag, ForceMode.Acceleration);

        // 3. Stop Logic
        if (currentSpeedKmh < brakingThreshold && timeSinceLaunch > 2.0f)
        {
            ApplyBraking();
        }
    }

    void HandleGroundFriction()
    {
        // If we are touching the ground, apply our constant arcade friction
        // We no longer calculate slope angles or apply downhill acceleration.
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2.0f))
        {
            rb.AddForce(-rb.velocity.normalized * flatGroundDeceleration, ForceMode.Acceleration);
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

    public void ApplyImpactSlowdown(float speedLossKmh)
    {
        // CRITICAL FIX: Read the exact velocity right NOW, not the cached FixedUpdate one.
        // This guarantees if you hit 3 objects in one frame, the slowdown aggressively stacks!
        float exactCurrentSpeedKmh = rb.velocity.magnitude * 3.6f;

        if (exactCurrentSpeedKmh < 1f) return;

        float newSpeedKmh = exactCurrentSpeedKmh - speedLossKmh;
        if (newSpeedKmh < 5f) newSpeedKmh = 5f;

        rb.velocity = rb.velocity.normalized * (newSpeedKmh / 3.6f);
    }

    public void ApplyBonk()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(-transform.forward * 10f, ForceMode.Impulse);

        isLaunched = false;
        Debug.Log("ArcadeBoulder: BONK! Hit a solid wall.");
        OnRunFinished?.Invoke();
    }
}