using UnityEngine;
using System;
using System.IO;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;
    public PlayerData data;
    private string saveFilePath;

    // --- NEW GEOMETRIC CONFIG ---

    // 1. MASS (The Rock)
    
    private const float MASS_GROWTH_FACTOR = 100f;
    private const float BASE_MASS = 100f;

    private const float BASE_SPEED_KMH = 140f;
    private const float SPEED_PER_LEVEL = 14.0f;

    private const float BASE_GREED_MULTIPLIER = 1.0f;
    private const float GREED_PER_LEVEL = 0.05f;

    // 3. ECONOMY (The Wall)
    // 35% growth makes Level 50 prohibitively expensive (~$500M)
    private const float COST_GROWTH_FACTOR = 1.35f;

    // Base Costs
    private const int COST_BASE_MASS = 50;
    private const int COST_BASE_STRENGTH = 50;
    private const int COST_BASE_GREED = 100;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "TitanRoll_Save.json");
            Load();
        }
        else Destroy(gameObject);
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(saveFilePath, json);
    }

    public void Load()
    {
        if (File.Exists(saveFilePath))
        {
            data = JsonUtility.FromJson<PlayerData>(File.ReadAllText(saveFilePath));
        }
        else
        {
            data = new PlayerData(); // Starts with 1 Billion for testing
            Save();
        }
    }

    public void AddGold(int gold)
    {
        data.gold += gold;
        Save();
    }

    // --- NEW MATH METHODS ---

    public float GetTotalMass()
    {
        return BASE_MASS + ((data.massLevel - 1) * MASS_GROWTH_FACTOR);
    }

    public float GetTotalLaunchSpeed()
    {
        // Formula: 60 + (Level * 2)
        return BASE_SPEED_KMH + ((data.strengthLevel - 1) * SPEED_PER_LEVEL);
    }

    public float GetGoldMultiplier()
    {
        // Formula: 1.0 + (Level * 0.05)
        return BASE_GREED_MULTIPLIER + ((data.greedLevel - 1) * GREED_PER_LEVEL);
    }

    public double GetUpgradeCost(string type)
    {
        int level = 0;
        int baseCost = 0;

        switch (type)
        {
            case "Mass":
                level = data.massLevel;
                baseCost = COST_BASE_MASS;
                break;
            case "Strength":
                level = data.strengthLevel;
                baseCost = COST_BASE_STRENGTH;
                break;
            case "Greed":
                level = data.greedLevel;
                baseCost = COST_BASE_GREED;
                break;
        }

        // Geometric Price: Base * (1.35 ^ Level)
        return baseCost * Math.Pow(COST_GROWTH_FACTOR, level);
    }

    public bool TryBuyUpgrade(string type)
    {
        double cost = GetUpgradeCost(type);
        if (data.gold >= cost)
        {
            data.gold -= cost;
            switch (type)
            {
                case "Mass": data.massLevel++; break;
                case "Strength": data.strengthLevel++; break;
                case "Greed": data.greedLevel++; break;
            }
            Save();
            return true;
        }
        return false;
    }
}