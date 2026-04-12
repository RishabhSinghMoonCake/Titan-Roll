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

    [Header("Objects")]
    public ArcadeBoulder boulder;
    public float baseImpactReach = 2.5f;

    [Header("Drag Settings")]
    public float maxDragPixels = 200f;

    [Header("Timing")]
    public float impactDelayAfterRelease = 0.15f;

    [Header("UI")]
    public GameObject timingMinigamePanel;
    public Slider timingSlider;

    [Header("Phase Settings")]
    [Tooltip("The upgrade panel to hide when the game starts")]
    public GameObject upgradePanel;
    [Tooltip("Seconds to ignore touches after the camera cuts to prevent misfires")]
    public float inputDeadZoneDelay = 0.4f;

    private bool _waitingForFirstTap = true;

    private float smoothedDragPower = 0f;
    private float targetDragPower = 0f;
    private float velocity = 0f;

    private void Start()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnLaunchTap += HandleInitialTap;

        // Reset the upgrade panel scale in case the level was restarted
        if (upgradePanel != null) upgradePanel.transform.localScale = Vector3.one;

        StartCoroutine(PreLaunchSequence());
    }

    private void HandleInitialTap()
    {
        if (_waitingForFirstTap) _waitingForFirstTap = false;
    }

    private IEnumerator PreLaunchSequence()
    {
        // --- IDLE ---
        CutToCamera(vcamIdle);
        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);
        AlignCharacterToBoulder();

        _waitingForFirstTap = true;
        while (_waitingForFirstTap) yield return null;

        // --- PICKUP ---
        yield return StartCoroutine(FlyWeaponToHand());

        var animator = skinManager.currentActiveAnimator;

        if (animator != null)
            animator.SetTrigger("StartMinigame");

        yield return new WaitForSeconds(0.5f);

        // --- DRAG PHASE ---
        CutToCamera(vcamMinigame);

        if (timingMinigamePanel) timingMinigamePanel.SetActive(true);
        if (timingSlider) timingSlider.value = 0f;

        // 1. ANIMATE OUT THE UPGRADE PANEL (DOTWEEN)
        if (upgradePanel != null && upgradePanel.activeSelf)
        {
            upgradePanel.transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => upgradePanel.SetActive(false));
        }

        bool isDragging = false;
        Vector2 startTouchPos = Vector2.zero;

        float dpiScale = Screen.dpi > 0 ? Screen.dpi / 160f : Screen.height / 1080f;
        float actualMaxDrag = maxDragPixels * dpiScale;

        // 2. SET THE DEAD-ZONE TIMER
        float safeInputTime = Time.time + inputDeadZoneDelay;

        // 3. TRACK THE SPECIFIC FINGER
        UnityEngine.InputSystem.EnhancedTouch.Finger activeFinger = null;

        while (true)
        {
            // --- NON-BLOCKING SHIELD ---
            // Ignore touch inputs entirely until the camera has settled
            if (Time.time >= safeInputTime)
            {
                var touches = Touch.activeTouches;

                if (!isDragging && touches.Count > 0)
                {
                    foreach (var touch in touches)
                    {
                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                        {
                            isDragging = true;
                            activeFinger = touch.finger; // Lock onto this exact finger
                            startTouchPos = touch.screenPosition;

                            if (animator != null)
                                animator.SetBool("IsHolding", true);

                            break; // Stop checking other touches
                        }
                    }
                }
                else if (isDragging && activeFinger != null)
                {
                    var currentTouch = activeFinger.currentTouch;

                    if (currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                        currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    {
                        // Strict downward 1D pull
                        float dragDistY = startTouchPos.y - currentTouch.screenPosition.y;
                        targetDragPower = Mathf.Clamp01(dragDistY / actualMaxDrag);
                    }
                    else if (currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                             currentTouch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        break; // Player released the screen!
                    }
                }
            }

            // Smooth power (Using your exact working SmoothDamp)
            smoothedDragPower = Mathf.SmoothDamp(
                smoothedDragPower,
                targetDragPower,
                ref velocity,
                0.08f
            );

            // Update animator
            if (animator != null)
                animator.SetFloat("WindupPower", smoothedDragPower);

            if (timingSlider)
                timingSlider.value = smoothedDragPower;

            // Weapon shake at max
            if (skinManager.giantWeaponInScene != null)
            {
                if (smoothedDragPower > 0.95f)
                {
                    skinManager.giantWeaponInScene.localRotation =
                        Quaternion.Euler(
                            Random.Range(-3f, 3f),
                            Random.Range(-3f, 3f),
                            Random.Range(-3f, 3f)
                        );
                }
                else
                {
                    skinManager.giantWeaponInScene.localRotation = Quaternion.identity;
                }
            }

            yield return null;
        }

        // Cleanup
        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);
        if (skinManager.giantWeaponInScene != null)
            skinManager.giantWeaponInScene.localRotation = Quaternion.identity;

        // --- RELEASE ---
        if (animator != null)
        {
            animator.SetBool("IsHolding", false);

            // small anticipation delay
            yield return new WaitForSeconds(0.05f);

            animator.SetTrigger("Release");
        }

        yield return new WaitForSeconds(impactDelayAfterRelease);

        // --- LAUNCH ---
        CutToCamera(vcamFollow);

        float baseLaunchSpeed = GameLevelManager.Instance.GetTotalLaunchSpeed();
        float finalPower = Mathf.Pow(smoothedDragPower, 1.5f);
        float finalSpeed = Mathf.Lerp(baseLaunchSpeed * 0.25f, baseLaunchSpeed, finalPower);

        FindObjectOfType<DynamicBoulderCamera>()?.TriggerLaunchSequence();
        boulder.Launch(finalSpeed);

        GameLevelManager.Instance.SetStateToLaunched();
    }

    private IEnumerator FlyWeaponToHand()
    {
        if (skinManager == null) yield break;

        Transform weapon = skinManager.giantWeaponInScene;
        Transform socket = skinManager.currentWeaponSocket;

        if (weapon == null || socket == null) yield break;

        weapon.DOMove(socket.position, 0.5f).SetEase(Ease.InOutSine);
        weapon.DORotateQuaternion(socket.rotation, 0.5f).SetEase(Ease.InOutSine);

        yield return new WaitForSeconds(0.5f);

        weapon.SetParent(socket);
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

        float radius = boulder.transform.localScale.z * 0.5f;
        Vector3 pos = boulder.transform.position;

        float z = pos.z - (radius + baseImpactReach);

        Transform charRoot = skinManager.visualHolder;
        charRoot.position = new Vector3(charRoot.position.x, charRoot.position.y, z);
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnLaunchTap -= HandleInitialTap;
    }
}