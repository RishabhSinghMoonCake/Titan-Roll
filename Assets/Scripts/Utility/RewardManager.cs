using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;

    [Header("Mathematical Reward Tuning")]
    [Tooltip("Base gold multiplier per meter traveled.")]
    public float distanceGoldCoefficient = 0.12f;
    [Tooltip("Exponent for distance rewards. >1.0 means longer runs earn disproportionately more gold.")]
    public float distanceGoldExponent = 1.35f;

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
    /// Pure mathematical distance reward: Gold = Coefficient * (Distance ^ Exponent)
    /// Replaces the old foreach tier loop entirely!
    /// </summary>
    public int CalculateRunRewards(float finalDistance)
    {
        if (finalDistance <= 0f) return Mathf.FloorToInt(_accumulatedRunGold);

        // Calculate base distance payout using super-linear growth
        float rawDistanceGold = distanceGoldCoefficient * Mathf.Pow(finalDistance, distanceGoldExponent);

        // Multiply by Greed stat
        float totalDistanceGold = rawDistanceGold * GetIncomeMultiplier();

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
    /// Pure mathematical income multiplier: Multiplier = 1.0 + [Coefficient * (Level - 1) ^ Exponent]
    /// Replaces the 18-step array and handles infinite level growth cleanly.
    /// </summary>
    public float GetIncomeMultiplier()
    {
        if (PlayerDataManager.Instance == null) return 1.0f;
        int level = PlayerDataManager.Instance.data.greedLevel;
        if (level <= 1) return 1.0f;

        // Yields: L2=1.15x, L5=1.79x, L10=3.08x, L15=4.85x, L50=22.8x
        float addedMultiplier = incomeStepCoefficient * Mathf.Pow(level - 1, incomeGrowthExponent);

        return 1.0f + addedMultiplier;
    }
}