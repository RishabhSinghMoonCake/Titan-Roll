using UnityEngine;

public class Destructible : MonoBehaviour
{
    [Header("Toughness Tier")]
    [Range(1, 50)]
    [Tooltip("1 = Flower, 50 = Apartment Building")]
    public int toughnessLevel = 1;

    [Header("References")]
    public GameObject fracturedPrefab;
    public GameObject dustEffectPrefab;

    private float _actualResistance;
    private float _actualRequiredMass;
    private float _actualHardness;
    private float _actualBaseReward;

    private bool _isBroken = false;

    private void Start()
    {
        // THE FIX: Hardcoding the absolute bounds here. 
        // Unity's Inspector can NEVER zero these out again!
        float minRes = 20f, maxRes = 25000f;
        float minMass = 0f, maxMass = 600f;
        float minHard = 0.2f, maxHard = 1.0f;
        float minRew = 2f, maxRew = 50000f;

        // Calculate where we are on the 1-to-50 scale as a percentage
        float t = (toughnessLevel - 1) / 49f;

        _actualResistance = Mathf.Lerp(minRes, maxRes, t);
        _actualRequiredMass = Mathf.Lerp(minMass, maxMass, t);
        _actualHardness = Mathf.Lerp(minHard, maxHard, t);
        _actualBaseReward = Mathf.Lerp(minRew, maxRew, t);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isBroken) return;

        if (other.CompareTag("Boulder"))
        {
            ArcadeBoulder boulder = other.GetComponent<ArcadeBoulder>();
            if (boulder == null) return;

            float playerMass = other.attachedRigidbody ? other.attachedRigidbody.mass : 50f;
            float playerSpeedMs = boulder.GetCurrentSpeedMs();

            // --- 1. THE GATEKEEPER ---
            if (playerMass < _actualRequiredMass)
            {
                boulder.ApplyBonk();
                return;
            }

            // --- 2. THE ENERGY DRAIN MATH ---
            float effectiveSpeed = Mathf.Sqrt(playerSpeedMs) * 6f;
            float impactPower = playerMass * effectiveSpeed;

            if (impactPower >= _actualResistance)
            {
                // --- SUCCESS: SMASH! ---
                _isBroken = true;

                // Calculate percentage of power used, multiplied by hardness
                float powerRatioUsed = _actualResistance / impactPower;
                float speedLossPercentage = Mathf.Clamp01(powerRatioUsed * _actualHardness);

                float exactCurrentSpeedKmh = playerSpeedMs * 3.6f;
                float speedLossKmh = exactCurrentSpeedKmh * speedLossPercentage;

                boulder.ApplyImpactSlowdown(speedLossKmh);

                if (RewardManager.Instance != null)
                {
                    RewardManager.Instance.ProcessDestructionReward(_actualBaseReward, transform.position.z);
                }

                Vector3 estimatedVel = other.attachedRigidbody ? other.attachedRigidbody.velocity : Vector3.forward * playerSpeedMs;
                Shatter(other.ClosestPoint(transform.position), estimatedVel);
            }
            else
            {
                // --- FAIL: BONK! ---
                boulder.ApplyBonk();
            }
        }
    }

    void Shatter(Vector3 hitPoint, Vector3 playerVelocity)
    {
        if (fracturedPrefab != null)
        {
            GameObject brokenObj = ObjectPooler.Instance.Spawn(fracturedPrefab, transform.position, transform.rotation);

            Rigidbody[] pieces = brokenObj.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in pieces)
            {
                rb.AddExplosionForce(_actualResistance / 2f, hitPoint, 10f);
                rb.velocity += playerVelocity * 0.7f;
            }

            DebrisFader fader = brokenObj.GetComponent<DebrisFader>();
            if (fader == null) fader = brokenObj.AddComponent<DebrisFader>();
            fader.BeginFade();
        }

        if (dustEffectPrefab != null)
        {
            ObjectPooler.Instance.Spawn(dustEffectPrefab, hitPoint, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}