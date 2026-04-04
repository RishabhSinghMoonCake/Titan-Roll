using Cinemachine;
using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class LaunchSequenceManager : MonoBehaviour
{
    [Header("Managers")]
    public CharacterSkinManager skinManager;

    [Header("Cameras")]
    public CinemachineVirtualCamera vcamIdle;
    public CinemachineVirtualCamera vcamMinigame;
    public CinemachineVirtualCamera vcamFollow;

    [Header("Characters & Objects")]
    public ArcadeBoulder boulder;
    public float baseImpactReach = 2.5f;

    [Header("Weapon Animation")]
    public float weaponPickupDuration = 0.5f;

    [Header("Drag & Strike Mechanics")]
    [Tooltip("The exact name of your slapping animation state in the Animator (Case Sensitive)")]
    public string strikeAnimationStateName = "Slap";

    [Tooltip("How many reference pixels the user must drag to reach 100% power")]
    public float maxDragPixels = 200f;

    [Tooltip("At 100% power, what % of the animation is played? (e.g., 0.4 means it scrubs up to 40% into the animation)")]
    [Range(0.1f, 0.9f)] public float maxWindupNormalizedTime = 0.45f;

    [Tooltip("How long after the player releases the screen does the bat physically hit the rock?")]
    public float impactDelayAfterRelease = 0.15f;

    [Header("UI Minigame")]
    public GameObject timingMinigamePanel;
    public Slider timingSlider; // We will reuse this as a Power Bar!

    private bool _waitingForFirstTap = true;

    private void Start()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnLaunchTap += HandleInitialTap;
        }

        StartCoroutine(PreLaunchSequence());
    }

    private void HandleInitialTap()
    {
        if (_waitingForFirstTap) _waitingForFirstTap = false;
    }
    private IEnumerator PreLaunchSequence()
    {
        // --- 1. HAPPY IDLE STATE ---
        CutToCamera(vcamIdle);
        if (timingMinigamePanel != null) timingMinigamePanel.SetActive(false);
        AlignCharacterToBoulder();

        // Character is looping HappyIdle. Wait for tap.
        _waitingForFirstTap = true;
        while (_waitingForFirstTap) yield return null;


        // --- 2. WEAPON PICKUP & BASEBALL IDLE TRANSITION ---
        yield return StartCoroutine(FlyWeaponToHand());

        if (skinManager.currentActiveAnimator != null)
        {
            // Tell the Animator to blend from HappyIdle into BaseballIdle
            skinManager.currentActiveAnimator.SetTrigger("StartMinigame");
        }

        // Wait half a second for the animation transition to fully finish
        yield return new WaitForSeconds(0.5f);


        // --- 3. THE DRAG & WINDUP PHASE (CODE CONTROLLED) ---
        CutToCamera(vcamMinigame);
        if (timingMinigamePanel != null) timingMinigamePanel.SetActive(true);
        if (timingSlider != null) timingSlider.value = 0f;

        float targetDragPower = 0f;
        float smoothedDragPower = 0f;
        bool isDragging = false;
        Vector2 startTouchPos = Vector2.zero;

        float dpiScale = Screen.dpi > 0 ? Screen.dpi / 160f : Screen.height / 1080f;
        float actualMaxDrag = maxDragPixels * dpiScale;

        while (true)
        {
            if (Touch.activeTouches.Count > 0)
            {
                var touch = Touch.activeTouches[0];

                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    isDragging = true;
                    startTouchPos = touch.screenPosition;

                    // PAUSE the animator so we can manually scrub the frames!
                    if (skinManager.currentActiveAnimator != null)
                        skinManager.currentActiveAnimator.speed = 0f;
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)
                {
                    float currentDragDist = Vector2.Distance(startTouchPos, touch.screenPosition);
                    targetDragPower = Mathf.Clamp01(currentDragDist / actualMaxDrag);
                }
                else if ((touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled) && isDragging)
                {
                    break; // Player released the screen!
                }
            }

            // Smooth the input for buttery frame scrubbing
            smoothedDragPower = Mathf.Lerp(smoothedDragPower, targetDragPower, Time.deltaTime * 15f);
            if (timingSlider != null) timingSlider.value = smoothedDragPower;

            // --- THE SCRUBBING ---
            // Force the animator to display the exact frame of the Windup animation
            if (skinManager.currentActiveAnimator != null && isDragging)
            {
                // Ensure the string here exactly matches your Windup state name in the Animator!
                float currentAnimFrame = smoothedDragPower * maxWindupNormalizedTime;
                skinManager.currentActiveAnimator.Play("Windup", 0, currentAnimFrame);
                skinManager.currentActiveAnimator.Update(0);
            }

            // Juice: Weapon Shake at Max Power
            if (skinManager.giantWeaponInScene != null)
            {
                if (smoothedDragPower > 0.95f)
                    skinManager.giantWeaponInScene.localRotation = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), Random.Range(-3f, 3f));
                else
                    skinManager.giantWeaponInScene.localRotation = Quaternion.identity;
            }

            yield return null;
        }

        // Cleanup visuals
        if (skinManager.giantWeaponInScene != null) skinManager.giantWeaponInScene.localRotation = Quaternion.identity;
        if (timingMinigamePanel != null) timingMinigamePanel.SetActive(false);


        // --- 4. THE FULL RELEASE & SLAP ---

        if (skinManager.currentActiveAnimator != null)
        {
            // UNPAUSE the animator so it plays normally again
            skinManager.currentActiveAnimator.speed = 1f;

            // Fire the white arrow transition from Windup to Slap
            skinManager.currentActiveAnimator.SetTrigger("Release");
        }

        yield return new WaitForSeconds(impactDelayAfterRelease);

        CutToCamera(vcamFollow);

        float baseLaunchSpeed = GameLevelManager.Instance.GetTotalLaunchSpeed();
        float finalSpeed = Mathf.Lerp(baseLaunchSpeed * 0.25f, baseLaunchSpeed, smoothedDragPower);

        FindObjectOfType<DynamicBoulderCamera>()?.TriggerLaunchSequence();
        boulder.Launch(finalSpeed);

        GameLevelManager.Instance.SetStateToLaunched();
    }

    private IEnumerator FlyWeaponToHand()
    {
        if (skinManager == null) yield break;

        Transform weapon = skinManager.giantWeaponInScene;
        Transform targetSocket = skinManager.currentWeaponSocket;

        if (weapon == null || targetSocket == null) yield break;

        weapon.DOMove(targetSocket.position, weaponPickupDuration).SetEase(Ease.InOutSine);
        weapon.DORotateQuaternion(targetSocket.rotation, weaponPickupDuration).SetEase(Ease.InOutSine);

        yield return new WaitForSeconds(weaponPickupDuration);

        weapon.SetParent(targetSocket);
        weapon.localPosition = Vector3.zero;
        weapon.localRotation = Quaternion.identity;
    }

    private void CutToCamera(CinemachineVirtualCamera targetCam)
    {
        if (vcamIdle) vcamIdle.Priority = 10;
        if (vcamMinigame) vcamMinigame.Priority = 10;
        if (vcamFollow) vcamFollow.Priority = 10;

        if (targetCam) targetCam.Priority = 20;
    }

    private void AlignCharacterToBoulder()
    {
        if (boulder == null || skinManager == null || skinManager.visualHolder == null) return;

        float currentBoulderRadius = boulder.transform.localScale.z * 0.5f;
        Vector3 boulderPos = boulder.transform.position;
        float perfectZPosition = boulderPos.z - (currentBoulderRadius + baseImpactReach);

        Transform charRoot = skinManager.visualHolder;
        charRoot.position = new Vector3(charRoot.position.x, charRoot.position.y, perfectZPosition);
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnLaunchTap -= HandleInitialTap;
        }
    }
}