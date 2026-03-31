using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

public class LaunchSequenceManager : MonoBehaviour
{
    [Header("Managers")]
    public CharacterSkinManager skinManager;
    // Note: GameLevelManager is accessed via GameLevelManager.Instance!

    [Header("Cameras")]
    public CinemachineVirtualCamera vcamIdle;
    public CinemachineVirtualCamera vcamMinigame;
    public CinemachineVirtualCamera vcamFollow;

    [Header("Characters & Objects")]
    public ArcadeBoulder boulder;
    [Tooltip("How far forward the hand extends from the character's root at exact frame of impact")]
    public float baseImpactReach = 2.5f;

    [Header("Weapon Animation")]
    public float weaponPickupDuration = 0.5f;

    [Header("UI Minigame")]
    public GameObject timingMinigamePanel;
    public Slider timingSlider;
    public float sliderSpeed = 2f;

    private bool _waitingForFirstTap = true;
    private bool _waitingForTimingTap = false;
    private float _timingResult = 0f;

    private void Start()
    {
        // Listen for screen taps from our Input Manager
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnLaunchTap += HandleTap;
        }

        // Begin the cinematic flow
        StartCoroutine(PreLaunchSequence());
    }

    private void HandleTap()
    {
        // Route the tap to whichever phase of the sequence we are currently in
        if (_waitingForFirstTap) _waitingForFirstTap = false;
        else if (_waitingForTimingTap) _waitingForTimingTap = false;
    }

    private IEnumerator PreLaunchSequence()
    {
        // --- 1. IDLE STATE ---
        CutToCamera(vcamIdle);
        if (timingMinigamePanel != null) timingMinigamePanel.SetActive(false);
        AlignCharacterToBoulder();

        // Wait for the player to tap the screen to start the sequence
        _waitingForFirstTap = true;
        while (_waitingForFirstTap) yield return null;


        // --- 2. WEAPON PICKUP ---
        // Magically fly the giant weapon from the ground into the character's hand
        yield return StartCoroutine(FlyWeaponToHand());


        // --- 3. TIMING MINIGAME (DISABLED FOR NOW) ---
        /*
        CutToCamera(vcamMinigame);
        timingMinigamePanel.SetActive(true);

        _waitingForTimingTap = true;
        float sliderPingPong = 0f;

        // Bounce the slider back and forth until the player taps again
        while (_waitingForTimingTap)
        {
            sliderPingPong += Time.deltaTime * sliderSpeed;
            timingSlider.value = Mathf.PingPong(sliderPingPong, 1f);
            yield return null;
        }

        // Calculate accuracy (0.5 is perfect dead center of the slider)
        float distanceFromCenter = Mathf.Abs(0.5f - timingSlider.value);
        _timingResult = 1f - (distanceFromCenter * 2f); // Results in a 0.0 to 1.0 multiplier

        timingMinigamePanel.SetActive(false);
        */

        // TEMPORARY: Force 100% Perfect Timing for the single-tap launch
        _timingResult = 1f;


        // --- 4. THE SLAP ANIMATION ---
        if (skinManager != null && skinManager.currentActiveAnimator != null)
        {
            skinManager.currentActiveAnimator.SetTrigger("Slap");
        }

        // CRITICAL: Wait for the exact frame the hand physically hits the boulder
        // You will need to tweak this number based on your specific animation!
        yield return new WaitForSeconds(0.6f);


        // --- 5. THE LAUNCH & HANDOFF ---
        CutToCamera(vcamFollow);

        // Get base speed from GameLevelManager and apply our minigame accuracy multiplier
        float baseLaunchSpeed = GameLevelManager.Instance.GetTotalLaunchSpeed();

        // Final speed: At worst 50% speed, at best 100% speed
        float finalSpeed = Mathf.Lerp(baseLaunchSpeed * 0.5f, baseLaunchSpeed, _timingResult);

        // Tell the dynamic camera to do its cinematic lag/rubber-band effect
        FindObjectOfType<DynamicBoulderCamera>()?.TriggerLaunchSequence();

        // Fire the physics boulder!
        boulder.Launch(finalSpeed);

        // Pass control back to GameLevelManager to allow joystick steering
        GameLevelManager.Instance.SetStateToLaunched();
    }

    private IEnumerator FlyWeaponToHand()
    {
        if (skinManager == null) yield break;

        Transform weapon = skinManager.giantWeaponInScene;
        Transform targetSocket = skinManager.currentWeaponSocket;

        if (weapon == null || targetSocket == null) yield break;

        Vector3 startPos = weapon.position;
        Quaternion startRot = weapon.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < weaponPickupDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / weaponPickupDuration;

            // SmoothStep makes the weapon accelerate and decelerate naturally
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            weapon.position = Vector3.Lerp(startPos, targetSocket.position, smoothT);
            weapon.rotation = Quaternion.Lerp(startRot, targetSocket.rotation, smoothT);

            yield return null;
        }

        // Once it arrives, formally parent it to the bone socket so it follows animations
        weapon.SetParent(targetSocket);
        weapon.localPosition = Vector3.zero;
        weapon.localRotation = Quaternion.identity;
    }

    private void CutToCamera(CinemachineVirtualCamera targetCam)
    {
        // Reset all cameras to baseline priority
        if (vcamIdle) vcamIdle.Priority = 10;
        if (vcamMinigame) vcamMinigame.Priority = 10;
        if (vcamFollow) vcamFollow.Priority = 10;

        // Elevate the requested camera to instantly cut to it
        if (targetCam) targetCam.Priority = 20;
    }

    private void AlignCharacterToBoulder()
    {
        if (boulder == null || skinManager == null || skinManager.visualHolder == null) return;

        // Calculate how big the boulder currently is
        float currentBoulderRadius = boulder.transform.localScale.z * 0.5f;
        Vector3 boulderPos = boulder.transform.position;

        // Calculate the exact distance to stand behind the boulder so the hand hits the edge
        float perfectZPosition = boulderPos.z - (currentBoulderRadius + baseImpactReach);

        // Move the Character's parent container
        Transform charRoot = skinManager.visualHolder;
        charRoot.position = new Vector3(charRoot.position.x, charRoot.position.y, perfectZPosition);
    }

    private void OnDestroy()
    {
        // Clean up events to prevent memory leaks
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnLaunchTap -= HandleTap;
        }
    }
}