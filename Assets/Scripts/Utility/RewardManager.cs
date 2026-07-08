using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;

    [Header("Mathematical Income Tuning")]
    [Tooltip("How fast the Greed multiplier grows per level.")]
    public float incomeStepCoefficient = 0.15f;
    [Tooltip("Exponent for Greed scaling. 1.2 provides steady, controlled growth forever.")]
    public float incomeGrowthExponent = 1.2f;

    private float _accumulatedRunGold = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartNewRun()
    {
        _accumulatedRunGold = 0f;
    }

    public void ProcessDestructionReward(float baseReward, float zDistance)
    {
        float incomeMultiplier = GetIncomeMultiplier();
        float finalReward = baseReward * incomeMultiplier;
        _accumulatedRunGold += finalReward;
    }

    /// <summary>
    /// Calculates distance rewards with an EXACT 1:1 payout up to 500m, 
    /// then scales up for mid and late-game distance milestones.
    /// </summary>
    public int CalculateRunRewards(float finalDistance)
    {
        if (finalDistance <= 0f) return Mathf.FloorToInt(_accumulatedRunGold);

        float baseDistanceGold = 0f;

        // --- RULE 1: FIRST 500m = EXACT 1:1 PAYOUT ---
        if (finalDistance <= 500f)
        {
            baseDistanceGold = finalDistance * 1.0f; // 100m = 100g, 500m = 500g
        }
        else if (finalDistance <= 1500f)
        {
            // 500m to 1500m: Pays 500g for the first 500m, plus 1.6x for meters beyond 500
            baseDistanceGold = 500f + ((finalDistance - 500f) * 0.8f);
        }
        else
        {
            // 1500m+: Pays 2100g for the first 1500m, plus 2.6x for meters beyond 1500
            baseDistanceGold = 2100f + ((finalDistance - 1500f) * 1.8f);
        }

        // Apply the player's Greed (Income) Multiplier
        float totalDistanceGold = baseDistanceGold * GetIncomeMultiplier() * EconomyManager.Instance.GetPrestigeRewardMultiplier();

        return Mathf.FloorToInt(totalDistanceGold + _accumulatedRunGold);
    }

    public void FinalizeRunRewards(float finalDistance)
    {
        int totalEarned = CalculateRunRewards(finalDistance);
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.AddGold(totalEarned);
        }
        _accumulatedRunGold = 0f;
    }

    /// <summary>
    /// Smooth income growth: L1=1.00x, L5=1.79x, L10=3.08x, L15=4.85x
    /// </summary>
    public float GetIncomeMultiplier()
    {
        if (PlayerDataManager.Instance == null) return 1.0f;
        int level = PlayerDataManager.Instance.data.greedLevel;
        if (level <= 1) return 1.0f;

        float addedMultiplier = incomeStepCoefficient * Mathf.Pow(level - 1, incomeGrowthExponent);
        return 1.0f + addedMultiplier;
    }
}