using System;

[Serializable]
public class PlayerData
{
    // --- Currency ---
    public double gold;
    public int diamonds;

    // --- Upgrade Levels ---
    public int massLevel;      // Rock Density
    public int strengthLevel;  // Kick Force
    public int greedLevel;     // Money Multiplier

    // --- Prestige/Progress ---
    public int currentLevelIndex; // For Biome progression
    public int prestigeCount;
    public float bestDistance;

    // Constructor sets default "New Game" values
    public PlayerData()
    {
        gold = 100; 
        diamonds = 0;

        massLevel = 1;
        strengthLevel = 1;
        greedLevel = 1;

        currentLevelIndex = 0;
        prestigeCount = 0;
        bestDistance = 0f;
    }
}