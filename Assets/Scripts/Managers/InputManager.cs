using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using System.Collections.Generic;
using DG.Tweening; // Added for visual fading

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Logical Joystick Settings")]
    [Tooltip("Distance in pixels from the initial tap to reach full 100% steering")]
    public float joystickRadius = 250f;
    public bool invertSteering = false;

    [Header("Visual Joystick GUI")]
    public CanvasGroup joystickCanvasGroup;
    public RectTransform outerCircle;
    public RectTransform innerKnob;

    [Tooltip("Size of the outer circle (Width & Height)")]
    public float outerCircleSize = 300f;
    [Tooltip("Size of the inner knob (Width & Height)")]
    public float innerKnobSize = 100f;
    [Tooltip("Max visual distance the inner knob can travel from the center")]
    public float knobTravelRadius = 100f;
    [Tooltip("How fast the joystick fades in and out")]
    public float fadeDuration = 0.2f;

    [Header("Smoothness & Mass Physics")]
    public float baseSteeringSmoothness = 10f;
    public float massEffectWeight = 0.003f;

    [HideInInspector] public float currentBoulderMass = 100f;

    public event Action OnLaunchTap;
    public float SteeringInput { get; private set; }
    public float ExternalJoystickInput { get; set; }

    private Vector2 _joystickCenter;
    private float _targetSteering;
    private bool _isInteracting;
    private Finger _activeFinger;
    private bool _isJoystickVisible = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        // 1. MUST boot up the system BEFORE subscribing to any events
        EnhancedTouchSupport.Enable();

#if UNITY_EDITOR
        TouchSimulation.Enable();
#endif

        // 2. Now it is safe to subscribe
        Touch.onFingerDown += HandleFingerDown;
        Touch.onFingerMove += HandleFingerMove;
        Touch.onFingerUp += HandleFingerUp;
    }

    void OnDisable()
    {
        // 1. Unsubscribe first
        Touch.onFingerDown -= HandleFingerDown;
        Touch.onFingerMove -= HandleFingerMove;
        Touch.onFingerUp -= HandleFingerUp;

        // 2. Safely shut down the systems
#if UNITY_EDITOR
        TouchSimulation.Disable();
#endif
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        // UI visual setup remains safely in Start
        if (joystickCanvasGroup != null)
        {
            joystickCanvasGroup.alpha = 0f;
            joystickCanvasGroup.blocksRaycasts = false;
            joystickCanvasGroup.interactable = false;
        }
        if (outerCircle != null) outerCircle.sizeDelta = new Vector2(outerCircleSize, outerCircleSize);
        if (innerKnob != null) innerKnob.sizeDelta = new Vector2(innerKnobSize, innerKnobSize);
    }

    private void Update()
    {
        float minPlayableSmoothness = 6.0f;
        float massPenalty = currentBoulderMass * massEffectWeight;
        float dynamicSmoothness = Mathf.Clamp(baseSteeringSmoothness - massPenalty, minPlayableSmoothness, baseSteeringSmoothness);
        float finalTarget = Mathf.Clamp(_targetSteering + ExternalJoystickInput, -1f, 1f);

        float lerpFactor = 1f - Mathf.Exp(-dynamicSmoothness * Time.deltaTime);
        SteeringInput = Mathf.Lerp(SteeringInput, finalTarget, lerpFactor);

        // If holding touch during a cutscene, gracefully fade joystick in the moment boulder launches
        if (_isInteracting && !_isJoystickVisible && IsBoulderPlayable())
        {
            ShowJoystick(_activeFinger.screenPosition);
        }
    }

    private void HandleFingerDown(Finger finger)
    {
        if (_isInteracting) return;
        if (IsPointerOverUI(finger.screenPosition)) return;

        _isInteracting = true;
        _activeFinger = finger;
        _joystickCenter = finger.screenPosition;
        _targetSteering = 0f;

        // Only show visually if the game has started (boulder is rolling)
        if (IsBoulderPlayable())
        {
            ShowJoystick(_joystickCenter);
        }

        OnLaunchTap?.Invoke();
    }

    private void HandleFingerMove(Finger finger)
    {
        if (!_isInteracting || finger != _activeFinger) return;

        float deltaX = finger.screenPosition.x - _joystickCenter.x;

        // --- THE FLOATING ANCHOR FIX ---
        if (Mathf.Abs(deltaX) > joystickRadius)
        {
            _joystickCenter.x = finger.screenPosition.x - (Mathf.Sign(deltaX) * joystickRadius);
            deltaX = Mathf.Sign(deltaX) * joystickRadius;
        }

        float sensitivity = invertSteering ? -1f : 1f;
        float safeRadius = Mathf.Max(joystickRadius, 1f);
        float rawSteer = (deltaX / safeRadius) * sensitivity;

        _targetSteering = Mathf.Clamp(rawSteer, -1f, 1f);

        // --- UPDATE VISUALS ---
        if (_isJoystickVisible && joystickCanvasGroup != null)
        {
            outerCircle.position = _joystickCenter;

            // Allow the inner knob to visually drag in 2D space, clamped to the travel radius
            Vector2 visualDelta = finger.screenPosition - _joystickCenter;
            if (visualDelta.magnitude > knobTravelRadius)
            {
                visualDelta = visualDelta.normalized * knobTravelRadius;
            }
            innerKnob.position = _joystickCenter + visualDelta;
        }
    }

    private void HandleFingerUp(Finger finger)
    {
        if (!_isInteracting || finger != _activeFinger) return;

        _isInteracting = false;
        _activeFinger = null;
        _targetSteering = 0f;

        HideJoystick();
    }

    // --- VISUAL & STATE HELPERS ---

    private bool IsBoulderPlayable()
    {
        if (ArcadeBoulder.Instance == null) return false;
        Rigidbody rb = ArcadeBoulder.Instance.GetComponent<Rigidbody>();
        // Only return true when the boulder is no longer kinematic (Main game mode)
        return rb != null && !rb.isKinematic;
    }

    private void ShowJoystick(Vector2 position)
    {
        if (joystickCanvasGroup == null) return;

        _isJoystickVisible = true;
        joystickCanvasGroup.DOKill();

        outerCircle.position = position;
        innerKnob.position = position;

        joystickCanvasGroup.DOFade(1f, fadeDuration);
    }

    private void HideJoystick()
    {
        if (joystickCanvasGroup == null) return;

        _isJoystickVisible = false;
        joystickCanvasGroup.DOKill();
        joystickCanvasGroup.DOFade(0f, fadeDuration);
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }
}