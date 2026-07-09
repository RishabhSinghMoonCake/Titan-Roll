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

    [Header("Coin Fountain Tuning")]
    [Tooltip("Time delay between each coin shooting out of the boulder.")]
    public float coinSpawnDelay = 0.04f;
    [Tooltip("How hard the coins shoot up into the air.")]
    public float coinUpwardForce = 10f;
    [Tooltip("How wide the scatter cone is.")]
    public float coinSpreadRadius = 4.5f;
    [Tooltip("Random variation applied to the force so they don't look perfectly uniform.")]
    [Range(0f, 1f)] public float coinRandomness = 0.3f;

    [Header("Coin UI Flight Tuning")]
    [Tooltip("How small the coins shrink when they reach the top right UI. (e.g., 0.2 = 20% of original size)")]
    [Range(0.05f, 1f)] public float uiCoinScaleMultiplier = 0.2f;
    [Tooltip("How long it takes the coins to fly across the screen.")]
    public float coinFlightDuration = 0.85f;
    [Tooltip("Delay between each coin launching towards the UI.")]
    public float coinFlightStagger = 0.04f;

    [Header("Game Over UI Text")]
    public TextMeshProUGUI currentRunText;
    public TextMeshProUGUI bestRunText;
    public TextMeshProUGUI totalGoldText;
    public UnityEngine.UI.Button continueButton;
    [Tooltip("UI Badge or Text that says 'NEW REWARD!' or 'NEW RECORD!' when previous best is beaten.")]
    public GameObject newRecordBadge; // <-- NEW: The celebration badge!

    [Header("Level Cleared (Prestige) Dedicated UI")]
    public GameObject levelClearedPanel;       // <-- NEW: Completely separate from Game Over!
    public TextMeshProUGUI levelClearedTitleText;
    public TextMeshProUGUI levelClearedBonusText;
    public TextMeshProUGUI nextLevelPreviewText;
    public UnityEngine.UI.Button prestigeContinueButton;

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

        // --- PURE MATH MASS SCALING ---
        // Formula: Mass = BaseMass + [MaxDelta * (Level / 39)^1.5]
        // Provides slow early weight gain (50 -> 60 -> 72 -> 87kg) that ramps up to 865kg!
        float t = Mathf.Max(0f, (float)(massLevel - 1) / 39f);
        float newMass = baseMass + ((massAtLevel40 - baseMass) * Mathf.Pow(t, 1.5f));

        if (rb != null) rb.mass = newMass;
        if (InputManager.Instance != null) InputManager.Instance.currentBoulderMass = newMass;

        if (launchPadAnchor != null)
        {
            float currentRadius = baseColliderRadius * currentScale;
            Vector3 anchorPos = launchPadAnchor.position;
            arcadeBoulder.transform.position = new Vector3(anchorPos.x, anchorPos.y + currentRadius, anchorPos.z);
        }

        if (dynamicCamera != null) dynamicCamera.UpdateCameraDistance(currentScale);

        if (launchSequenceManager != null) launchSequenceManager.UpdateCameraScales(currentScale);
        arcadeBoulder.ApplyUpgrades(massLevel);

        if (arcadeBoulder != null)
        {
            arcadeBoulder.UpdateTrailWidth(currentScale);
        }
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
        int strengthLvl = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.data.strengthLevel : 1;
        int benchmark = EconomyManager.Instance != null ? EconomyManager.Instance.benchmarkStrength : 25;

        // 1. Normalize progress against the benchmark (0.0 at Level 1, 1.0 at Benchmark Level)
        float t = Mathf.Max(0f, (float)(strengthLvl - 1) / Mathf.Max(1f, (float)(benchmark - 1)));

        // 2. Diminishing Returns Power Curve (t^0.75)
        // This gives punchy, noticeable speed boosts in early levels while maintaining steady endgame growth!
        float mathCurve = Mathf.Pow(t, 0.75f);

        // 3. Scale between baseLaunchSpeed (~140 km/h) and benchmark target (~350 km/h)
        // LerpUnclamped ensures that when players progress past Level 25, speed continues growing infinitely!
        return Mathf.LerpUnclamped(baseLaunchSpeed, 350f, mathCurve);
    }

    public float GetLaunchStamina()
    {
        int strengthLvl = PlayerDataManager.Instance != null ? PlayerDataManager.Instance.data.strengthLevel : 1;
        int benchmark = EconomyManager.Instance != null ? EconomyManager.Instance.benchmarkStrength : 25;

        float t = Mathf.Max(0f, (float)(strengthLvl - 1) / (benchmark - 1));
        float mathCurve = Mathf.Pow(t, 0.7f);

        return 4f + ((16f - 4f) * mathCurve);
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

        if (arcadeBoulder.visualMesh != null)
            arcadeBoulder.visualMesh.DOScale(Vector3.zero, 1f).SetEase(Ease.InBack);
        else
            arcadeBoulder.transform.DOScale(Vector3.zero, 1f).SetEase(Ease.InBack);

        if (HighScoreVisuals.Instance != null)
        {
            HighScoreVisuals.Instance.PlantNewRecordFlag(boulderPos);
        }

        AudioManager.Instance?.Play("CoinSquirt");

        yield return new WaitForSeconds(0.6f);

        // ==========================================
        // SPAWN PHYSICAL COIN FOUNTAIN (SEQUENTIAL CONE)
        // ==========================================
        _activeRewardCoins.Clear();
        for (int i = 0; i < visualCoinsToSpawn; i++)
        {
            Vector3 spawnPos = boulderPos + (Vector3.up * 1.5f);
            GameObject coin = Instantiate(coin3DPrefab, spawnPos, Random.rotation);
            _activeRewardCoins.Add(coin);

            Rigidbody rb = coin.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // 1. Base upward pop
                Vector3 upwardForce = Vector3.up * coinUpwardForce;

                // 2. Outward spread cone (random point in a flat circle)
                Vector2 randomCircle = Random.insideUnitCircle * coinSpreadRadius;
                Vector3 spreadForce = new Vector3(randomCircle.x, 0f, randomCircle.y);

                // 3. Combine and apply random multiplier
                Vector3 finalForce = upwardForce + spreadForce;
                finalForce *= 1f + Random.Range(-coinRandomness, coinRandomness);

                rb.AddForce(finalForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 25f, ForceMode.Impulse);
            }
            else
            {
                // Fallback for non-rigidbody coins
                Vector2 randomCircle = Random.insideUnitCircle * 3.5f;
                Vector3 targetPos = boulderPos + new Vector3(randomCircle.x, 0f, randomCircle.y);
                coin.transform.DOJump(targetPos, jumpPower: Random.Range(3f, 5f), numJumps: 2, duration: 0.7f).SetEase(Ease.OutBounce);
            }

            // THE FIX: Wait a fraction of a second before shooting the next coin!
            yield return new WaitForSeconds(coinSpawnDelay);
        }

        yield return new WaitForSeconds(0.5f);

        // Show Game Over Panel & Populate Data
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (continueButton != null) continueButton.interactable = false;

        if (newRecordBadge != null)
        {
            newRecordBadge.SetActive(isNewRecord);
            if (isNewRecord)
            {
                newRecordBadge.transform.DOKill();
                newRecordBadge.transform.localScale = Vector3.zero;
                newRecordBadge.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).SetDelay(0.2f);
            }
        }

        PlayerDataManager.Instance.UpdateBestDistance(_lastRunDistance);

        if (currentRunText != null) currentRunText.text = $"{Mathf.FloorToInt(_lastRunDistance)}m";
        if (bestRunText != null) bestRunText.text = $"Best : {Mathf.FloorToInt(PlayerDataManager.Instance.data.bestDistance)}m";
        if (totalGoldText != null) totalGoldText.text = FormatMoney(PlayerDataManager.Instance.data.gold);

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
        if (continueButton != null) continueButton.interactable = true;
    }

    private IEnumerator FinalizeAndReloadRoutine()
    {
        Camera mainCam = Camera.main;
        

        foreach (var coin in _activeRewardCoins)
        {
            if (coin == null) continue;

            Rigidbody rb = coin.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Collider col = coin.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (mainCam != null && goldTextTarget != null)
            {
                Vector3 screenTarget = goldTextTarget.position;
                screenTarget.z = 4.5f;
                Vector3 worldTarget = mainCam.ScreenToWorldPoint(screenTarget);

                Vector3 initialScale = coin.transform.localScale;

                // THE FIX: Target UI size uses your new Inspector variable!
                Vector3 targetUiScale = initialScale * uiCoinScaleMultiplier;

                // Smooth Flight Tween
                coin.transform.DOMove(worldTarget, coinFlightDuration).SetEase(Ease.InOutCubic);

                

                // Proportional Size Shrink
                coin.transform.DOScale(targetUiScale, coinFlightDuration).SetEase(Ease.InOutCubic).OnComplete(() =>
                {
                    if (coin != null)
                    {
                        Sequence popSeq = DOTween.Sequence();
                        popSeq.Append(coin.transform.DOScale(targetUiScale * 1.35f, 0.1f).SetEase(Ease.OutQuad));
                        popSeq.Append(coin.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack));
                        popSeq.OnComplete(() => Destroy(coin));

                        if (goldTextTarget != null)
                        {
                            goldTextTarget.DOKill(true);
                            goldTextTarget.DOPunchScale(new Vector3(0.18f, 0.18f, 0.18f), 0.15f, 10, 1f);
                        }
                    }
                });
            }

            // Stagger each coin's departure based on your tuning settings
            yield return new WaitForSeconds(coinFlightStagger);
        }

        yield return new WaitForSeconds(coinFlightDuration + 0.3f);

        RewardManager.Instance.FinalizeRunRewards(_lastRunDistance);
        UpdateUI();

        yield return new WaitForSeconds(0.2f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Link this method to your UI Button's OnClick event in the Inspector!
    public void OnContinuePressed()
    {
        if (continueButton != null) continueButton.interactable = false; // Prevent double-clicks
        StartCoroutine(FinalizeAndReloadRoutine());
    }


    public void TriggerPrestigeWin()
    {
        StartCoroutine(PrestigeWinRoutine());
    }

    private IEnumerator PrestigeWinRoutine()
    {
        currentState = GameState.Idle;
        int currentLevel = PlayerPrefs.GetInt("PrestigeLevel", 1);
        int nextLevel = currentLevel + 1;

        // 1. Ensure normal Game Over panel is off
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // 2. Freeze camera tracking cleanly
        LaunchSequenceManager launchManager = FindObjectOfType<LaunchSequenceManager>();
        if (launchManager != null && launchManager.vcamFollow != null)
        {
            launchManager.vcamFollow.Follow = null;
        }
        DynamicBoulderCamera customCam = FindObjectOfType<DynamicBoulderCamera>();
        if (customCam != null) customCam.enabled = false;

        yield return new WaitForSeconds(0.5f);

        // 3. Calculate Prestige Data & Wipe Save
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

        // 4. THE FIX: Open the Dedicated Level Cleared Panel!
        if (levelClearedPanel != null)
        {
            levelClearedPanel.SetActive(true);
            levelClearedPanel.transform.DOKill();
            levelClearedPanel.transform.localScale = Vector3.zero;
            levelClearedPanel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        }

        if (prestigeContinueButton != null) prestigeContinueButton.interactable = true;

        if (levelClearedTitleText != null) levelClearedTitleText.text = $"LEVEL {currentLevel} CLEARED!";
        if (nextLevelPreviewText != null) nextLevelPreviewText.text = $"Entering Level {nextLevel}!";
        if (levelClearedBonusText != null) levelClearedBonusText.text = $"+{FormatMoney(startingGoldBonus)} GOLD BONUS";
        AudioManager.Instance?.Play("Win");
    }

    /// <summary>
    /// Hook this directly to the Continue Button inside your Level Cleared Panel!
    /// </summary>
    public void OnPrestigeContinuePressed()
    {
        if (prestigeContinueButton != null) prestigeContinueButton.interactable = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (arcadeBoulder != null) arcadeBoulder.OnRunFinished -= StartEndRunSequence;
    }
}