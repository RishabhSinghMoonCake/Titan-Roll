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
        float minRes = 20f, maxRes = 25000f;
        float minMass = 0f, maxMass = 600f;
        float minHard = 0.2f, maxHard = 1.0f;
        float minRew = 2f, maxRew = 5000f;

        // Calculate where we are on the 1-to-50 scale as a percentage (0.0 to 1.0)
        float t = (toughnessLevel - 1) / 49f;

        _actualResistance = Mathf.Lerp(minRes, maxRes, t);
        _actualRequiredMass = Mathf.Lerp(minMass, maxMass, t);
        _actualHardness = Mathf.Lerp(minHard, maxHard, t);

        // --- THE ECONOMY SAVER: CUBIC REWARD CURVE ---
        // By cubing the percentage (t * t * t), the reward stays very low for a long time,
        // preventing early-game inflation, and then spikes massively for late-game buildings!
        float economyCurve = Mathf.Pow(t, 3);
        _actualBaseReward = Mathf.Lerp(minRew, maxRew, economyCurve);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_isBroken) return;

        if (other.CompareTag("Boulder"))
        {
            ArcadeBoulder boulder = other.GetComponent<ArcadeBoulder>();
            if (boulder == null) return;

            float playerSpeedMs = boulder.GetCurrentSpeedMs();

            // --- 1. THE MASS TRAP FIX ---
            // Unity defaults Rigidbody mass to 1. We force a minimum of 50 so the math never breaks!
            float playerMass = other.attachedRigidbody ? Mathf.Max(other.attachedRigidbody.mass, 50f) : 50f;

            // --- 2. THE GATEKEEPER ---
            if (playerMass < _actualRequiredMass)
            {
                boulder.ApplyBonk();
                return;
            }

            // --- 3. THE DEATH-SPIRAL FIX ---
            int strengthLevel = PlayerDataManager.Instance.data.strengthLevel;

            // We clamp the speed to a minimum of 10m/s for the calculation. 
            // This guarantees you never lose your sheer strength just because you slowed down!
            float speedFactor = Mathf.Clamp(playerSpeedMs, 10f, 100f) * 0.5f;

            // --- 4. THE BASE POWER BOOST ---
            // We add a baseline flat power (strength * 100) so even at a dead stop, 
            // a Level 12 boulder will effortlessly crush a Level 1 flower.
            float impactPower = (playerMass * strengthLevel * speedFactor) + (strengthLevel * 100f);

            if (impactPower >= _actualResistance)
            {
                // --- SUCCESS: SMASH! ---
                _isBroken = true;

                float powerRatioUsed = _actualResistance / impactPower;

                // THE PAPER-MACHE RULE:
                // If our power is 10x higher than the resistance (powerRatioUsed < 0.1f), 
                // we plow through it taking 0% damage!
                float damagePercentage = 0f;
                if (powerRatioUsed > 0.1f)
                {
                    damagePercentage = Mathf.Clamp01(powerRatioUsed * _actualHardness);
                }

                boulder.ApplyImpactSlowdown(damagePercentage);

                if (RewardManager.Instance != null)
                {
                    RewardManager.Instance.ProcessDestructionReward(_actualBaseReward, transform.position.z);
                }

                if (BoulderComboText.Instance != null)
                {
                    BoulderComboText.Instance.AddGold(_actualBaseReward);
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