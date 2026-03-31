using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public struct GoldTier
{
    public float endDistance;
    public float goldPerMeter;
}

public class GameLevelManager : MonoBehaviour
{
    public enum GameState { Idle, Launched }
    public GameState currentState = GameState.Idle;

    [Header("Core References")]
    public ArcadeBoulder arcadeBoulder;
    public TextMeshProUGUI goldText;

    [Header("Boulder Visuals & Setup")]
    public Transform visualHolder;       // Empty child object inside ArcadeBoulder
    public Transform launchPadAnchor;    // Where the boulder rests
    public GameObject[] skinPrefabs;     // Array of your ball skins

    [Header("Boulder Math Settings")]
    public float scalePerLevel = 0.05f;  // 5% size increase per level
    public float massPerLevel = 10f;     // Added physical weight per level
    public float baseColliderRadius = 0.5f;
    public float baseLaunchSpeed = 140f;
    public float speedPerStrengthLevel = 14f;

    [Header("Economy Configuration")]
    public List<GoldTier> distanceTiers;

    [Header("Camera")]
    public DynamicBoulderCamera dynamicCamera;

    private GameObject _currentSkinInstance;
    private int _currentSkinIndex = -1;

    public static GameLevelManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        currentState = GameState.Idle;

        if (InputManager.Instance != null)
            InputManager.Instance.OnLaunchTap += HandleLaunchTap;

        if (arcadeBoulder != null)
            arcadeBoulder.OnRunFinished += StartEndRunSequence;

        UpdateBoulderVisualsAndStats();
        UpdateUI();
    }

    private void Update()
    {
        if (currentState == GameState.Launched)
        {
            arcadeBoulder.Steer(InputManager.Instance.SteeringInput);
        }
    }

    // --- UNIFIED BOULDER UPDATE ---

    private void UpdateBoulderVisualsAndStats()
    {
        int massLevel = PlayerDataManager.Instance.data.massLevel;

        // 1. Calculate Size (e.g., Level 1 = 1.0, Level 2 = 1.05, Level 10 = 1.45)
        float currentScale = 1.0f + ((massLevel - 1) * scalePerLevel);
        arcadeBoulder.transform.localScale = Vector3.one * currentScale;

        // 2. Update Physical Mass (makes it hit harder/roll heavier)
        Rigidbody rb = arcadeBoulder.GetComponent<Rigidbody>();
        float newMass = 100f + ((massLevel - 1) * massPerLevel); // Base mass of 100 + added mass per level
        if (rb != null) rb.mass = newMass;
        if (InputManager.Instance != null) InputManager.Instance.currentBoulderMass = newMass;

        // 3. Anchor Position (prevents ground clipping as it grows)
        if (launchPadAnchor != null)
        {
            float currentRadius = baseColliderRadius * currentScale;
            Vector3 anchorPos = launchPadAnchor.position;
            arcadeBoulder.transform.position = new Vector3(anchorPos.x, anchorPos.y + currentRadius, anchorPos.z);
        }

        // 4. Skin Changing Logic (Changes every 5 levels)
        // Level 1-4 = Index 0 | Level 5-9 = Index 1 | Level 10-14 = Index 2, etc.
        int requiredSkinIndex = (massLevel - 1) / 5;

        // Cap the index so we don't crash if they outlevel your available skins
        requiredSkinIndex = Mathf.Clamp(requiredSkinIndex, 0, skinPrefabs.Length - 1);

        if (_currentSkinIndex != requiredSkinIndex || _currentSkinInstance == null)
        {
            if (_currentSkinInstance != null) Destroy(_currentSkinInstance);

            _currentSkinInstance = Instantiate(skinPrefabs[requiredSkinIndex] ? skinPrefabs[requiredSkinIndex]: skinPrefabs[0], visualHolder);
            _currentSkinInstance.transform.localPosition = Vector3.zero;
            _currentSkinInstance.transform.localRotation = Quaternion.identity;
            _currentSkinInstance.transform.localScale = Vector3.one;

            _currentSkinIndex = requiredSkinIndex;
        }

        // 5. Update Camera Distance
        if (dynamicCamera != null)
        {
            dynamicCamera.UpdateCameraDistance(currentScale);
        }
    }

    // --- UI BUTTON CLICKS ---

    public void BuyMassUpgrade()
    {
        if (PlayerDataManager.Instance.TryBuyUpgrade("Mass"))
        {
            UpdateBoulderVisualsAndStats(); // Instantly apply size/mass/skin
            UpdateUI();
        }
    }

    public void BuyStrengthUpgrade()
    {
        if (PlayerDataManager.Instance.TryBuyUpgrade("Strength")) UpdateUI();
    }

    public void BuyGreedUpgrade()
    {
        if (PlayerDataManager.Instance.TryBuyUpgrade("Greed")) UpdateUI();
    }

    public void UpdateUI()
    {
        if (goldText != null)
            goldText.text = "Gold: " + PlayerDataManager.Instance.data.gold.ToString("N0");
    }

    // --- LAUNCH & GAMEPLAY ---

    private void HandleLaunchTap()
    {
        if (currentState == GameState.Idle) FireBoulder();
    }

    private void FireBoulder()
    {
        currentState = GameState.Launched;
        
        float launchSpeed = GetTotalLaunchSpeed();

        arcadeBoulder.Launch(launchSpeed);

        if (dynamicCamera != null) dynamicCamera.TriggerLaunchSequence();
    }

    public float GetTotalLaunchSpeed()
    {
        int strengthLvl = PlayerDataManager.Instance.data.strengthLevel;
        return baseLaunchSpeed + ((strengthLvl - 1) * speedPerStrengthLevel);
    }

    // --- END RUN LOGIC ---

    private void StartEndRunSequence() => StartCoroutine(EndRunRoutine());

    private IEnumerator EndRunRoutine()
    {
        currentState = GameState.Idle;

        float finalDist = arcadeBoulder.transform.position.z;
        float greedMult = 1.0f + ((PlayerDataManager.Instance.data.greedLevel - 1) * 0.05f);

        // Calculate Gold
        float remainingDist = finalDist;
        float accumulatedGold = 0f;
        float previousTierEnd = 0f;

        foreach (GoldTier tier in distanceTiers)
        {
            if (remainingDist <= 0) break;
            float tierLength = tier.endDistance - previousTierEnd;
            float distInTier = Mathf.Min(remainingDist, tierLength);
            accumulatedGold += distInTier * tier.goldPerMeter;
            remainingDist -= distInTier;
            previousTierEnd = tier.endDistance;
        }

        int goldEarned = Mathf.FloorToInt(accumulatedGold * greedMult);

        yield return new WaitForSeconds(2.5f);

        PlayerDataManager.Instance.AddGold(goldEarned);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null) InputManager.Instance.OnLaunchTap -= HandleLaunchTap;
        if (arcadeBoulder != null) arcadeBoulder.OnRunFinished -= StartEndRunSequence;
    }
}