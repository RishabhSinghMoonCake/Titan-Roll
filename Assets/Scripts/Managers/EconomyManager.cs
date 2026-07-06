using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    [Header("Benchmarks (Level to clear 3km)")]
    public int benchmarkStrength = 25;
    public int benchmarkMass = 20;
    public int benchmarkIncome = 15;

    [Header("Cost Tuning")]
    public float baseCost = 50f;
    public float maxCostTarget = 35000f;
    [Tooltip("How steeply costs rise after the midpoint. 2.2 = slow start, aggressive end.")]
    public float costExponent = 2.2f;

    [Header("Prestige Inflation (The Steamroll Fix)")]
    [Tooltip("How aggressively the STARTING cost multiplies per Prestige. Set high (4.0 - 4.5) to fight Greed carryover!")]
    public float prestigeBaseInflation = 4.2f; // <-- NEW: Strongly inflates early levels!
    [Tooltip("How aggressively the MID-GAME target multiplies per Prestige. Keep lower than Base to let toughness decrease.")]
    public float prestigeTargetInflation = 3.2f; // <-- NEW: Keeps late-game achievable!

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Pure mathematical cost calculation with Hybrid Curve and Base Floor Compression.
    /// </summary>
    public int GetUpgradeCost(int currentLevel, int benchmarkLevel, float customBase = -1f)
    {

        float startCost = customBase > 0 ? customBase : baseCost;
        float targetCost = maxCostTarget;

        // 1. Calculate normalized progress against the benchmark (0.0 to 1.0+)
        float t = Mathf.Max(0f, (float)(currentLevel - 1) / (benchmarkLevel - 1));

        // 2. THE HYBRID CURVE BLEND (20% Linear + 80% Exponential)
        // This completely eliminates the "flat tail" where Levels 1-6 cost almost the same!
        float linearPart = t * 0.20f;
        float expoPart = Mathf.Pow(t, costExponent) * 0.80f;
        float curveValue = linearPart + expoPart;

        // 3. PRESTIGE BASE FLOOR COMPRESSION
        // We apply strong inflation to the base floor, and moderate inflation to the target.
        int prestigeLevel = PlayerPrefs.GetInt("PrestigeLevel", 1);
        if (prestigeLevel > 1 && Mathf.Approximately(customBase, 50f))
        {
            float baseMult = Mathf.Pow(prestigeBaseInflation, prestigeLevel - 1);
            float targetMult = Mathf.Pow(prestigeTargetInflation, prestigeLevel - 1);

            startCost *= baseMult;
            targetCost *= targetMult;
        }

        // 4. Calculate final price (Unclamped so it scales infinitely past the benchmark)
        float finalCost = startCost + ((targetCost - startCost) * curveValue);

        return Mathf.RoundToInt(finalCost);
    }

    public int GetStrengthCost(int lvl) => GetUpgradeCost(lvl, benchmarkStrength, 50f);
    public int GetMassCost(int lvl) => GetUpgradeCost(lvl, benchmarkMass, 50f);
    public int GetIncomeCost(int lvl) => GetUpgradeCost(lvl, benchmarkIncome, 150f);
}