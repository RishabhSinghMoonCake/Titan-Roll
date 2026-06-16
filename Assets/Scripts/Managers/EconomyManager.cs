using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    [Header("Benchmarks (To clear 3km)")]
    public int benchmarkStrength = 25;
    public int benchmarkMass = 20;
    public int benchmarkIncome = 10;

    [Header("Absolute Limit (God Mode)")]
    public int absoluteMaxLevel = 50; // The true hard cap

    [Header("Cost Curves (0.0 to 1.0)")]
    [Tooltip("Draw an exponential curve curving UP")]
    public AnimationCurve costCurve;

    [Header("Cost Boundaries")]
    public int baseCost = 50;
    public int maxCostTarget = 35000; // Extrapolated from your 28.7k at L23 data

    [Header("Income Multiplier Curve")]
    [Tooltip("Draw a curve for the Income upgrade multiplier")]
    public AnimationCurve incomeCurve;
    public float maxIncomeMultiplier = 5.0f; // At L10, gold earned is multiplied by 5

    [Header("Distance Reward Curve")]
    [Tooltip("Maps the base gold earned purely by distance traveled")]
    public AnimationCurve distanceRewardCurve;
    public int maxDistance = 3000; // Your target finish line
    public int maxBaseGoldAtFinish = 1500;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // --- 1. CALCULATE UPGRADE COSTS ---
    public int GetUpgradeCost(int currentLevel, int benchmarkLevel, float customBase = -1)
    {
        if (currentLevel >= absoluteMaxLevel) return 999999;

        // Calculate progress based on the BENCHMARK, not the max.
        // If currentLevel is 30, and benchmark is 25, 't' becomes 1.2!
        float t = (float)(currentLevel - 1) / (benchmarkLevel - 1);

        // Ask the graph what the multiplier should be at time 't'
        float curveValue = costCurve.Evaluate(t);

        int startCost = customBase > 0 ? (int)customBase : baseCost;

        // Use LerpUnclamped so the cost can scale smoothly past the maxCostTarget!
        return Mathf.RoundToInt(Mathf.LerpUnclamped(startCost, maxCostTarget, curveValue));
    }

    // Update your fetchers to use the benchmarks:
    public int GetStrengthCost(int lvl) => GetUpgradeCost(lvl, benchmarkStrength, 50);
    public int GetMassCost(int lvl) => GetUpgradeCost(lvl, benchmarkMass, 50);
    public int GetIncomeCost(int lvl) => GetUpgradeCost(lvl, benchmarkIncome, 150);

    // --- 2. CALCULATE RUN REWARDS ---
    public int CalculateRunGold(float distanceTraveled, int currentIncomeLevel)
    {
        // 1. Calculate Base Gold from Distance
        float distT = Mathf.Clamp01(distanceTraveled / maxDistance);
        float baseGoldCurveValue = distanceRewardCurve.Evaluate(distT);
        float baseGold = Mathf.Lerp(0, maxBaseGoldAtFinish, baseGoldCurveValue);

        // 2. Apply the Income Upgrade Multiplier
        float incT = Mathf.Clamp01((float)(currentIncomeLevel - 1) / (absoluteMaxLevel - 1));
        float incomeMultiplier = Mathf.Lerp(1.0f, maxIncomeMultiplier, incomeCurve.Evaluate(incT));

        return Mathf.RoundToInt(baseGold * incomeMultiplier);
    }
}