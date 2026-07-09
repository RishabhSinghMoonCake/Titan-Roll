using Cinemachine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
    [Header("Hit Effect")]
    public GameObject hitParticlePrefab;

    [Header("Back Button UI")]
    [Tooltip("Assign the CanvasGroup attached to your Back Button UI object.")]
    public CanvasGroup backButtonCanvasGroup;
    public float backButtonFadeDuration = 0.3f;

    // --- NEW: Instant kill-switch flag to stop unintended launches ---
    private bool _isAborted = false;

    // --- THE FIX 1: Dynamic Animator Property ---
    // This guarantees we NEVER cache a destroyed animator if GameLevelManager swaps the skin!
    private Animator CurrentAnimator
    {
        get { return skinManager != null ? skinManager.currentActiveAnimator : null; }
    }

    private void Start()
    {
        InitializeNewRun();
    }

    public void InitializeNewRun()
    {
        _isAborted = false; // Reset abort flag on boot
        Time.timeScale = 1.0f;
        smoothedDragPower = 0f;
        targetDragPower = 0f;
        velocity = 0f;

        if (backButtonCanvasGroup != null)
        {
            backButtonCanvasGroup.DOKill();
            backButtonCanvasGroup.alpha = 0f;
            backButtonCanvasGroup.blocksRaycasts = false;
            backButtonCanvasGroup.interactable = false;
        }

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
        StartCoroutine(StartRunAfterPrewarm());
    }

    private IEnumerator StartRunAfterPrewarm()
    {
        yield return StartCoroutine(PrewarmCamerasRoutine());
        yield return StartCoroutine(PreLaunchSequence());
    }

    private bool _camerasPrewarmed = false;

    private IEnumerator PrewarmCamerasRoutine()
    {
        if (_camerasPrewarmed) yield break;
        _camerasPrewarmed = true;

        CinemachineVirtualCamera[] allCams = new CinemachineVirtualCamera[]
        {
            vcamIdle,
            vcamMinigame,
            vcamFollow,
            vcamBoulderCloseUp,
            vcamHandCloseUp
        };

        foreach (var cam in allCams)
        {
            if (cam != null)
            {
                CutToCamera(cam);
                cam.PreviousStateIsValid = false;
                yield return null;
            }
        }

        CutToCamera(vcamIdle);
        if (vcamIdle != null) vcamIdle.PreviousStateIsValid = false;
        yield return null;
    }

    private bool IsTouchOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        if (EventSystem.current.IsPointerOverGameObject()) return true;

        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
        {
            foreach (var touch in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
            {
                if (EventSystem.current.IsPointerOverGameObject(touch.touchId)) return true;
                if (EventSystem.current.IsPointerOverGameObject((int)touch.touchId)) return true;
            }
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private IEnumerator PreLaunchSequence()
    {
        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);
        AlignCharacterToBoulder();

        bool isFirstTimeBoot = PlayerPrefs.GetInt("HasPlayedBefore", 0) == 0;

        if (isFirstTimeBoot)
        {
            PlayerPrefs.SetInt("HasPlayedBefore", 1);
            PlayerPrefs.Save();

            if (upgradePanel != null) upgradePanel.SetActive(false);
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);
            if (LevelText != null) LevelText.gameObject.SetActive(false);
            if (backButtonCanvasGroup != null) backButtonCanvasGroup.gameObject.SetActive(false);

            if (skinManager != null && skinManager.giantWeaponInScene != null && skinManager.currentWeaponSocket != null)
            {
                Transform weapon = skinManager.giantWeaponInScene;
                Transform socket = skinManager.currentWeaponSocket;
                weapon.SetParent(socket);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.identity;
            }

            if (CurrentAnimator != null)
            {
                CurrentAnimator.ResetTrigger("Release");
                CurrentAnimator.SetBool("IsHolding", false);
                CurrentAnimator.SetTrigger("StartMinigame");
            }

            yield return new WaitForSeconds(0.6f);
        }
        else
        {
            // --- NORMAL PLAYTHROUGH: START AT IDLE ---
            CutToCamera(vcamIdle);

            float safeStartTime = Time.time + 0.4f;
            bool waitingForStart = true;
            while (waitingForStart)
            {
                if (Time.time >= safeStartTime && Touch.activeTouches.Count > 0)
                {
                    var touch = Touch.activeTouches[0];
                    // Accept Began, Moved, or Stationary so tap & holds are not ignored
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began ||
                        touch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                        touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    {
                        if (!IsTouchOverUI(touch.screenPosition))
                        {
                            waitingForStart = false;
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

            if (LevelText != null && LevelText.gameObject.activeSelf)
            {
                LevelText.transform.DOKill();
                LevelText.DOFade(0f, 0.2f);
                LevelText.transform.DOScale(Vector3.zero, 0.25f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => LevelText.gameObject.SetActive(false));
            }

            yield return StartCoroutine(FlyWeaponToHand());

            if (CurrentAnimator != null)
            {
                CurrentAnimator.ResetTrigger("Release");
                CurrentAnimator.SetBool("IsHolding", false);
                CurrentAnimator.SetTrigger("StartMinigame");
            }

            // Reduced wait time for a snappier transition!
            yield return new WaitForSeconds(0.3f);
        }

        // --- PHASE 3: DRAG MINIGAME INITIALIZATION ---
        CutToCamera(vcamMinigame);

        if (timingMinigamePanel) timingMinigamePanel.SetActive(true);
        if (timingSlider) timingSlider.value = 0f;

        if (backButtonCanvasGroup != null)
        {
            backButtonCanvasGroup.DOKill();
            backButtonCanvasGroup.blocksRaycasts = true;
            backButtonCanvasGroup.interactable = true;
            backButtonCanvasGroup.DOFade(1f, backButtonFadeDuration);
        }

        if (powerPercentageText != null)
        {
            powerPercentageText.gameObject.SetActive(true);
            powerPercentageText.text = "0%";
        }

        if (handTutorialUI != null) handTutorialUI.PlayTutorial();

        bool isDragging = false;
        Vector2 startTouchPos = Vector2.zero;
        float dpiScale = Screen.dpi > 0 ? Screen.dpi / 160f : Screen.height / 1080f;
        float actualMaxDrag = Mathf.Max(10f, maxDragPixels * dpiScale);

        // Force a tiny 0.1s deadzone instead of 0.4s to prevent laggy input tracking
        float safeInputTime = Time.time + 0.1f;
        UnityEngine.InputSystem.EnhancedTouch.Finger activeFinger = null;

        while (true)
        {
            if (_isAborted) 
            {
                if (AudioManager.Instance != null) AudioManager.Instance.Stop("Stretch");
                yield break; 
            }

            if (Time.time >= safeInputTime)
            {
                var touches = Touch.activeTouches;

                if (!isDragging && touches.Count > 0)
                {
                    foreach (var touch in touches)
                    {
                        // Accept Began, Moved, AND Stationary! 
                        // If the player starts swiping before the camera arrives, we instantly grab it here!
                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began ||
                            touch.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                            touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                        {
                            if (IsTouchOverUI(touch.screenPosition)) continue;

                            isDragging = true;
                            activeFinger = touch.finger;
                            startTouchPos = touch.screenPosition;

                            if (AudioManager.Instance != null) AudioManager.Instance.Play("Stretch");

                            if (backButtonCanvasGroup != null)
                            {
                                backButtonCanvasGroup.blocksRaycasts = false;
                                backButtonCanvasGroup.DOFade(0f, backButtonFadeDuration);
                            }

                            if (handTutorialUI != null) handTutorialUI.StopTutorial();

                            if (CurrentAnimator != null) CurrentAnimator.SetBool("IsHolding", true);
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

            // --- THE FIX: Eliminate Input Lag & Desync ---
            // Changed from slow SmoothDamp to a high-speed Lerp (30f) to instantly lock the visual to the finger.
            smoothedDragPower = Mathf.Lerp(smoothedDragPower, targetDragPower, Time.deltaTime * 30f);

            if (isDragging && AudioManager.Instance != null)
            {
                // Maps 0% -> 100% power to a pitch range of 0.8 -> 1.5
                float currentPitch = Mathf.Lerp(0.8f, 1.5f, smoothedDragPower);
                AudioManager.Instance.ModulateSound("Stretch", currentPitch);
            }

            if (CurrentAnimator != null) CurrentAnimator.SetFloat("WindupPower", smoothedDragPower);

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
        if (AudioManager.Instance != null) AudioManager.Instance.Stop("Stretch");
        if (_isAborted) yield break;

        // --- NEW: Snap to raw input on release! ---
        // If the player swipes super fast and releases, this guarantees the launch logic 
        // uses their EXACT raw finger position, eliminating launch power desyncs.
        smoothedDragPower = targetDragPower;

        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);
        if (powerPercentageText != null) powerPercentageText.gameObject.SetActive(false);

        if (skinManager.giantWeaponInScene != null)
            skinManager.giantWeaponInScene.localRotation = Quaternion.identity;

        bool isMaxPower = smoothedDragPower >= 0.99f;

        if (CurrentAnimator != null)
        {
            CurrentAnimator.SetBool("IsHolding", false);

            if (isMaxPower)
            {
                Time.timeScale = 0.7f;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            else
            {
                yield return new WaitForSeconds(0.05f);
            }

            CurrentAnimator.SetTrigger("Release");
        }

        if (_isAborted) yield break;

        if (isMaxPower)
        {
            yield return new WaitForSeconds(impactDelayAfterRelease);

            Time.timeScale = 0.01f;

            if (hitParticlePrefab != null && skinManager != null && skinManager.currentWeaponSocket != null)
            {
                Vector3 handPos = skinManager.currentWeaponSocket.position;
                Vector3 spawnPos = handPos;

                if (boulder != null)
                {
                    Collider boulderCol = boulder.GetComponent<Collider>();
                    if (boulderCol != null)
                    {
                        Vector3 boulderSurface = boulderCol.ClosestPoint(handPos);
                        spawnPos = Vector3.Lerp(handPos, boulderSurface, 0.5f);
                    }
                    else
                    {
                        spawnPos = Vector3.Lerp(handPos, boulder.transform.position, 0.5f);
                    }
                }

                if (ObjectPooler.Instance != null) ObjectPooler.Instance.Spawn(hitParticlePrefab, spawnPos, Quaternion.identity);
                else Instantiate(hitParticlePrefab, spawnPos, Quaternion.identity);
            }

            if (CustomCameraShaker.Instance != null) CustomCameraShaker.Instance.Shake(ShakeType.Short);
            AudioManager.Instance?.Play("Slap");
            yield return new WaitForSecondsRealtime(4f / 60f);

            Time.timeScale = 1.0f;
        }
        else
        {
            AudioManager.Instance?.Play("Slap");
            yield return new WaitForSeconds(impactDelayAfterRelease);
        }

        if (_isAborted) yield break;

        CutToCamera(vcamFollow);

        float clampedPower = Mathf.Clamp01(smoothedDragPower);
        float baseLaunchSpeed = GameLevelManager.Instance.GetTotalLaunchSpeed();
        float finalPower = Mathf.Pow(clampedPower, 1.5f);
        float finalSpeed = Mathf.Lerp(baseLaunchSpeed * 0.25f, baseLaunchSpeed, finalPower);

        FindObjectOfType<DynamicBoulderCamera>()?.TriggerLaunchSequence();

        if (float.IsNaN(finalSpeed) || float.IsInfinity(finalSpeed) || finalSpeed <= 0f) finalSpeed = 60f;

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

        // Sped up to make the start of a run feel radically faster
        weapon.DOMove(socket.position, 0.3f).SetEase(Ease.OutQuad);
        weapon.DORotateQuaternion(socket.rotation, 0.3f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.3f);

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

    public void OnBackButtonPressed()
    {
        _isAborted = true;
        StopAllCoroutines();
        if (AudioManager.Instance != null) AudioManager.Instance.Stop("Stretch");
        if (backButtonCanvasGroup != null)
        {
            backButtonCanvasGroup.blocksRaycasts = false;
            backButtonCanvasGroup.interactable = false;
        }

        StartCoroutine(SmoothReloadRoutine());
    }

    private IEnumerator SmoothReloadRoutine()
    {
        Time.timeScale = 1.0f;

        if (boulder != null)
        {
            var rb = boulder.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        if (backButtonCanvasGroup != null) backButtonCanvasGroup.DOFade(0f, 0.3f);
        if (powerPercentageText != null) powerPercentageText.DOFade(0f, 0.2f);
        if (handTutorialUI != null) handTutorialUI.StopTutorial();

        if (timingMinigamePanel != null)
        {
            var panelGroup = timingMinigamePanel.GetComponent<CanvasGroup>();
            if (panelGroup != null) panelGroup.DOFade(0f, 0.3f);
            else timingMinigamePanel.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
        }

        yield return new WaitForSecondsRealtime(0.3f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

    private struct CamDefaultData
    {
        public bool isCached;
        public Vector3 offset;
        public float distance;
    }
    private CamDefaultData _minigameCamData;
    private CamDefaultData _closeUpCamData;

    public void UpdateCameraScales(float boulderScale)
    {
        ScaleVirtualCamera(vcamMinigame, ref _minigameCamData, boulderScale);
        ScaleVirtualCamera(vcamBoulderCloseUp, ref _closeUpCamData, boulderScale);
    }

    private void ScaleVirtualCamera(CinemachineVirtualCamera vcam, ref CamDefaultData data, float scale)
    {
        if (vcam == null) return;

        if (!data.isCached)
        {
            var t = vcam.GetCinemachineComponent<CinemachineTransposer>();
            if (t != null) data.offset = t.m_FollowOffset;

            var f = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (f != null) data.distance = f.m_CameraDistance;

            var tp = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (tp != null) data.distance = tp.CameraDistance;

            data.isCached = true;
        }

        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null) transposer.m_FollowOffset = data.offset * Mathf.Sqrt(scale);

        var framing = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing != null) framing.m_CameraDistance = data.distance * Mathf.Sqrt(scale);

        var thirdPerson = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        if (thirdPerson != null) thirdPerson.CameraDistance = data.distance * Mathf.Sqrt(scale);
    }

    private void OnDestroy()
    {
        if (skinManager != null && skinManager.giantWeaponInScene != null) skinManager.giantWeaponInScene.DOKill();
        if (upgradePanel != null) upgradePanel.transform.DOKill();
    }
}