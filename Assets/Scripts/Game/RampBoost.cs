using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RampBoost : MonoBehaviour
{
    [Header("Speed Boost Settings")]
    [Tooltip("The percentage to increase the boulder's current speed by (e.g., 0.3 = 30% boost).")]
    [Range(0f, 2f)]
    public float boostPercentage = 0.3f;

    [Tooltip("If TRUE, launches the boulder exactly the way the ramp is facing. If FALSE, accelerates it in the direction it's already rolling.")]
    public bool forceRampDirection = true;

    [Header("Arcade Rewards")]
    [Tooltip("Restore some stamina to reward the player for hitting the ramp! Set to 0 to disable.")]
    public float staminaRestoreAmount = 15f;

    [Header("Visuals")]
    [Tooltip("Optional particle system (like wind lines or sparks) to play when triggered.")]
    public ParticleSystem boostParticles;

    private void Awake()
    {
        // Safety check to ensure the collider is always a trigger
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Boulder"))
        {
            ArcadeBoulder boulder = other.GetComponent<ArcadeBoulder>();
            Rigidbody rb = other.attachedRigidbody;

            if (boulder != null && rb != null)
            {
                // 1. Calculate the proportional speed addition
                float currentSpeed = rb.velocity.magnitude;
                float speedToAdd = currentSpeed * boostPercentage;

                // 2. Determine direction
                // Use transform.forward to force them over a jump, or rb.velocity to maintain their current trajectory
                Vector3 boostDir = forceRampDirection ? transform.forward : rb.velocity.normalized;

                // 3. Apply the percentage-based speed burst
                rb.AddForce(boostDir * speedToAdd, ForceMode.VelocityChange);

                // 4. Restore Stamina
                if (staminaRestoreAmount > 0f)
                {
                    boulder.currentStamina += staminaRestoreAmount;
                }

                // 5. Play visual effects
                if (boostParticles != null)
                {
                    boostParticles.Play();
                }

                Debug.Log($"<color=cyan>[RAMP BOOST]</color> Speed Increased by {boostPercentage * 100}% (+{speedToAdd:F1} m/s) | Stamina Restored: +{staminaRestoreAmount}");
            }
        }
    }
}