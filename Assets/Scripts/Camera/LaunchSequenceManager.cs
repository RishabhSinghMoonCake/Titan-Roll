using Cinemachine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TMPro;

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
    public HandTutorial handTutorialUI;
    public TextMeshProUGUI powerPercentageText;
    public TextMeshProUGUI tapToPlayText;
    public TextMeshProUGUI LevelText;

    [Header("Phase Settings")]
    public GameObject upgradePanel;
    public float inputDeadZoneDelay = 0.4f;

    private float smoothedDragPower = 0f;
    private float targetDragPower = 0f;
    private float velocity = 0f;

    [Header("Close Up Cameras")]
    public CinemachineVirtualCamera vcamBoulderCloseUp;
    public CinemachineVirtualCamera vcamHandCloseUp;

    private void Start()
    {
        InitializeNewRun();
    }

    public void InitializeNewRun()
    {
        smoothedDragPower = 0f;
        targetDragPower = 0f;
        velocity = 0f;

        if (upgradePanel != null)
        {
            upgradePanel.transform.DOKill();
            upgradePanel.transform.localScale = Vector3.one;
            upgradePanel.SetActive(true);
        }

        if (handTutorialUI != null) handTutorialUI.gameObject.SetActive(false);
        if (powerPercentageText != null) powerPercentageText.gameObject.SetActive(false);

        if (tapToPlayText != null)
        {
            tapToPlayText.transform.DOKill();
            tapToPlayText.transform.localScale = Vector3.one;
            tapToPlayText.gameObject.SetActive(true);
        }

        // --- NEW: LEVEL TEXT FADE IN ---
        if (LevelText != null)
        {
            int currentLevel = PlayerPrefs.GetInt("PrestigeLevel", 1);
            LevelText.text = $"LEVEL {currentLevel}";
            LevelText.gameObject.SetActive(true);

            // Reset state instantly before animating
            LevelText.transform.DOKill();
            LevelText.DOKill();
            LevelText.color = new Color(LevelText.color.r, LevelText.color.g, LevelText.color.b, 0f);
            LevelText.transform.localScale = Vector3.one * 0.5f;

            // Pop in and fade up
            LevelText.DOFade(1f, 0.4f);
            LevelText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        StopAllCoroutines();
        StartCoroutine(PreLaunchSequence());
    }

    private bool IsTouchOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private IEnumerator PreLaunchSequence()
    {
        CutToCamera(vcamIdle);
        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);
        AlignCharacterToBoulder();

        // --- PHASE 1: WAIT FOR THE FIRST START TAP ---
        bool waitingForStart = true;
        while (waitingForStart)
        {
            if (Touch.activeTouches.Count > 0)
            {
                var touch = Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (!IsTouchOverUI(touch.screenPosition))
                    {
                        waitingForStart = false; // Player clicked to start!
                    }
                }
            }
            yield return null;
        }

        if (upgradePanel != null && upgradePanel.activeSelf)
        {
            upgradePanel.transform.DOKill();
            upgradePanel.transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => upgradePanel.SetActive(false));
        }

        if (tapToPlayText != null && tapToPlayText.gameObject.activeSelf)
        {
            tapToPlayText.transform.DOKill();
            tapToPlayText.transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => tapToPlayText.gameObject.SetActive(false));
        }

        // --- NEW: FADE OUT LEVEL TEXT ON TAP ---
        if (LevelText != null && LevelText.gameObject.activeSelf)
        {
            LevelText.transform.DOKill();
            LevelText.DOFade(0f, 0.2f); // Quick fade
            LevelText.transform.DOScale(Vector3.zero, 0.25f)
                .SetEase(Ease.InBack)
                .OnComplete(() => LevelText.gameObject.SetActive(false));
        }


        // --- PHASE 2: TIMING CHANGED HERE ---
        // The weapon now remains completely stationary at its spawn point until this line fires
        yield return StartCoroutine(FlyWeaponToHand());

        var animator = skinManager.currentActiveAnimator;
        if (animator != null) animator.SetTrigger("StartMinigame");

        yield return new WaitForSeconds(0.5f);

        // --- PHASE 3: DRAG MINIGAME INITIALIZATION ---
        CutToCamera(vcamMinigame);

        if (timingMinigamePanel) timingMinigamePanel.SetActive(true);
        if (timingSlider) timingSlider.value = 0f;

        if (powerPercentageText != null)
        {
            powerPercentageText.gameObject.SetActive(true);
            powerPercentageText.text = "0%";
        }

        if (handTutorialUI != null) handTutorialUI.PlayTutorial();

        

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
                var touches = Touch.activeTouches;

                if (!isDragging && touches.Count > 0)
                {
                    foreach (var touch in touches)
                    {
                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                        {
                            if (IsTouchOverUI(touch.screenPosition)) continue;

                            isDragging = true;
                            activeFinger = touch.finger;
                            startTouchPos = touch.screenPosition;

                            if (handTutorialUI != null) handTutorialUI.StopTutorial();
                            if (animator != null) animator.SetBool("IsHolding", true);
                            break;
                        }
                    }
                }
                else if (isDragging && activeFinger != null)
                {
                    bool fingerStillOnScreen = false;
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
                                isDragging = false;
                            }
                            break;
                        }
                    }
                    if (!fingerStillOnScreen || !isDragging) break;
                }
            }

            smoothedDragPower = Mathf.SmoothDamp(smoothedDragPower, targetDragPower, ref velocity, 0.08f);
            if (animator != null) animator.SetFloat("WindupPower", smoothedDragPower);
            if (timingSlider) timingSlider.value = smoothedDragPower;

            if (powerPercentageText != null)
            {
                int percent = Mathf.RoundToInt(Mathf.Clamp01(smoothedDragPower) * 100f);
                powerPercentageText.text = $"{percent}%";
            }

            if (skinManager.giantWeaponInScene != null)
            {
                if (smoothedDragPower > 0.95f)
                {
                    skinManager.giantWeaponInScene.localRotation = Quaternion.Euler(
                        Random.Range(-3f, 3f), Random.Range(-3f, 3f), Random.Range(-3f, 3f));
                }
                else
                {
                    skinManager.giantWeaponInScene.localRotation = Quaternion.identity;
                }
            }
            yield return null;
        }

        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);
        if (powerPercentageText != null) powerPercentageText.gameObject.SetActive(false);

        if (skinManager.giantWeaponInScene != null)
            skinManager.giantWeaponInScene.localRotation = Quaternion.identity;

        if (animator != null)
        {
            animator.SetBool("IsHolding", false);
            yield return new WaitForSeconds(0.05f);
            animator.SetTrigger("Release");
        }

        yield return new WaitForSeconds(impactDelayAfterRelease);
        CutToCamera(vcamFollow);

        float clampedPower = Mathf.Clamp01(smoothedDragPower);
        float baseLaunchSpeed = GameLevelManager.Instance.GetTotalLaunchSpeed();
        float finalPower = Mathf.Pow(clampedPower, 1.5f);
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

        weapon.DOKill();
        weapon.SetParent(null);
        weapon.DOMove(socket.position, 0.5f).SetEase(Ease.InOutSine);
        weapon.DORotateQuaternion(socket.rotation, 0.5f).SetEase(Ease.InOutSine);
        yield return new WaitForSeconds(0.5f);

        weapon.SetParent(socket);
        weapon.localPosition = Vector3.zero;
        weapon.localRotation = Quaternion.identity;
    }

    public IEnumerator PanToTarget(CinemachineVirtualCamera activeCam, Transform targetTransform, float duration)
    {
        if (activeCam != null && targetTransform != null)
        {
            activeCam.LookAt = targetTransform;
            activeCam.Follow = targetTransform;

            CutToCamera(activeCam);
            yield return new WaitForSeconds(duration);
        }
    }

    public void ResetToIdleCamera()
    {
        CutToCamera(vcamIdle);
    }

    private void CutToCamera(CinemachineVirtualCamera targetCam)
    {
        if (vcamIdle) vcamIdle.Priority = 10;
        if (vcamMinigame) vcamMinigame.Priority = 10;
        if (vcamFollow) vcamFollow.Priority = 10;
        if (vcamBoulderCloseUp) vcamBoulderCloseUp.Priority = 10;
        if (vcamHandCloseUp) vcamHandCloseUp.Priority = 10;

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
        if (skinManager != null && skinManager.giantWeaponInScene != null) skinManager.giantWeaponInScene.DOKill();
        if (upgradePanel != null) upgradePanel.transform.DOKill();
    }
}