using UnityEngine;
using System;
using System.IO;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;
    public PlayerData data;
    private string saveFilePath;

    [Header("Economy Settings")]
    public float costGrowthFactor = 1.35f;
    public int baseMassCost = 50;
    public int baseStrengthCost = 50;
    public int baseGreedCost = 100;

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
            data = new PlayerData();
            Save();
        }
    }

    public void AddGold(int gold)
    {
        data.gold += gold;
        data.gold = 9999999999;
        Save();
    }

    // --- MATH ---

    public double GetUpgradeCost(string type)
    {
        int level = type switch
        {
            "Mass" => data.massLevel,
            "Strength" => data.strengthLevel,
            "Greed" => data.greedLevel,
            _ => 1
        };

        int baseCost = type switch
        {
            "Mass" => baseMassCost,
            "Strength" => baseStrengthCost,
            "Greed" => baseGreedCost,
            _ => 100
        };

        return baseCost * Math.Pow(costGrowthFactor, level);
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

    // --- HIGHSCORE TRACKING ---

    public void UpdateBestDistance(float distance)
    {
        // Only save if it's a new record
        if (distance > data.bestDistance)
        {
            data.bestDistance = distance;
            Save();
        }
    }
}