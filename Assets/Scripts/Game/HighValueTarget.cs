using UnityEngine;
using DG.Tweening; // Required for the pop and hover animations

public class HighValueTarget : MonoBehaviour
{
    [Header("Toughness & Economy")]
    [Range(1, 50)]
    [Tooltip("Matches the standard toughness tiers so you know how hard it is to break")]
    public int toughnessLevel = 10;

    [Tooltip("How much total gold this drops when destroyed")]
    public float totalGoldReward = 500f;

    [Header("Visuals & Markers")]
    [Tooltip("The main center of the object to base the offsets on (Leave empty to use this GameObject)")]
    public Transform mainBodyTransform;

    [Tooltip("The gold coin or icon that hovers above")]
    public GameObject hoveringCoinPrefab;
    public Vector3 hoveringCoinOffset = new Vector3(0, 3f, 0);

    [Tooltip("The glowing ring or marker at the base")]
    public GameObject feetMarkerPrefab;
    public Vector3 feetMarkerOffset = new Vector3(0, 0.1f, 0);

    [Header("Destruction Effects")]
    public GameObject fracturedPrefab;
    [Tooltip("Special particle explosion for high-value targets")]
    public GameObject highValueDeathEffect;

    [SerializeField] private string prefName = "HV[NUMBER]";

    // Internal Math State
    private float _actualResistance;
    private float _actualRequiredMass;
    private float _actualHardness;
    private bool _isBroken = false;

    // Track spawned visuals so we can clean up their tweens
    private GameObject _spawnedCoin;
    private GameObject _spawnedFeetMarker;


    private void Start()
    {
        if(PlayerPrefs.GetInt(prefName , 0) == 1) 
        {
            Destroy(gameObject);
            return;
        }

        // 1. Calculate the exact same physics requirements as the standard destructibles
        float minRes = 20f, maxRes = 25000f;
        float minMass = 0f, maxMass = 600f;
        float minHard = 0.2f, maxHard = 1.0f;

        float t = (toughnessLevel - 1) / 49f;

        _actualResistance = Mathf.Lerp(minRes, maxRes, t);
        _actualRequiredMass = Mathf.Lerp(minMass, maxMass, t);
        _actualHardness = Mathf.Lerp(minHard, maxHard, t);

        // 2. Setup the visual markers using DOTween
        SetupVisualMarkers();
    }

    private void SetupVisualMarkers()
    {
        // Use the assigned body transform, or default to the root if none was assigned
        Transform anchor = mainBodyTransform != null ? mainBodyTransform : transform;

        // --- FEET MARKER ---
        if (feetMarkerPrefab != null)
        {
            _spawnedFeetMarker = Instantiate(feetMarkerPrefab, anchor);
            _spawnedFeetMarker.transform.localPosition = feetMarkerOffset;

            // Optional: Slowly pulse the feet marker using DOTween
            _spawnedFeetMarker.transform.DOScale(Vector3.one * 1.2f, 1f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        // --- HOVERING COIN ---
        if (hoveringCoinPrefab != null)
        {
            _spawnedCoin = Instantiate(hoveringCoinPrefab, anchor);
            _spawnedCoin.transform.localPosition = hoveringCoinOffset;

            // Start scale at zero for the "Pop" effect
            _spawnedCoin.transform.localScale = Vector3.zero;

            // Pop in!
            _spawnedCoin.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

            // Smoothly bob up and down
            _spawnedCoin.transform.DOLocalMoveY(hoveringCoinOffset.y + 0.5f, 1f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            // Continuously rotate 360 degrees
            _spawnedCoin.transform.DOLocalRotate(new Vector3(0, 360, 0), 1.5f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
        }
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

            // 1. THE GATEKEEPER CHECK
            if (playerMass < _actualRequiredMass)
            {
                boulder.ApplyBonk();
                return;
            }

            int strengthLevel = PlayerDataManager.Instance.data.strengthLevel;

            // Factor in the speed! We clamp it to a minimum of 5 so a slow roll doesn't become mathematically 0.
            // We use a 0.5f multiplier to keep the late-game values balanced with your economy.
            float speedFactor = Mathf.Max(playerSpeedMs, 5f) * 0.5f;

            float impactPower = playerMass * strengthLevel * speedFactor;

            if (impactPower >= _actualResistance)
            {
                // --- SUCCESS: SMASH! ---
                _isBroken = true;

                float powerRatioUsed = _actualResistance / impactPower;
                float damagePercentage = Mathf.Clamp01(powerRatioUsed * _actualHardness);

                boulder.ApplyImpactSlowdown(damagePercentage);

                // Give the massive custom reward
                if (RewardManager.Instance != null)
                {
                    RewardManager.Instance.ProcessDestructionReward(totalGoldReward, transform.position.z);
                }

                // --- THE NEW FLOATING TEXT TRIGGER ---
                if (BoulderComboText.Instance != null)
                {
                    BoulderComboText.Instance.AddGold(totalGoldReward);
                }

                Vector3 estimatedVel = other.attachedRigidbody ? other.attachedRigidbody.velocity : Vector3.forward * playerSpeedMs;
                Shatter(other.ClosestPoint(transform.position), estimatedVel);
                PlayerPrefs.SetInt(prefName, 1); // Mark this specific target as destroyed in PlayerPrefs
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
        // CRITICAL: Kill the tweens before destroying the object to prevent memory leaks!
        if (_spawnedCoin != null) _spawnedCoin.transform.DOKill();
        if (_spawnedFeetMarker != null) _spawnedFeetMarker.transform.DOKill();

        // Spawn the broken pieces
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

        // Spawn the unique high-value explosion
        if (highValueDeathEffect != null)
        {
            ObjectPooler.Instance.Spawn(highValueDeathEffect, hitPoint, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}