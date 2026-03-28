using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoulderPhysics : MonoBehaviour
{
    [Header("Launch Settings")]
    [Tooltip("The fixed vertical angle for the launch (0=flat, 90=straight up).")]
    [Range(0f, 90f)]
    public float launchAngle = 35f;

    [Header("Rotation 'Juice'")]
    [SerializeField] private Transform boulderBody; // Assign the visual model of the boulder here
    [Tooltip("How fast the boulder spins relative to its forward speed.")]
    public float rotationMultiplier = 5f;
    [Tooltip("Maximum rotation speed to prevent physics glitches on mobile.")]
    public float maxRotationSpeed = 30f;

    [Header("Stop Logic")]
    [Tooltip("Speed below which the run is considered over.")]
    public float stopSpeedThreshold = 1.0f;
    [Tooltip("Time in seconds before we start checking for low speed (prevents instant stop).")]
    public float initialGracePeriod = 2.0f;

    // State
    private Rigidbody rb;
    private bool isLaunched = false;
    private float launchTimer = 0f;
    private bool isStopped = false;

    // Public Events (Optional: Subscribe to these for Game Over logic)
    public System.Action OnBoulderStopped;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // OPTIMIZATION: Mobile physics settings
        // Interpolate makes it look smooth even at low framerates
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Continuous detection prevents tunneling through terrain at high speeds
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Prevent accidental rolling before launch
        rb.isKinematic = true;
    }

    /// <summary>
    /// Call this from your Titan Input script.
    /// forceMagnitude: The 0-1 stretch amount * your max power.
    /// </summary>
    public void Launch(float forceMagnitude)
    {
        isLaunched = true;
        isStopped = false;
        launchTimer = 0f;
        rb.isKinematic = false; // Wake up physics

        // 1. Calculate Fixed Angle Vector
        // We take the object's forward direction, flatten it, and apply the angle.
        Vector3 flatForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

        // Create a rotation of 'launchAngle' degrees around the 'Right' axis relative to forward
        Vector3 launchDir = Quaternion.AngleAxis(-launchAngle, Vector3.Cross(flatForward, Vector3.up)) * flatForward;

        // 2. Apply Impulse
        rb.AddForce(launchDir * forceMagnitude, ForceMode.Impulse);
    }

    void FixedUpdate()
    {
        if (!isLaunched || isStopped) return;

        //HandleRotation();
        CheckStopCondition();
    }

    /// <summary>
    /// Forces visual rotation to match speed, creating "Juice" even in mid-air.
    /// </summary>
    void HandleRotation()
    {
        float currentSpeed = rb.velocity.magnitude; // or rb.velocity

        if (currentSpeed > 0.1f)
        {
            // 1. Calculate Rotation Axis (Perpendicular to movement)
            // If moving Forward (Z), we rotate around X (Right)
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, rb.velocity).normalized;

            // 2. Calculate Amount
            float rotationAmount = currentSpeed * rotationMultiplier * Time.deltaTime;

            // 3. Apply to CHILD only
            // Space.World ensures it spins correctly regardless of parent orientation
            boulderBody.Rotate(rotationAxis, rotationAmount, Space.World);
        }
    }

    /// <summary>
    /// Checks if the run has ended based on speed.
    /// </summary>
    void CheckStopCondition()
    {
        launchTimer += Time.fixedDeltaTime;

        // Don't check stop condition during the initial launch or bounce
        if (launchTimer < initialGracePeriod) return;

        // Use sqrMagnitude for mobile optimization (avoids square root calculation)
        if (rb.velocity.sqrMagnitude < (stopSpeedThreshold * stopSpeedThreshold))
        {
            StopRun();
        }
    }

    void StopRun()
    {
        isStopped = true;
        rb.angularVelocity = Vector3.zero;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true; // Sleep physics to save performance

        Debug.Log("Run Ended: Speed too low.");
        OnBoulderStopped?.Invoke();
    }

    // API for UI
    public float GetCurrentSpeed()
    {
        if (rb.isKinematic) return 0f;
        return rb.velocity.magnitude;
    }
}