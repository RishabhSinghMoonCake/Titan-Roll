using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


public class GameLevelManager : MonoBehaviour
{
    public enum GameState { Idle, Launched }
    public GameState currentState = GameState.Idle;

    public static GameLevelManager Instance;

    [Header("Core References")]
    public ArcadeBoulder arcadeBoulder;
    public TextMeshProUGUI goldText;

    [Header("Sub-Managers")]
    [Tooltip("Reference to the new manager handling the character skins")]
    public CharacterSkinManager characterSkinManager;

    [Header("Boulder Visuals & Setup")]
    public Transform visualHolder;       // Empty child object inside ArcadeBoulder
    public Transform launchPadAnchor;    // Where the boulder rests
    public GameObject[] skinPrefabs;     // Array of your ball skins

    [Header("Boulder Math Settings")]
    public float scalePerLevel = 0.05f;  // 5% size increase per level
    public float massAtLevel40 = 1000f;     // Added physical weight per level
    public float baseColliderRadius = 0.5f;
    public float baseLaunchSpeed = 140f;
    public float baseMass = 50f; // Base mass for level 1 (can be used in calculations or just as a reference)
    public float speedPerStrengthLevel = 14f;
    [Header("Progression Curves")]
    [Tooltip("Draw how fast the boulder gets from Lv 1 to Lv 40")]
    public AnimationCurve speedCurve;
    [Tooltip("Draw how much stamina you get from Lv 1 to Lv 40")]
    public AnimationCurve staminaCurve;

    [Header("Camera")]
    public DynamicBoulderCamera dynamicCamera;

    [Header("UI System")]
    public UpgradeCardUI[] upgradeCards; // Drag all 3 cards here

    [Header("Optimization & Debug")]
    public TMPro.TextMeshProUGUI fpsText;
    private float _deltaTime = 0.0f;


    private GameObject _currentSkinInstance;
    private int _currentSkinIndex = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        currentState = GameState.Idle;

        // Note: We REMOVED the OnLaunchTap subscription from here. 
        // LaunchSequenceManager handles the initial taps and minigame now.

        if (arcadeBoulder != null)
            arcadeBoulder.OnRunFinished += StartEndRunSequence;

        UpdateBoulderVisualsAndStats();
        UpdateUI();
    }

    private void Update()
    {
        // Only allow steering if the LaunchSequenceManager told us the slap finished
        if (currentState == GameState.Launched)
        {
            arcadeBoulder.Steer(InputManager.Instance.SteeringInput);
        }


        // --- SMOOTHED FPS METER ---
        if (fpsText != null)
        {
            // Smooth out the time between frames
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;

            // Only rebuild the UI text every 15 frames to save performance!
            if (Time.frameCount % 15 == 0)
            {
                float fps = 1.0f / _deltaTime;
                fpsText.text = $"FPS: {Mathf.Ceil(fps)}";

                // Optional: Change color if dropping frames
                if (fps >= 50f) fpsText.color = Color.green;
                else if (fps >= 30f) fpsText.color = Color.yellow;
                else fpsText.color = Color.red;
            }
        }
    }

    // --- CALLED BY LAUNCH SEQUENCE MANAGER ---
    public void SetStateToLaunched()
    {
        currentState = GameState.Launched;
    }

    // --- UNIFIED BOULDER UPDATE ---

    private void UpdateBoulderVisualsAndStats()
    {
        int massLevel = PlayerDataManager.Instance.data.massLevel;

        // 1. Calculate Size
        float currentScale = 1.0f + ((massLevel - 1) * scalePerLevel);
        arcadeBoulder.transform.localScale = Vector3.one * currentScale;

        // 2. Update Physical Mass
        Rigidbody rb = arcadeBoulder.GetComponent<Rigidbody>();
        float newMass = Mathf.Lerp(baseMass,massAtLevel40, (massLevel - 1) / 39f);
        if (rb != null) rb.mass = newMass;
        if (InputManager.Instance != null) InputManager.Instance.currentBoulderMass = newMass;

        // 3. Anchor Position
        if (launchPadAnchor != null)
        {
            float currentRadius = baseColliderRadius * currentScale;
            Vector3 anchorPos = launchPadAnchor.position;
            arcadeBoulder.transform.position = new Vector3(anchorPos.x, anchorPos.y + currentRadius, anchorPos.z);
        }

        // 4. Skin Changing Logic
        int requiredSkinIndex = (massLevel - 1) / 5;
        requiredSkinIndex = Mathf.Clamp(requiredSkinIndex, 0, skinPrefabs.Length - 1);

        if (_currentSkinIndex != requiredSkinIndex || _currentSkinInstance == null)
        {
            if (_currentSkinInstance != null) Destroy(_currentSkinInstance);

            _currentSkinInstance = Instantiate(skinPrefabs[requiredSkinIndex] ? skinPrefabs[requiredSkinIndex] : skinPrefabs[0], visualHolder);
            _currentSkinInstance.transform.localPosition = Vector3.zero;
            _currentSkinInstance.transform.localRotation = Quaternion.identity;
            _currentSkinInstance.transform.localScale = Vector3.one;

            _currentSkinIndex = requiredSkinIndex;
        }

        // 5. Update Character Skin & Hand Socket
        if (characterSkinManager != null)
        {
            // Equip skin 0 (Can be tied to PlayerData later!)
            characterSkinManager.EquipCharacterSkin(0);
        }

        // 6. Update Camera Distance
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
        // 1. Update the main gold text
        if (goldText != null)
            goldText.text = FormatMoney(PlayerDataManager.Instance.data.gold);

        // 2. Tell every upgrade card to re-calculate its logic
        foreach (UpgradeCardUI card in upgradeCards)
        {
            if (card != null) card.RefreshCardUI();
        }
    }

    public string FormatMoney(double amount)
    {
        if (amount >= 1000000000) return (amount / 1000000000D).ToString("0.##") + "B";
        if (amount >= 1000000) return (amount / 1000000D).ToString("0.##") + "M";
        if (amount >= 1000) return (amount / 1000D).ToString("0.##") + "K";
        return amount.ToString("N0");
    }

    // --- LAUNCH CALCULATION ---

    // --- NEW STAMINA & SPEED MATH ---

    public float GetTotalLaunchSpeed()
    {
        int strengthLvl = PlayerDataManager.Instance.data.strengthLevel;
        int benchmark = EconomyManager.Instance.benchmarkStrength; // Reference the benchmark!
        
        // Let 't' go past 1.0 if the player over-levels!
        float t = (strengthLvl - 1) / (float)(benchmark - 1);

        float evaluatedCurve = speedCurve.Evaluate(t); 

        // Use LerpUnclamped so a Level 30 player actually gets > 150 km/h
        return Mathf.LerpUnclamped(60f, 450f, evaluatedCurve); 
    }

    public float GetLaunchStamina()
    {
        int strengthLvl = PlayerDataManager.Instance.data.strengthLevel;
        int benchmark = EconomyManager.Instance.benchmarkStrength;
        
        float t = (strengthLvl - 1) / (float)(benchmark - 1);

        float evaluatedCurve = staminaCurve.Evaluate(t);

        // Use LerpUnclamped so a Level 30 player actually gets > 80 seconds
        return Mathf.LerpUnclamped(5f, 80f, evaluatedCurve);
    }

    // --- END RUN LOGIC ---

    private void StartEndRunSequence() => StartCoroutine(EndRunRoutine());

    private IEnumerator EndRunRoutine()
    {
        currentState = GameState.Idle;

        float finalDist = arcadeBoulder.transform.position.z;

        // The RewardManager now handles the Distance Tiers AND the Smash Gold!
        int goldEarned = RewardManager.Instance.FinalizeRunRewards(finalDist);

        // Optional: Update your UI here to show "Total Earned: [goldEarned]" before restarting
        yield return new WaitForSeconds(2.5f);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        // Removed InputManager un-subscribing since it's gone from this script
        if (arcadeBoulder != null) arcadeBoulder.OnRunFinished -= StartEndRunSequence;
    }
}