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

        float safeInputTime = Time.time + inputDeadZoneDelay;
        UnityEngine.InputSystem.EnhancedTouch.Finger activeFinger = null;

        while (true)
        {
            if (Time.time >= safeInputTime)
            {
                // THE MEMORY LEAK FIX: We ONLY look at the fresh touches for this exact frame
                var touches = Touch.activeTouches;

                if (!isDragging && touches.Count > 0)
                {
                    foreach (var touch in touches)
                    {
                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                        {
                            isDragging = true;
                            activeFinger = touch.finger;
                            startTouchPos = touch.screenPosition;

                            if (animator != null)
                                animator.SetBool("IsHolding", true);

                            break;
                        }
                    }
                }
                else if (isDragging && activeFinger != null)
                {
                    bool fingerStillOnScreen = false;

                    // Manually check if our specific finger is still touching the screen this frame
                    foreach (var touch in touches)
                    {
                        if (touch.finger == activeFinger)
                        {
                            fingerStillOnScreen = true;

                            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                                touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                            {
                                float dragDistY = startTouchPos.y - touch.screenPosition.y;
                                targetDragPower = Mathf.Clamp01(dragDistY / actualMaxDrag);
                            }
                            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                                     touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                            {
                                isDragging = false; // Player lifted their finger
                            }

                            break; // Found our finger, stop looping
                        }
                    }

                    // If they lifted the finger, OR the finger data vanished from the screen array, break!
                    if (!fingerStillOnScreen || !isDragging)
                    {
                        break;
                    }
                }
            }

            smoothedDragPower = Mathf.SmoothDamp(
                smoothedDragPower,
                targetDragPower,
                ref velocity,
                0.08f
            );

            if (animator != null)
                animator.SetFloat("WindupPower", smoothedDragPower);

            if (timingSlider)
                timingSlider.value = smoothedDragPower;

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

        // --- CLEANUP & RELEASE ---
        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);
        if (skinManager.giantWeaponInScene != null)
            skinManager.giantWeaponInScene.localRotation = Quaternion.identity;

        if (animator != null)
        {
            animator.SetBool("IsHolding", false);
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