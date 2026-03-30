using UnityEngine;

public class Destructible : MonoBehaviour
{
    [Header("Object Settings")]
    [Tooltip("The 'Weight' of this object. Determines BOTH break difficulty AND speed penalty.")]
    public float resistanceMomentum = 2000f; // Fence=500, House=5000, BossWall=15000

    [Tooltip("How 'Hard' the impact feels. 0.5 = Arcade (Forgiving), 1.0 = Realistic (Punishing)")]
    public float hardnessFactor = 0.6f;

    [Header("References")]
    public GameObject fracturedPrefab;
    public GameObject dustEffectPrefab;

    private bool isBroken = false;

    void OnTriggerEnter(Collider other)
    {
        if (isBroken) return;

        if (other.CompareTag("Player"))
        {
            ArcadeBoulder boulder = other.GetComponent<ArcadeBoulder>();
            if (boulder == null) return;

            // 1. GATHER STATS
            float playerMass = other.attachedRigidbody ? other.attachedRigidbody.mass : 50f;
            float playerSpeedMs = boulder.GetCurrentSpeedMs();
            float impactMomentum = playerMass * playerSpeedMs;

            // 2. CHECK: CAN WE BREAK IT?
            if (impactMomentum >= resistanceMomentum)
            {
                // --- SUCCESS: SMASH! ---

                // A. Calculate Dynamic Speed Penalty
                // Formula: Velocity Change = Impulse / Mass
                // A Big House (5000 Resistance) penalizes 10x more than a Fence (500)
                float speedLossMs = (resistanceMomentum / playerMass) * hardnessFactor;
                float speedLossKmh = speedLossMs * 3.6f;

                Debug.Log($"SMASH! Object: {name} | Penalty: -{speedLossKmh:F1} km/h");

                // B. Apply Slowdown
                boulder.ApplyImpactSlowdown(speedLossKmh);

                // C. Visual Juice (Scaling shake based on object size)
                // If it's a big house (High Resistance), shake the camera HARD.
                float shakeIntensity = Mathf.Clamp(resistanceMomentum / 5000f, 0.5f, 3.0f);
                // GameLevelManager.Instance.ShakeCamera(0.2f, shakeIntensity); // Add this later

                // D. Spawn Debris
                Vector3 estimatedVel = other.attachedRigidbody ? other.attachedRigidbody.velocity : Vector3.forward * playerSpeedMs;
                Shatter(other.ClosestPoint(transform.position), estimatedVel);
            }
            else
            {
                // --- FAIL: BONK! ---
                Debug.Log($"FAIL! Object: {name} | Needed: {resistanceMomentum:F0} momentum");
                boulder.ApplyBonk();
            }
        }
    }


    void Shatter(Vector3 hitPoint, Vector3 playerVelocity)
    {
        isBroken = true;

        if (fracturedPrefab != null)
        {
            // NEW: USE POOL
            GameObject brokenObj = ObjectPooler.Instance.Spawn(fracturedPrefab, transform.position, transform.rotation);

            Rigidbody[] pieces = brokenObj.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in pieces)
            {
                rb.AddExplosionForce(resistanceMomentum / 2f, hitPoint, 10f);
                rb.velocity += playerVelocity * 0.7f;
            }

            // Ensure Fader uses Pool Return
            DebrisFader fader = brokenObj.GetComponent<DebrisFader>();
            if (fader == null) fader = brokenObj.AddComponent<DebrisFader>();
            fader.BeginFade(); // Start the timer manually
        }

        // FX (Also Pooled!)
        if (dustEffectPrefab != null) // Change variable type to GameObject for pooling
        {
            ObjectPooler.Instance.Spawn(dustEffectPrefab, hitPoint, Quaternion.identity);
        }

        // Original object is solid geometry, just disable it (or Destroy if not pooled)
        // Since solid objects are usually part of the Map Segments, they get destroyed 
        // when the map segment is destroyed. So just turning off renderer/collider is safer.
        gameObject.SetActive(false);
    }
}