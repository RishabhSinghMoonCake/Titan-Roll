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

    private bool _isUpgradingCutscene = false;

    [Header("Dynamic Camera Framers")]
    public CinemachineDynamicScaler boulderZoomFramer; // <-- Changed type
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
        yield return new WaitForSeconds(dramaticPauseDuration);

        // 4. Reset camera
        if (launchSequenceManager != null) launchSequenceManager.ResetToIdleCamera();

        UpdateUI();
        _isUpgradingCutscene = false;
    }

    private IEnumerator StrengthMilestoneCutsceneRoutine()
    {
        _isUpgradingCutscene = true;

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

        yield return new WaitForSeconds(dramaticPauseDuration);

        // 4. Reset camera
        if (launchSequenceManager != null) launchSequenceManager.ResetToIdleCamera();

        UpdateUI();
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

    private IEnumerator EndRunRoutine()
    {
        currentState = GameState.Idle;
        float finalDist = arcadeBoulder.transform.position.z;
        RewardManager.Instance.FinalizeRunRewards(finalDist);
        yield return new WaitForSeconds(2.5f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (arcadeBoulder != null) arcadeBoulder.OnRunFinished -= StartEndRunSequence;
    }
}