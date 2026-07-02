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

    private float GetIncomeMultiplier()
    {
        if (PlayerDataManager.Instance == null) return 1f;
        return 1.0f + ((PlayerDataManager.Instance.data.greedLevel - 1) * 0.1f);
    }
}