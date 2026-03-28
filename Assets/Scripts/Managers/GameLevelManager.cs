using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


[System.Serializable]
public struct GoldTier
{
    public string tierName;      // e.g. "Bronze Sector"
    public float endDistance;    // e.g. 500m
    public float goldPerMeter;   // e.g. 1.0 (Base), 1.5 (Silver), 2.0 (Gold)
}

public class GameLevelManager : MonoBehaviour
{
    public enum GameState { Idle, Aiming, Launched }
    public GameState currentState = GameState.Idle;

    [Header("Cameras")]
    public CinemachineVirtualCamera idleCam;
    public CinemachineVirtualCamera launchCam;
    public CinemachineVirtualCamera followCam;

    [Header("References")]
    //public TitanLauncher titanVisuals;
    public ArcadeBoulder arcadeBoulder;
    public Transform boulderVisualMesh;

    [Header("Settings")]
    public float minPullToLaunch = 0.10f;

    [Header("UI References (Optional)")]
    public TextMeshProUGUI goldText;

    [Header("Economy Configuration")]
    [Tooltip("Define your zones here. Ensure they are sorted by distance!")]
    public List<GoldTier> distanceTiers = new List<GoldTier>()
    {
        new GoldTier { tierName = "Zone 1", endDistance = 500f, goldPerMeter = 1.0f },
        new GoldTier { tierName = "Zone 2", endDistance = 1500f, goldPerMeter = 2.0f },
        new GoldTier { tierName = "Zone 3", endDistance = 3000f, goldPerMeter = 4.0f },
        new GoldTier { tierName = "Infinity", endDistance = 99999f, goldPerMeter = 10.0f }
    };

    [Header("End Run Settings")]
    public float animationDelay = 2.5f;

    [Header("Scaling Settings")]
    public float maxBoulderScale = 15f;

    private void Start()
    {
        SwitchToCam(idleCam);
        currentState = GameState.Idle;

        // Subscribe to events
        InputManager.Instance.OnDragUpdate += HandleDragUpdate;
        InputManager.Instance.OnDragEnd += HandleDragEnd;

        SyncStatsFromSave();

        // Listen for the Boulder Stopping
        arcadeBoulder.OnRunFinished += StartEndRunSequence;
    }

    private void Update()
    {
        // --- STEERING LOGIC ---
        // Only active when the ball is actually rolling
        if (currentState == GameState.Launched)
        {
            // Get Combined Input (-1 to 1) from Touch or Joystick
            float steerVal = InputManager.Instance.SteeringInput;

            // Pass to Arcade Physics
            arcadeBoulder.Steer(steerVal);
        }
    }

    private void SyncStatsFromSave()
    {
        int massLevel = PlayerDataManager.Instance.data.massLevel;

        // --- 1. THE DOUBLING LOGIC ---
        // Every 5 levels, we double the size.
        // Formula: 2 ^ (Level / 5)
        // Level 1-4: Scale 1
        // Level 5-9: Scale 2
        // Level 10-14: Scale 4
        // Level 15: Scale 8...

        int sizeTier = massLevel / 5;
        float doublingFactor = Mathf.Pow(1.3f, sizeTier);

        // We add a tiny linear bit (0.1 per level) so levels 2,3,4 still feel like progress
        float linearFactor = (massLevel % 5) * 0.1f;

        float finalScale = 1.0f * (doublingFactor + linearFactor);

        // SAFETY CAP: Don't let it get larger than the mountain
        finalScale = Mathf.Clamp(finalScale, 1.0f, maxBoulderScale);

        if (boulderVisualMesh != null)
        {
            // Use LeanTween or regular scaling for smoothness
            boulderVisualMesh.localScale = Vector3.one * finalScale;

            // Adjust Collider (Optional: if your physics feels weird with big rocks)
            // arcadeBoulder.GetComponent<SphereCollider>().radius = 0.5f * finalScale;
        }

        Debug.Log($"Stats Synced | Level: {massLevel} | Scale: x{finalScale:F2}");
    }

    private void FireBoulder(float pullPercentage)
    {
        currentState = GameState.Launched;

        // 1. GET MAX SPEED (e.g., 148 km/h)
        float maxSpeed = PlayerDataManager.Instance.GetTotalLaunchSpeed();

        // 2. CALCULATE ACTUAL SPEED (Based on drag input 0.0 - 1.0)
        float finalSpeed = maxSpeed * pullPercentage;

        // 3. LAUNCH (Arcade Physics)
        arcadeBoulder.Launch(finalSpeed);

        // 4. JUICE
        //titanVisuals.TriggerKick();
        SwitchToCam(followCam);
    }

    public void BuyMassUpgrade()
    {
        if (PlayerDataManager.Instance.TryBuyUpgrade("Mass"))
        {
            int newLevel = PlayerDataManager.Instance.data.massLevel;

            // Check Milestone: Is this a multiple of 5? (5, 10, 15...)
            if (newLevel % 5 == 0)
            {
            }

            SyncStatsFromSave(); // Apply size immediately
        }
    }

    public void BuyStrengthUpgrade()
    {
        if (PlayerDataManager.Instance.TryBuyUpgrade("Strength"))
        {
            int newLevel = PlayerDataManager.Instance.data.strengthLevel;

            if (newLevel % 5 == 0)
            {
            }
        }
    }

    public void BuyGreedUpgrade()
    {
        if (PlayerDataManager.Instance.TryBuyUpgrade("Greed"))
        {
            int newLevel = PlayerDataManager.Instance.data.greedLevel;

            if (newLevel % 5 == 0)
            {
            }
        }
    }
    // Simple UI refresher
    public void UpdateUI()
    {
        if (goldText != null)
        {
            // Format large numbers (e.g., "1.2B")
            double gold = PlayerDataManager.Instance.data.gold;
            goldText.text = "Gold: " + gold.ToString("N0");
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnDragUpdate -= HandleDragUpdate;
            InputManager.Instance.OnDragEnd -= HandleDragEnd;
        }

        if (arcadeBoulder != null)
            arcadeBoulder.OnRunFinished -= StartEndRunSequence;
    }

    // --- LOGIC ---

    private void HandleDragUpdate(float power)
    {
        // Only allow stretching if we are already in the "Aiming" mode
        if (currentState == GameState.Aiming)
        {
            //titanVisuals.SetStretch(power);
        }
    }

    private void HandleDragEnd(float power)
    {
        // LOGIC BRANCH: What state are we in?

        if (currentState == GameState.Idle)
        {
            // IDLE PHASE: A tap happened.
            // Switch to AIMING mode (Zoom in).
            Debug.Log("Tap Detected: Switching to Aiming Mode");
            currentState = GameState.Aiming;
            SwitchToCam(launchCam);
            return;
        }

        if (currentState == GameState.Aiming)
        {
            // AIMING PHASE: A release happened.
            // Did they pull far enough?
            if (power >= minPullToLaunch)
            {
                FireBoulder(power);
            }
            else
            {
                // They let go without pulling enough. 
                // Just reset the leg, but STAY in Aiming mode (don't go back to Idle).
                //titanVisuals.SetStretch(0f);
            }
        }
    }

    private void SwitchToCam(CinemachineVirtualCamera target)
    {
        idleCam.Priority = 0;
        launchCam.Priority = 0;
        followCam.Priority = 0;
        target.Priority = 10;
    }


    // --- 1. THE CALCULATION LOGIC (Tax Bracket Style) ---

    public int CalculateTotalGold(float totalDistance)
    {
        float remainingDistance = totalDistance;
        float accumulatedGold = 0f;
        float previousTierEnd = 0f;

        // Get Player's Greed Multiplier (e.g. 1.2x)
        float greedMult = PlayerDataManager.Instance.GetGoldMultiplier();

        foreach (GoldTier tier in distanceTiers)
        {
            if (remainingDistance <= 0) break;

            // How long is this specific tier? (e.g. 0 to 500 = 500m length)
            float tierLength = tier.endDistance - previousTierEnd;

            // How much of OUR distance falls into this tier?
            float distanceInThisTier = Mathf.Min(remainingDistance, tierLength);

            // Add Cash
            accumulatedGold += distanceInThisTier * tier.goldPerMeter;

            // Prepare for next loop
            remainingDistance -= distanceInThisTier;
            previousTierEnd = tier.endDistance;
        }

        // Apply global multiplier and return integer
        return Mathf.FloorToInt(accumulatedGold * greedMult);
    }

    // --- 2. THE END SEQUENCE (Coroutine) ---

    private void StartEndRunSequence()
    {
        StartCoroutine(EndRunRoutine());
    }

    private IEnumerator EndRunRoutine()
    {
        currentState = GameState.Idle; // Prevent input

        // A. Calculate Earnings
        // We use the Boulder's Z position as distance (assuming start is Z=0)
        float finalDist = arcadeBoulder.transform.position.z;
        int goldEarned = CalculateTotalGold(finalDist);

        Debug.Log($"Run Over! Distance: {finalDist:F0}m | Gold: {goldEarned}");

        // B. Trigger UI Animation (Placeholder)
        // UIManager.Instance.ShowEndScreen(finalDist, goldEarned, animationDelay);
        // Play "Coin Count Up" Sound Loop here

        // C. Wait for "Juice" (The Delay)
        yield return new WaitForSeconds(animationDelay);

        // D. Save Data
        PlayerDataManager.Instance.AddGold(goldEarned);

        // E. Restart Scene
        // Using "LoadScene" cleans up all physics/memory automatically
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}