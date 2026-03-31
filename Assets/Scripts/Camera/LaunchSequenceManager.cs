using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

public class LaunchSequenceManager : MonoBehaviour
{
    [Header("Cameras")]
    public CinemachineVirtualCamera vcamIdle;
    public CinemachineVirtualCamera vcamMinigame;
    public CinemachineVirtualCamera vcamFollow;

    [Header("Characters & Objects")]
    public Transform characterRoot;
    public Animator characterAnimator;
    public ArcadeBoulder boulder;

    [Header("Alignment Settings")]
    [Tooltip("How far forward the hand extends from the character's root at the exact frame of impact")]
    public float baseImpactReach = 2.5f;

    [Header("UI Minigame")]
    public GameObject timingMinigamePanel;
    public Slider timingSlider;
    public float sliderSpeed = 2f;

    // Internal State
    private bool _waitingForFirstTap = true;
    private bool _waitingForTimingTap = false;
    private float _timingResult = 0f; // 0 is worst, 1 is perfect dead center

    private void Start()
    {
        // Subscribe to your Input Manager
        InputManager.Instance.OnLaunchTap += HandleTap;

        // Start the sequence!
        StartCoroutine(PreLaunchSequence());
    }

    private void HandleTap()
    {
        if (_waitingForFirstTap) _waitingForFirstTap = false;
        else if (_waitingForTimingTap) _waitingForTimingTap = false;
    }

    private IEnumerator PreLaunchSequence()
    {
        // --- 1. IDLE STATE ---
        CutToCamera(vcamIdle);
        timingMinigamePanel.SetActive(false);

        // Calculate exact positions based on current upgrades
        AlignCharacterToBoulder();

        // Wait for the player to tap anywhere on the screen to start
        _waitingForFirstTap = true;
        while (_waitingForFirstTap) yield return null;


        // --- 2. TIMING MINIGAME STATE ---
        CutToCamera(vcamMinigame);
        timingMinigamePanel.SetActive(true);

        // Optional: Play a "winding up" animation here
        // characterAnimator.SetTrigger("WindUp");

        _waitingForTimingTap = true;
        float sliderPingPong = 0f;

        // Oscillate the slider left and right until they tap again
        while (_waitingForTimingTap)
        {
            sliderPingPong += Time.deltaTime * sliderSpeed;
            // Mathf.PingPong bounces the value perfectly between 0 and 1
            timingSlider.value = Mathf.PingPong(sliderPingPong, 1f);
            yield return null;
        }

        // Calculate how close they were to the perfect middle (0.5)
        // This gives a percentage from 0.0 (terrible) to 1.0 (perfect)
        float distanceFromCenter = Mathf.Abs(0.5f - timingSlider.value);
        _timingResult = 1f - (distanceFromCenter * 2f);

        timingMinigamePanel.SetActive(false);


        // --- 3. THE SLAP ANIMATION ---
        characterAnimator.SetTrigger("Slap");

        // We must wait for the exact moment the hand hits the boulder in the animation.
        // E.g., if the slap impacts at 0.6 seconds into the animation clip:
        yield return new WaitForSeconds(0.6f);


        // --- 4. THE IMPACT & LAUNCH ---
        // Switch to the dynamic follow camera right before it flies away
        CutToCamera(vcamFollow);

        // Ask PlayerDataManager for base speed, and multiply it by our Minigame timing result!
        float baseLaunchSpeed = GameLevelManager.Instance.GetTotalLaunchSpeed();

        // Example: Perfect hit = 100% speed. Worst hit = 50% speed.
        float finalSpeed = Mathf.Lerp(baseLaunchSpeed * 0.5f, baseLaunchSpeed, _timingResult);

        // Tell the camera to do its cinematic lag (from our previous script)
        FindObjectOfType<DynamicBoulderCamera>()?.TriggerLaunchSequence();

        // Fire the physics!
        boulder.Launch(finalSpeed);
    }

    // --- HELPER METHODS ---

    private void CutToCamera(CinemachineVirtualCamera targetCam)
    {
        // Reset all to 10
        vcamIdle.Priority = 10;
        vcamMinigame.Priority = 10;
        vcamFollow.Priority = 10;

        // Elevate the requested camera to 20 so Cinemachine cuts to it
        targetCam.Priority = 20;
    }

    private void AlignCharacterToBoulder()
    {
        // Get the current radius of the boulder
        float currentBoulderRadius = boulder.transform.localScale.z * 0.5f; // Assuming base radius is 0.5

        // Get the current reach of the hand (if you have strength/size upgrades for the character)
        // float currentHandReach = baseImpactReach * PlayerDataManager.Instance.GetCharacterScale();
        float currentHandReach = baseImpactReach;

        // Position the character exactly 'Radius + Reach' meters behind the boulder's center
        Vector3 boulderPos = boulder.transform.position;
        float perfectZPosition = boulderPos.z - (currentBoulderRadius + currentHandReach);

        // We keep the Character's current X and Y, just shift them backward on the Z axis
        characterRoot.position = new Vector3(characterRoot.position.x, characterRoot.position.y, perfectZPosition);
    }
}