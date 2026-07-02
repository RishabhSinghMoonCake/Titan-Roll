using UnityEngine;

public class Destructible : MonoBehaviour
{
    [Header("Progression Thresholds")]
    [Tooltip("The player Mass Level required to start bypassing the max penalty.")]
    public int targetMassLevel = 1;

    [Tooltip("The maximum speed penalty (0.0 to 1.0) applied when the player is under or at the target level.")]
    [Range(0f, 1f)]
    public float maxDamagePenalty = 1.0f;

    [Header("References")]
    public GameObject fracturedPrefab;
    public GameObject dustEffectPrefab;

    // The custom tuning dial you requested
    private float _linearFactor = 0.4f;
    private bool _isBroken = false;

    void OnTriggerEnter(Collider other)
    {
        if (_isBroken) return;

        if (other.CompareTag("Boulder"))
        {
            ArcadeBoulder boulder = other.GetComponent<ArcadeBoulder>();
            if (boulder == null) return;

            // Get the player's current mass level directly from the save data
            int playerLevel = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.data.massLevel : 1;

            float damagePercentage = 0f;

            // --- THE PERCENTAGE MATH ---
            if (playerLevel <= targetMassLevel)
            {
                // Under-leveled or exactly at the level takes full penalty.
                damagePercentage = maxDamagePenalty;
            }
            else
            {
                // Linear degradation formula with the custom tuning factor
                float ratio = (float)targetMassLevel / playerLevel;
                float calculatedDamage = maxDamagePenalty * ratio * _linearFactor;

                // Clamp it so the factor never pushes it above the max penalty or below 0
                damagePercentage = Mathf.Clamp(calculatedDamage, 0f, maxDamagePenalty);
            }

            // --- SUCCESS: SMASH! ---
            _isBroken = true;

            // Debug the percentage to the console
            Debug.Log($"[Destructible] {gameObject.name} smashed by Level {playerLevel} Boulder! Damage Penalty: {damagePercentage * 100f:F1}%");

            // Apply the calculated slowdown to the boulder
            boulder.ApplyImpactSlowdown(damagePercentage);

            // Trigger Visuals & Destroy
            Vector3 estimatedVel = other.attachedRigidbody ? other.attachedRigidbody.velocity : Vector3.forward * boulder.GetCurrentSpeedMs();
            Shatter(other.ClosestPoint(transform.position), estimatedVel);
        }
    }

    void Shatter(Vector3 hitPoint, Vector3 playerVelocity)
    {
        // Play the dust effect
        if (dustEffectPrefab != null)
        {
            ObjectPooler.Instance.Spawn(dustEffectPrefab, hitPoint, Quaternion.identity);
        }

        // --- CALCULATE STRICT 45-DEGREE LAUNCH TRAJECTORY ---
        // 1. Get the pure horizontal direction of the boulder
        Vector3 horizontalDir = new Vector3(playerVelocity.x, 0f, playerVelocity.z).normalized;

        // Fallback just in case the boulder was completely stationary
        if (horizontalDir == Vector3.zero) horizontalDir = Vector3.forward;

        // 2. Combine it with Vector3.up to create a perfect 45-degree vector
        Vector3 launchDir45 = (horizontalDir + Vector3.up).normalized;

        // 3. Get the raw speed magnitude to determine how hard we hit it
        float impactSpeed = playerVelocity.magnitude;

        if (fracturedPrefab != null)
        {
            // PATH A: Fractured Prefab
            GameObject brokenObj = ObjectPooler.Instance.Spawn(fracturedPrefab, transform.position, transform.rotation);

            Rigidbody[] pieces = brokenObj.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody pieceRb in pieces)
            {
                // Push the debris along the 45-degree arc, matching the boulder's speed
                pieceRb.velocity = launchDir45 * (impactSpeed * 0.7f);

                // Add a small explosion just to scatter the pieces away from each other
                pieceRb.AddExplosionForce(200f, hitPoint, 5f);
            }

            DebrisFader fader = brokenObj.GetComponent<DebrisFader>();
            if (fader == null) fader = brokenObj.AddComponent<DebrisFader>();
            fader.BeginFade();

            Destroy(gameObject);
        }
        else
        {
            Rigidbody myRb = GetComponent<Rigidbody>();

            if (myRb != null)
            {
                // PATH B: Dynamic Rigidbody Punt
                myRb.isKinematic = false;

                // Launch the object exactly 45 degrees into the sky based on impact speed
                // We use Mathf.Max to guarantee at least a 15m/s punt even if the boulder is rolling slowly
                float puntForce = Mathf.Max(impactSpeed * 0.8f, 15f);
                myRb.AddForce(launchDir45 * puntForce, ForceMode.VelocityChange);

                myRb.AddTorque(Random.insideUnitSphere * 500f, ForceMode.Impulse);

                Collider[] colliders = GetComponentsInChildren<Collider>();
                foreach (Collider col in colliders) col.enabled = false;

                DebrisFader fader = GetComponent<DebrisFader>();
                if (fader == null) fader = gameObject.AddComponent<DebrisFader>();
                fader.BeginFade();

                Destroy(gameObject, 2.5f);
            }
            else
            {
                // PATH C: Insta-Destroy
                Destroy(gameObject);
            }
        }
    }
}