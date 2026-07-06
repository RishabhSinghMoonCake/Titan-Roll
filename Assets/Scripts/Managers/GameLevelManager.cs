using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
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
    public CharacterSkinManager characterSkinManager;

    [Header("Boulder Visuals & Setup")]
    public Transform visualHolder;
    public Transform launchPadAnchor;
    public GameObject[] skinPrefabs;

    [Header("Boulder Particle References")]
    [Tooltip("Assign the Particle System component that lives directly on each corresponding boulder prefab")]
    public ParticleSystem[] boulderParticles;

    [Header("Boulder Math Settings")]
    public float scalePerLevel = 0.05f;
    public float massAtLevel40 = 1000f;
    public float baseColliderRadius = 0.5f;
    public float baseLaunchSpeed = 140f;
    public float baseMass = 50f;
    public float speedPerStrengthLevel = 14f;

    [Header("Progression Curves")]
    public AnimationCurve speedCurve;
    public AnimationCurve staminaCurve;

    [Header("Camera")]
    public DynamicBoulderCamera dynamicCamera;

    [Header("UI System")]
    public UpgradeCardUI[] upgradeCards;

    [Header("Optimization & Debug")]
    public TMPro.TextMeshProUGUI fpsText;
    private float _deltaTime = 0.0f;

    private GameObject _currentSkinInstance;
    private int _currentSkinIndex = -1;

    [Header("Upgrade Cutscene Settings")]
    public LaunchSequenceManager launchSequenceManager;
    public float cameraPanDuration = 1.5f;
    public float dramaticPauseDuration = 1.2f;
    public TextMeshProUGUI upgradeAnnouncementText;
    [Tooltip("Set custom messages. Index 1 = Level 6 Upgrade, Index 2 = Level 11, etc.")]
    public string[] boulderUpgradeMessages;

    [Tooltip("Set custom messages. Index 1 = Level 6 Upgrade, Index 2 = Level 11, etc.")]
    public string[] weaponUpgradeMessages;

    private bool _isUpgradingCutscene = false;

    [Header("Dynamic Camera Framers")]
    public CinemachineDynamicScaler boulderZoomFramer;

    [Header("Reward Sequence Sequence")]
    public GameObject coin3DPrefab;
    public RectTransform goldTextTarget;
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTallyText; // Show the gold earned THIS run
    public int visualCoinsToSpawn = 20;

    [Header("Game Over UI Text")]
    public TextMeshProUGUI currentRunText;
    public TextMeshProUGUI bestRunText;
    public TextMeshProUGUI totalGoldText;
    public UnityEngine.UI.Button continueButton;

    // These variables hold data between the run ending and the continue button being pressed
    private List<GameObject> _activeRewardCoins = new List<GameObject>();
    private float _lastRunDistance;


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

        if (arcadeBoulder != null)
            arcadeBoulder.OnRunFinished += StartEndRunSequence;

        // NEW: Initialize the static boulder target
        if (boulderZoomFramer != null && arcadeBoulder != null)
        {
            boulderZoomFramer.SetTarget(arcadeBoulder.transform);
        }

        int currentLevel = PlayerPrefs.GetInt("PrestigeLevel", 1);
        if (launchSequenceManager != null && launchSequenceManager.LevelText != null)
        {
            launchSequenceManager.LevelText.text = $"LEVEL {currentLevel}";
        }



        UpdateBoulderVisualsAndStats();
        UpdateUI();
    }

    private void Update()
    {
        if (currentState == GameState.Launched)
        {
            arcadeBoulder.Steer(InputManager.Instance.SteeringInput);
        }

        if (fpsText != null)
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            if (Time.frameCount % 15 == 0)
            {
                float fps = 1.0f / _deltaTime;
                fpsText.text = $"FPS: {Mathf.Ceil(fps)}";
                if (fps >= 50f) fpsText.color = Color.green;
                else if (fps >= 30f) fpsText.color = Color.yellow;
                else fpsText.color = Color.red;
            }
        }
    }

    public void SetStateToLaunched() => currentState = GameState.Launched;

    public void BuyMassUpgrade()
    {
        if (_isUpgradingCutscene) return;

        if (PlayerDataManager.Instance.TryBuyUpgrade("Mass"))
        {
            int massLevel = PlayerDataManager.Instance.data.massLevel;

            // Trigger on levels 6, 11, 16, etc. (Modulo 5 + 1)
            if (massLevel > 1 && massLevel % 5 == 1)
            {
                StartCoroutine(MassMilestoneCutsceneRoutine());
            }
            else
            {
                UpdateBoulderVisualsAndStats();
                UpdateUI();
            }
        }
    }

    // ==========================================
    // UI ANIMATIONS
    // ==========================================
    private void ShowUpgradeAnnouncement(string message)
    {
        if (upgradeAnnouncementText == null) return;

        upgradeAnnouncementText.gameObject.SetActive(true);
        upgradeAnnouncementText.text = message;

        // Reset state instantly before animating
        upgradeAnnouncementText.DOKill();
        upgradeAnnouncementText.transform.DOKill();
        upgradeAnnouncementText.color = new Color(upgradeAnnouncementText.color.r, upgradeAnnouncementText.color.g, upgradeAnnouncementText.color.b, 0f);
        upgradeAnnouncementText.transform.localScale = Vector3.one * 0.5f;

        // Build the pop & fade sequence
        Sequence seq = DOTween.Sequence();

        // 1. Pop In
        seq.Append(upgradeAnnouncementText.DOFade(1f, 0.4f));
        seq.Join(upgradeAnnouncementText.transform.DOScale(1.2f, 0.4f).SetEase(Ease.OutBack));

        // 2. Settle to normal size
        seq.Append(upgradeAnnouncementText.transform.DOScale(1f, 0.2f));

        // 3. Stay on screen during the dramatic pause
        seq.AppendInterval(dramaticPauseDuration - 0.2f);

        // 4. Pop out and fade away
        seq.Append(upgradeAnnouncementText.transform.DOScale(0.5f, 0.3f).SetEase(Ease.InBack));
        seq.Join(upgradeAnnouncementText.DOFade(0f, 0.3f));

        // 5. Disable the object when finished
        seq.OnComplete(() => upgradeAnnouncementText.gameObject.SetActive(false));
    }

    public void BuyStrengthUpgrade()
    {
        if (_isUpgradingCutscene) return;

        if (PlayerDataManager.Instance.TryBuyUpgrade("Strength"))
        {
            int strengthLevel = PlayerDataManager.Instance.data.strengthLevel;

            // Trigger on levels 6, 11, 16, etc. (Modulo 5 + 1)
            if (strengthLevel > 1 && strengthLevel % 5 == 1)
            {
                StartCoroutine(StrengthMilestoneCutsceneRoutine());
            }
            else
            {
                UpdateBoulderVisualsAndStats();
                UpdateUI();
            }
        }
    }

    // --- NEW: Handles ONLY the Boulder / Mass ---
    private IEnumerator MassMilestoneCutsceneRoutine()
    {
        _isUpgradingCutscene = true;

        HideCutsceneUI();

        UpdateBoulderStatsWithoutRecreatingCharacter();

        // 1. Pan camera ONLY to the boulder
        if (launchSequenceManager != null && arcadeBoulder != null)
        {
            yield return StartCoroutine(launchSequenceManager.PanToTarget(launchSequenceManager.vcamBoulderCloseUp, arcadeBoulder.transform, cameraPanDuration));
        }

        // 2. Swap out the boulder skin 
        SwapBoulderSkinOnly();

        // 3. Fire off the boulder upgrade particle effect
        int massLevel = PlayerDataManager.Instance.data.massLevel;
        int requiredIndex = (massLevel - 1) / 5;
        int boulderIdx = Mathf.Clamp(requiredIndex, 0, boulderParticles.Length - 1);

        if (boulderParticles != null && boulderParticles.Length > boulderIdx && boulderParticles[boulderIdx] != null)
        {
            boulderParticles[boulderIdx].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            boulderParticles[boulderIdx].Play(true);
        }
        string message = "BOULDER UPGRADED!"; // Fallback text
        if (boulderUpgradeMessages != null && requiredIndex < boulderUpgradeMessages.Length)
        {
            // Use the text assigned in the inspector for this specific tier
            message = boulderUpgradeMessages[requiredIndex];
        }
        ShowUpgradeAnnouncement(message);
        yield return new WaitForSeconds(dramaticPauseDuration);

        // 4. Reset camera
        if (launchSequenceManager != null) launchSequenceManager.ResetToIdleCamera();

        UpdateUI();

        RestoreCutsceneUI();
        _isUpgradingCutscene = false;
    }

    private IEnumerator StrengthMilestoneCutsceneRoutine()
    {
        _isUpgradingCutscene = true;
        HideCutsceneUI();
        // 1. Pan camera to the stable ANCHOR on the ground instead of the temporary weapon
        if (launchSequenceManager != null && characterSkinManager != null)
        {
            Transform camTarget = characterSkinManager.weaponRestAnchor != null
                ? characterSkinManager.weaponRestAnchor
                : characterSkinManager.giantWeaponInScene;

            yield return StartCoroutine(launchSequenceManager.PanToTarget(
                launchSequenceManager.vcamHandCloseUp,
                camTarget,
                cameraPanDuration));
        }

        // 2. Perform the actual asset prefab swap
        int strengthLevel = PlayerDataManager.Instance.data.strengthLevel;
        int requiredIndex = (strengthLevel - 1) / 5;
        if (characterSkinManager != null)
        {
            characterSkinManager.EquipWeaponSkin(requiredIndex);
        }

        // 3. Play the weapon upgrade particle effect dynamically
        if (characterSkinManager != null) characterSkinManager.PlayActiveWeaponParticle();

        string message = "WEAPON UPGRADED!"; // Fallback text
        if (weaponUpgradeMessages != null && requiredIndex < weaponUpgradeMessages.Length)
        {
            // Use the text assigned in the inspector for this specific tier
            message = weaponUpgradeMessages[requiredIndex];
        }
        ShowUpgradeAnnouncement(message);

        yield return new WaitForSeconds(dramaticPauseDuration);

        // 4. Reset camera
        if (launchSequenceManager != null) launchSequenceManager.ResetToIdleCamera();

        UpdateUI();
        RestoreCutsceneUI();
        _isUpgradingCutscene = false;
    }

    private void UpdateBoulderStatsWithoutRecreatingCharacter()
    {
        int massLevel = PlayerDataManager.Instance.data.massLevel;

        float currentScale = 1.0f + ((massLevel - 1) * scalePerLevel);
        arcadeBoulder.transform.localScale = Vector3.one * currentScale;

        Rigidbody rb = arcadeBoulder.GetComponent<Rigidbody>();
        float newMass = Mathf.Lerp(baseMass, massAtLevel40, (massLevel - 1) / 39f);
        if (rb != null) rb.mass = newMass;
        if (InputManager.Instance != null) InputManager.Instance.currentBoulderMass = newMass;

        if (launchPadAnchor != null)
        {
            float currentRadius = baseColliderRadius * currentScale;
            Vector3 anchorPos = launchPadAnchor.position;
            arcadeBoulder.transform.position = new Vector3(anchorPos.x, anchorPos.y + currentRadius, anchorPos.z);
        }

        if (dynamicCamera != null) dynamicCamera.UpdateCameraDistance(currentScale);
        arcadeBoulder.ApplyUpgrades(massLevel);
    }

    private void SwapBoulderSkinOnly()
    {
        int massLevel = PlayerDataManager.Instance.data.massLevel;
        int requiredIndex = (massLevel - 1) / 5;
        int boulderIndex = Mathf.Clamp(requiredIndex, 0, skinPrefabs.Length - 1);

        if (_currentSkinIndex != boulderIndex || _currentSkinInstance == null)
        {
            if (_currentSkinInstance != null) Destroy(_currentSkinInstance);

            _currentSkinInstance = Instantiate(skinPrefabs[boulderIndex] ? skinPrefabs[boulderIndex] : skinPrefabs[0], visualHolder);
            _currentSkinInstance.transform.localPosition = Vector3.zero;
            _currentSkinInstance.transform.localRotation = Quaternion.identity;
            _currentSkinInstance.transform.localScale = Vector3.one;

            _currentSkinIndex = boulderIndex;
        }
    }

    private void UpdateBoulderVisualsAndStats()
    {
        UpdateBoulderStatsWithoutRecreatingCharacter();
        if (characterSkinManager != null) characterSkinManager.EquipCharacterSkin(0);
        SwapBoulderSkinOnly();

        if (characterSkinManager != null)
        {
            // FIX: Tie the weapon skin strictly to the Strength Level!
            int requiredIndex = (PlayerDataManager.Instance.data.strengthLevel - 1) / 5;
            characterSkinManager.EquipWeaponSkin(requiredIndex);
        }
    }

    public void BuyGreedUpgrade()
    {
        if (PlayerDataManager.Instance.TryBuyUpgrade("Greed")) UpdateUI();
    }

    public void UpdateUI()
    {
        if (goldText != null)
            goldText.text = FormatMoney(PlayerDataManager.Instance.data.gold);

        foreach (UpgradeCardUI card in upgradeCards)
        {
            if (card != null) card.RefreshCardUI();
        }
    }

    private void HideCutsceneUI()
    {
        if (launchSequenceManager == null) return;

        // Shrink the upgrade panel
        if (launchSequenceManager.upgradePanel != null)
        {
            launchSequenceManager.upgradePanel.transform.DOKill();
            launchSequenceManager.upgradePanel.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
        }

        // Shrink the Tap to Play text
        if (launchSequenceManager.tapToPlayText != null)
        {
            launchSequenceManager.tapToPlayText.transform.DOKill();
            launchSequenceManager.tapToPlayText.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
        }
    }

    private void RestoreCutsceneUI()
    {
        if (launchSequenceManager == null) return;

        // Pop the upgrade panel back in
        if (launchSequenceManager.upgradePanel != null)
        {
            launchSequenceManager.upgradePanel.transform.DOKill();
            launchSequenceManager.upgradePanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }

        // Pop the Tap to Play text back in
        if (launchSequenceManager.tapToPlayText != null)
        {
            launchSequenceManager.tapToPlayText.transform.DOKill();
            launchSequenceManager.tapToPlayText.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
        }
    }

    public string FormatMoney(double amount)
    {
        if (amount >= 1000000000) return (amount / 1000000000D).ToString("0.##") + "B";
        if (amount >= 1000000) return (amount / 1000000D).ToString("0.##") + "M";
        if (amount >= 1000) return (amount / 1000D).ToString("0.##") + "K";
        return amount.ToString("N0");
    }

    public float GetTotalLaunchSpeed()
    {
        int strengthLvl = PlayerDataManager.Instance.data.strengthLevel;
        int benchmark = EconomyManager.Instance.benchmarkStrength;
        float t = (strengthLvl - 1) / (float)(benchmark - 1);
        return Mathf.LerpUnclamped(60f, 350f, speedCurve.Evaluate(t));
    }

    public float GetLaunchStamina()
    {
        int strengthLvl = PlayerDataManager.Instance.data.strengthLevel;
        int benchmark = EconomyManager.Instance.benchmarkStrength;
        float t = (strengthLvl - 1) / (float)(benchmark - 1);
        return Mathf.LerpUnclamped(4f, 16f, staminaCurve.Evaluate(t));
    }

    private void StartEndRunSequence() => StartCoroutine(EndRunRoutine());

    // ==========================================
    // REWARD & GAME OVER SEQUENCE
    // ==========================================

    private IEnumerator EndRunRoutine()
    {
        currentState = GameState.Idle;
        _lastRunDistance = arcadeBoulder.transform.position.z;
        Vector3 boulderPos = arcadeBoulder.transform.position;

        bool isNewRecord = _lastRunDistance > PlayerDataManager.Instance.data.bestDistance;

        // 1. Deflate the boulder smoothly
        if (arcadeBoulder.visualMesh != null)
        {
            arcadeBoulder.visualMesh.DOScale(Vector3.zero, 1f).SetEase(Ease.InBack);
        }
        else
        {
            arcadeBoulder.transform.DOScale(Vector3.zero, 1f).SetEase(Ease.InBack);
        }

        if (isNewRecord && HighScoreVisuals.Instance != null)
        {
            HighScoreVisuals.Instance.PlantNewRecordFlag(boulderPos);
        }

        yield return new WaitForSeconds(0.8f);

        // 2. Spawn the 3D Coin Fountain
        _activeRewardCoins.Clear();
        for (int i = 0; i < visualCoinsToSpawn; i++)
        {
            GameObject coin = Instantiate(coin3DPrefab, boulderPos, Quaternion.identity);
            _activeRewardCoins.Add(coin);

            Vector2 randomCircle = Random.insideUnitCircle * 3f;
            Vector3 targetPos = boulderPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

            coin.transform.DOJump(targetPos, jumpPower: Random.Range(3f, 6f), numJumps: 1, duration: 0.6f).SetEase(Ease.OutQuad);
            coin.transform.DORotate(new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360)), 0.6f, RotateMode.FastBeyond360);

            yield return new WaitForSeconds(0.02f);
        }

        yield return new WaitForSeconds(0.5f);

        // 3. Show Panel & Populate Data
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (continueButton != null) continueButton.interactable = false;

        // --- SAVE THE BEST DISTANCE BEFORE UI UPDATE ---
        PlayerDataManager.Instance.UpdateBestDistance(_lastRunDistance);

        // Populate distance texts
        if (currentRunText != null) currentRunText.text = $"{Mathf.FloorToInt(_lastRunDistance)}m";
        if (bestRunText != null) bestRunText.text = $"Best : {Mathf.FloorToInt(PlayerDataManager.Instance.data.bestDistance)}m";

        // Populate total gold (Gold BEFORE the run ended)
        if (totalGoldText != null) totalGoldText.text = FormatMoney(PlayerDataManager.Instance.data.gold);

        // --- CALCULATE ACTUAL GOLD EARNED FOR UI TALLY ---
        int actualGoldEarned = RewardManager.Instance.CalculateRunRewards(_lastRunDistance);
        int displayGold = 0;

        if (gameOverTallyText != null)
        {
            DOTween.To(() => displayGold, x =>
            {
                displayGold = x;
                gameOverTallyText.text = "+" + FormatMoney(displayGold);
            }, actualGoldEarned, 1.5f).SetEase(Ease.OutExpo);
        }

        yield return new WaitForSeconds(1.5f);

        // 4. Enable Continue Button
        if (continueButton != null) continueButton.interactable = true;
    }

    // Link this method to your UI Button's OnClick event in the Inspector!
    public void OnContinuePressed()
    {
        if (continueButton != null) continueButton.interactable = false; // Prevent double-clicks
        StartCoroutine(FinalizeAndReloadRoutine());
    }

    private IEnumerator FinalizeAndReloadRoutine()
    {
        Camera mainCam = Camera.main;

        // 1. Fly 3D coins directly to the 2D UI Text
        foreach (var coin in _activeRewardCoins)
        {
            if (mainCam != null && coin != null)
            {
                // Convert screen target to world space for the 3D coin
                Vector3 screenTarget = goldTextTarget.position;
                screenTarget.z = 5f;
                Vector3 worldTarget = mainCam.ScreenToWorldPoint(screenTarget);

                coin.transform.DOMove(worldTarget, 0.6f).SetEase(Ease.InBack);
            }

            if (coin != null) coin.transform.DOScale(Vector3.zero, 0.6f).SetEase(Ease.InBack);
            yield return new WaitForSeconds(0.02f);
        }

        yield return new WaitForSeconds(0.6f);

        // 2. Finalize rewards & punch the UI text to show it registered
        RewardManager.Instance.FinalizeRunRewards(_lastRunDistance);
        UpdateUI();

        if (goldTextTarget != null)
        {
            goldTextTarget.DOPunchScale(new Vector3(0.3f, 0.3f, 0.3f), 0.3f, 5);
        }

        // Cleanup the 3D coins
        foreach (var coin in _activeRewardCoins)
        {
            if (coin != null) Destroy(coin);
        }
        _activeRewardCoins.Clear();

        // 3. Reset the scene
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TriggerPrestigeWin()
    {
        StartCoroutine(PrestigeWinRoutine());
    }

    private IEnumerator PrestigeWinRoutine()
    {
        currentState = GameState.Idle;
        int currentLevel = PlayerPrefs.GetInt("PrestigeLevel", 1);

        // --- 1. THE CINEMATIC CAMERA TRICK ---
        // Find your LaunchSequenceManager to access the Cinemachine cameras
        LaunchSequenceManager launchManager = FindObjectOfType<LaunchSequenceManager>();

        if (launchManager != null && launchManager.vcamFollow != null)
        {
            // Remove the Follow target so the camera completely freezes its position in the world
            launchManager.vcamFollow.Follow = null;

            // Ensure LookAt is still pointing at the boulder so the camera swivels to watch it leave
            if (arcadeBoulder != null)
            {
                launchManager.vcamFollow.LookAt = arcadeBoulder.transform;
            }
        }

        // If you are also using a custom script like DynamicBoulderCamera to handle movement, disable it here
        DynamicBoulderCamera customCam = FindObjectOfType<DynamicBoulderCamera>();
        if (customCam != null) customCam.enabled = false;

        // 2. Let the player watch the boulder roll away for 3.5 seconds
        // 1. Let the player watch the boulder roll away for 3.5 seconds
        yield return new WaitForSeconds(3.5f);

        // 2. TRIGGER YOUR EXISTING NORMAL GAME OVER SYSTEM!
        // This will fire your built-in cleanup: hiding the ball, fading out the distance/speed HUD, and opening the Game Over panel.
        StartEndRunSequence(); // (Or whatever your normal game over method is named in GameLevelManager)

        // 3. Calculate Prestige Data & Wipe Save
        int nextLevel = currentLevel + 1;
        PlayerPrefs.SetInt("PrestigeLevel", nextLevel);
        PlayerPrefs.Save();

        int startingGoldBonus = 5000 * currentLevel;

        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.data.gold = startingGoldBonus;
            PlayerDataManager.Instance.data.massLevel = 1;
            PlayerDataManager.Instance.data.strengthLevel = 1;
            PlayerDataManager.Instance.data.bestDistance = 0;
            PlayerDataManager.Instance.Save();
        }

        // 4. OVERRIDE THE TEXT LABELS
        // Since your normal Game Over just opened the panel, we simply override the text to show the Prestige rewards instead!
        if (gameOverTallyText != null) gameOverTallyText.text = "PRESTIGE RANK UP!";
        if (currentRunText != null) currentRunText.text = $"LEVEL {currentLevel} CLEARED!";
        if (bestRunText != null) bestRunText.text = $"Next Level: {nextLevel}";
        if (totalGoldText != null) totalGoldText.text = $"Bonus: +{startingGoldBonus}";
    }

    private void OnDestroy()
    {
        if (arcadeBoulder != null) arcadeBoulder.OnRunFinished -= StartEndRunSequence;
    }
}