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
        StartCoroutine(StartRunAfterPrewarm());
    }

    private IEnumerator StartRunAfterPrewarm()
    {
        // This will only take ~5 frames on initial load, and 0 frames on quick retries!
        yield return StartCoroutine(PrewarmCamerasRoutine());

        // Once cameras are warm, kick off the normal sequence
        yield return StartCoroutine(PreLaunchSequence());
    }

    private bool _camerasPrewarmed = false;

    /// <summary>
    /// Wakes up every Cinemachine camera for 1 frame so Unity caches shaders, LODs, shadows, and damping math.
    /// </summary>
    private IEnumerator PrewarmCamerasRoutine()
    {
        // Only run this heavy prewarm once per scene load!
        if (_camerasPrewarmed) yield break;
        _camerasPrewarmed = true;


        // Put all cameras into an array for easy looping
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
                // 1. Force this camera to be the highest priority
                CutToCamera(cam);

                // 2. Tell Cinemachine to instantly snap to its target without damping lag
                cam.PreviousStateIsValid = false;

                // 3. Wait 1 frame so Unity actually renders this view and caches shadows/materials
                yield return null;
            }
        }

        // Return to the starting Idle camera cleanly
        CutToCamera(vcamIdle);
        if (vcamIdle != null) vcamIdle.PreviousStateIsValid = false;

        // Give the brain one final frame to settle
        yield return null;

    }

    private bool IsTouchOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        // 1. Check native Unity UI pointer (catches mouse and standard touch)
        if (EventSystem.current.IsPointerOverGameObject()) return true;

        // 2. Explicitly check active mobile touch IDs
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
        {
            foreach (var touch in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
            {
                if (EventSystem.current.IsPointerOverGameObject(touch.touchId)) return true;
                if (EventSystem.current.IsPointerOverGameObject((int)touch.touchId)) return true;
            }
        }

        // 3. Fallback to RaycastAll
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private IEnumerator PreLaunchSequence()
    {
        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);  
        AlignCharacterToBoulder();  

        var animator = skinManager.currentActiveAnimator;  

        // --- CHECK IF THIS IS THE VERY FIRST TIME PLAYING ---
        bool isFirstTimeBoot = PlayerPrefs.GetInt("HasPlayedBefore", 0) == 0;

        if (isFirstTimeBoot)
        {
            // 1. Mark that they have now played, so subsequent runs start at Idle
            PlayerPrefs.SetInt("HasPlayedBefore", 1);
            PlayerPrefs.Save();

            // 2. Hide starting UI immediately
            if (upgradePanel != null) upgradePanel.SetActive(false);  
            if (tapToPlayText != null) tapToPlayText.gameObject.SetActive(false);  
            if (LevelText != null) LevelText.gameObject.SetActive(false);  

            // 3. Instantly snap weapon to hand without the 0.5s animation wait
            if (skinManager != null && skinManager.giantWeaponInScene != null && skinManager.currentWeaponSocket != null)
            {
                Transform weapon = skinManager.giantWeaponInScene;  
                Transform socket = skinManager.currentWeaponSocket;  
                weapon.SetParent(socket);  
                weapon.localPosition = Vector3.zero;  
                weapon.localRotation = Quaternion.identity;  
            }

            if (animator != null) animator.SetTrigger("StartMinigame");  
        }
        else
        {
            // --- NORMAL PLAYTHROUGH: START AT IDLE ---
            CutToCamera(vcamIdle);

            // THE FIX: Ignore touch inputs for 0.4s after boot so scene reloads don't auto-start!
            float safeStartTime = Time.time + 0.4f;
            //PHASE 1
            bool waitingForStart = true;
            while (waitingForStart)
            {
                if (Time.time >= safeStartTime && Touch.activeTouches.Count > 0)
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

            if (LevelText != null && LevelText.gameObject.activeSelf)  
            {
                LevelText.transform.DOKill();  
                LevelText.DOFade(0f, 0.2f);  
                LevelText.transform.DOScale(Vector3.zero, 0.25f) 
                    .SetEase(Ease.InBack) 
                    .OnComplete(() => LevelText.gameObject.SetActive(false));  
            }

            // PHASE 2: FLY WEAPON TO HAND
            yield return StartCoroutine(FlyWeaponToHand());  

            if (animator != null) animator.SetTrigger("StartMinigame");  
            yield return new WaitForSeconds(0.5f);  
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
        float safeInputTime = Time.time + inputDeadZoneDelay;  
        UnityEngine.InputSystem.EnhancedTouch.Finger activeFinger = null;  

        while (true)  
        {
            if (_isAborted) yield break;
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

                            if (backButtonCanvasGroup != null)
                            {
                                backButtonCanvasGroup.blocksRaycasts = false;
                                backButtonCanvasGroup.DOFade(0f, backButtonFadeDuration);
                            }

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
        if (_isAborted) yield break;
        if (timingMinigamePanel) timingMinigamePanel.SetActive(false);  
        if (powerPercentageText != null) powerPercentageText.gameObject.SetActive(false);  

        if (skinManager.giantWeaponInScene != null)  
            skinManager.giantWeaponInScene.localRotation = Quaternion.identity;

        bool isMaxPower = smoothedDragPower >= 0.99f;

        if (animator != null)
        {
            animator.SetBool("IsHolding", false);

            if (isMaxPower)
            {
                // 1. 50% FASTER SLOW-MO: Increased from 0.35f to 0.70f speed!
                Time.timeScale = 0.7f;
                yield return new WaitForSecondsRealtime(0.05f);
            }
            else
            {
                yield return new WaitForSeconds(0.05f);
            }

            animator.SetTrigger("Release");
        }
        if (_isAborted) yield break;
        if (isMaxPower)
        {
            // --- 100% POWER CRITICAL HIT SEQUENCE ---

            // Wait for the arm animation to reach the boulder (now 50% faster!)
            yield return new WaitForSeconds(impactDelayAfterRelease);

            // 2. 3-4 FRAME SKIP HIT-STOP: Freeze time almost completely
            Time.timeScale = 0.01f;

            // 3. EXACT CONTACT POINT SPAWN
            if (hitParticlePrefab != null && skinManager != null && skinManager.currentWeaponSocket != null)
            {
                Vector3 handPos = skinManager.currentWeaponSocket.position;
                Vector3 spawnPos = handPos;

                if (boulder != null)
                {
                    Collider boulderCol = boulder.GetComponent<Collider>();
                    if (boulderCol != null)
                    {
                        // Get the exact impact point on the boulder's physical surface
                        Vector3 boulderSurface = boulderCol.ClosestPoint(handPos);
                        // Place particle directly between the hand and the boulder surface!
                        spawnPos = Vector3.Lerp(handPos, boulderSurface, 0.5f);
                    }
                    else
                    {
                        spawnPos = Vector3.Lerp(handPos, boulder.transform.position, 0.5f);
                    }
                }

                if (ObjectPooler.Instance != null)
                {
                    ObjectPooler.Instance.Spawn(hitParticlePrefab, spawnPos, Quaternion.identity);
                }
                else
                {
                    Instantiate(hitParticlePrefab, spawnPos, Quaternion.identity);
                }
            }

            // 4. CAMERA SHAKE AT IMPACT
            CinemachineImpulseSource impulse = boulder != null ? boulder.GetComponent<CinemachineImpulseSource>() : GetComponent<CinemachineImpulseSource>();
            if (impulse != null)
            {
                impulse.GenerateImpulse(1.2f); // Max intensity shake
            }

            // Hold the freeze for exactly 4 frames at 60 FPS (~0.066 real seconds)
            yield return new WaitForSecondsRealtime(4f / 60f);

            // Snap time back to normal speed for the launch
            Time.timeScale = 1.0f;
        }
        else
        {
            // --- STANDARD LAUNCH (< 100% POWER) ---
            yield return new WaitForSeconds(impactDelayAfterRelease);
        }
        if (_isAborted) yield break;
        CutToCamera(vcamFollow);


        float clampedPower = Mathf.Clamp01(smoothedDragPower);  
        float baseLaunchSpeed = GameLevelManager.Instance.GetTotalLaunchSpeed();  
        float finalPower = Mathf.Pow(clampedPower, 1.5f);  
        float finalSpeed = Mathf.Lerp(baseLaunchSpeed * 0.25f, baseLaunchSpeed, finalPower);  

        FindObjectOfType<DynamicBoulderCamera>()?.TriggerLaunchSequence();

        if (float.IsNaN(finalSpeed) || float.IsInfinity(finalSpeed) || finalSpeed <= 0f)
        {
            finalSpeed = 60f;
        }
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

    /// <summary>
    /// Hook this public method directly to your UI Back Button's OnClick event in the Inspector!
    /// </summary>
    public void OnBackButtonPressed()
    {
        _isAborted = true;
        // 1. Instantly kill the minigame while(true) loop so no lingering touches trigger a launch!
        StopAllCoroutines();
        // 1. Immediately prevent double-clicking the button
        if (backButtonCanvasGroup != null)
        {
            backButtonCanvasGroup.blocksRaycasts = false;
            backButtonCanvasGroup.interactable = false;
        }

        // 2. Start the smooth reload sequence
        StartCoroutine(SmoothReloadRoutine());
    }

    private IEnumerator SmoothReloadRoutine()
    {
        Time.timeScale = 1.0f;

        // 2. Lock boulder physics instantly so it cannot fall or trigger Game Over
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

        // 3. Fade out UI
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

    // --- NEW: Caching structures for dynamic camera scaling ---
    private struct CamDefaultData
    {
        public bool isCached;
        public Vector3 offset;
        public float distance;
    }
    private CamDefaultData _minigameCamData;
    private CamDefaultData _closeUpCamData;

    /// <summary>
    /// Scales the minigame and close-up camera distances proportionally to the boulder's physical size.
    /// </summary>
    public void UpdateCameraScales(float boulderScale)
    {
        ScaleVirtualCamera(vcamMinigame, ref _minigameCamData, boulderScale);
        ScaleVirtualCamera(vcamBoulderCloseUp, ref _closeUpCamData, boulderScale);
    }

    private void ScaleVirtualCamera(CinemachineVirtualCamera vcam, ref CamDefaultData data, float scale)
    {
        if (vcam == null) return;

        // 1. Automatically cache the original Level 1 defaults the first time this runs
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

        // 2. Multiply the offset/distance by the boulder's current scale
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