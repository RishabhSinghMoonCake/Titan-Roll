using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct GoldTier
{
    public float endDistance;
    public float goldPerMeter;
}

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;

    [Header("End of Run (Distance)")]
    public List<GoldTier> distanceTiers;

    private float _accumulatedRunGold = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Called when the boulder is launched
    public void StartNewRun()
    {
        _accumulatedRunGold = 0f;
    }

    // Called every time the boulder smashes an object
    public void ProcessDestructionReward(float baseReward, float zDistance)
    {

        // 2. Income Multiplier (Greed Level)
        float incomeMultiplier = GetIncomeMultiplier();

        // 3. The Final Cut
        float finalReward = baseReward * incomeMultiplier;

        _accumulatedRunGold += finalReward;

        // Optional: You could Instantiate a floating "+15 Gold" UI text right here!
    }

    // Called by GameLevelManager when the run finishes
    // 1. The Calculator: The UI calls this to see how much gold WILL be earned.
    public int CalculateRunRewards(float finalDistance)
    {
        float remainingDist = finalDistance;
        float distanceGold = 0f;
        float previousTierEnd = 0f;

        foreach (GoldTier tier in distanceTiers)
        {
            if (remainingDist <= 0) break;
            float tierLength = tier.endDistance - previousTierEnd;
            float distInTier = Mathf.Min(remainingDist, tierLength);
            distanceGold += distInTier * tier.goldPerMeter;
            remainingDist -= distInTier;
            previousTierEnd = tier.endDistance;
        }

        distanceGold *= GetIncomeMultiplier();
        return Mathf.FloorToInt(distanceGold + _accumulatedRunGold);
    }

    // 2. The Finalizer: Called AFTER the continue button is pressed and coins fly.
    public void FinalizeRunRewards(float finalDistance)
    {
        int totalEarned = CalculateRunRewards(finalDistance);

        // Pay the player
        PlayerDataManager.Instance.AddGold(totalEarned);

        // RESET the smash gold so it doesn't accidentally carry over to the next run!
        _accumulatedRunGold = 0f;
    }

    [Header("Income Progression")]
    [Tooltip("Define exact multipliers. Index 0 = Level 1, Index 1 = Level 2, etc.")]
    public float[] incomeMultiplierSteps = new float[]
    {
        1.0f, // Level 1 (Base)
        1.1f, // Level 2 (+0.1)
        1.2f, // Level 3 (+0.1)
        1.3f, // Level 4 
        1.6f, // Level 5 
        1.9f, // Level 6
        2.2f, // Level 7 
        2.5f, // Level 8
        2.7f, // Level 9
        3.0f,
        3.5f,
        4f,
        5f,
        7.5f,
        10f,
        10f,
        10f,
        10f
    };

    public float GetIncomeMultiplier()
    {
        if(PlayerDataManager.Instance == null) return 1.0f;
        int index = PlayerDataManager.Instance.data.greedLevel - 1; // Arrays start at 0, levels start at 1

        if (index < 0) return 1.0f;

        // If the array has the specific level defined, return it exactly.
        if (index < incomeMultiplierSteps.Length)
        {
            return incomeMultiplierSteps[index];
        }
        else
        {
            // INFINITE FALLBACK: If they level past 10, keep adding 0.5 per level automatically
            float lastDefinedValue = incomeMultiplierSteps[incomeMultiplierSteps.Length - 1];
            int extraLevels = index - (incomeMultiplierSteps.Length - 1);

            return lastDefinedValue + (extraLevels * 0.5f);
        }
    }
}