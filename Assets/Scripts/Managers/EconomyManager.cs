using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    [Header("Benchmarks (Level to clear 3km)")]
    public int benchmarkStrength = 25;
    public int benchmarkMass = 20;
    public int benchmarkIncome = 15;

    [Header("Base Costs (Level 1)")]
    public float baseCost = 50f;
    public float incomeBaseCost = 150f;

    [Header("Growth Exponents")]
    [Tooltip("The exponential multiplier for standard upgrades (e.g., 1.35 means cost increases by 35% each level)")]
    public float standardGrowthExponent = 1.35f;

    [Tooltip("The exponential multiplier for the Income upgrade (e.g., 1.5 makes it significantly more expensive over time)")]
    public float incomeGrowthExponent = 1.5f;

    [Header("Late Game Smoothing (Anti-Explosion)")]
    [Tooltip("The level where exponential growth starts to slow down to prevent insane late-game costs.")]
    public float exponentialSoftCapLevel = 25f;
    [Tooltip("How much the growth is dampened after the soft cap (0.0 to 1.0). Lower = flatter cost curve later.")]
    public float lateGameDampening = 0.65f;

    [Header("Prestige Inflation (The Steamroll Fix)")]
    public float prestigeBaseInflation = 4.2f;
    [Tooltip("How much extra gold players earn per prestige to keep up with the inflated costs.")]
    public float prestigeRewardInflation = 4.2f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Simplified Exponential Cost Calculation with Late-Game Smoothing
    /// </summary>
    private int GetUpgradeCost(int currentLevel, float customBase = -1f, bool isIncome = false)
    {
        float startCost = customBase > 0 ? customBase : baseCost;
        float exponent = isIncome ? incomeGrowthExponent : standardGrowthExponent;

        // --- 1. LATE GAME SMOOTHING (The Anti-Explosion Fix) ---
        float effectiveLevel = currentLevel - 1;

        if (effectiveLevel > exponentialSoftCapLevel)
        {
            // Calculate how many levels the player is PAST the soft cap
            float overflowLevels = effectiveLevel - exponentialSoftCapLevel;

            // Severely dampen the overflow levels (e.g., 10 overflow levels might only act as 4 levels mathematically)
            // This turns the violent exponential curve into a smooth, manageable climb.
            effectiveLevel = exponentialSoftCapLevel + Mathf.Pow(overflowLevels, lateGameDampening);
        }

        // 2. Calculate the base exponential cost using the smoothed effective level
        float finalCost = startCost * Mathf.Pow(exponent, effectiveLevel);

        // 3. PRESTIGE INFLATION
        // Multiply the cost based on prestige level to combat steamrolling
        int prestigeLevel = PlayerPrefs.GetInt("PrestigeLevel", 1);
        if (prestigeLevel > 1)
        {
            finalCost *= Mathf.Pow(prestigeBaseInflation, prestigeLevel - 1);
        }

        // 4. Safety catch against Integer Overflow if levels get insanely high
        if (finalCost > int.MaxValue || float.IsInfinity(finalCost))
        {
            return int.MaxValue;
        }

        return Mathf.RoundToInt(finalCost);
    }

    // Pass the level, the base cost, and whether it's the income upgrade
    public int GetStrengthCost(int lvl) => GetUpgradeCost(lvl, baseCost, false);
    public int GetMassCost(int lvl) => GetUpgradeCost(lvl, baseCost, false);
    public int GetIncomeCost(int lvl) => GetUpgradeCost(lvl, incomeBaseCost, true);

    /// <summary>
    /// Use this in your RewardManager to multiply the player's gold payout so they can keep up with prestige costs!
    /// </summary>
    public float GetPrestigeRewardMultiplier()
    {
        int prestigeLevel = PlayerPrefs.GetInt("PrestigeLevel", 1);
        if (prestigeLevel <= 1) return 1f;

        // If they are Prestige 2, they earn 4.2x more gold. Prestige 3 = 17.6x more gold, etc.
        return Mathf.Pow(prestigeRewardInflation, prestigeLevel - 1);
    }
}