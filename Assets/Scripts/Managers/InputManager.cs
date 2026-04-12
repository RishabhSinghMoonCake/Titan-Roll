using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Joystick Settings")]
    [Tooltip("Distance in pixels from the initial tap to reach full 100% steering")]
    public float joystickRadius = 250f;
    public bool invertSteering = false;

    [Header("Smoothness & Mass Physics")]
    public float baseSteeringSmoothness = 10f;
    public float massEffectWeight = 0.003f;

    [HideInInspector] public float currentBoulderMass = 100f;

    public event Action OnLaunchTap;
    public float SteeringInput { get; private set; }

    private Vector2 _joystickCenter;
    private float _targetSteering;
    private bool _isInteracting;

    // NEW: We lock onto the exact finger that touched the screen, ignoring the index number!
    private Finger _activeFinger;

    public float ExternalJoystickInput { get; set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        TouchSimulation.Enable();

        Touch.onFingerDown += HandleFingerDown;
        Touch.onFingerMove += HandleFingerMove;
        Touch.onFingerUp += HandleFingerUp;
    }

    void OnDisable()
    {
        Touch.onFingerDown -= HandleFingerDown;
        Touch.onFingerMove -= HandleFingerMove;
        Touch.onFingerUp -= HandleFingerUp;

        TouchSimulation.Disable();
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        float minPlayableSmoothness = 6.0f;
        float massPenalty = currentBoulderMass * massEffectWeight;

        float dynamicSmoothness = Mathf.Clamp(baseSteeringSmoothness - massPenalty, minPlayableSmoothness, baseSteeringSmoothness);

        float finalTarget = Mathf.Clamp(_targetSteering + ExternalJoystickInput, -1f, 1f);

        // THE FIX: Framerate-Independent Lerp!
        // This guarantees your steering feels EXACTLY the same at 15 FPS or 120 FPS.
        float lerpFactor = 1f - Mathf.Exp(-dynamicSmoothness * Time.deltaTime);
        SteeringInput = Mathf.Lerp(SteeringInput, finalTarget, lerpFactor);
    }

    private void HandleFingerDown(Finger finger)
    {
        // If we are already tracking a finger, ignore any new fingers touching the screen
        if (_isInteracting) return;
        if (IsPointerOverUI(finger.screenPosition)) return;

        _isInteracting = true;
        _activeFinger = finger; // Lock onto this exact finger

        _joystickCenter = finger.screenPosition;
        _targetSteering = 0f;

        OnLaunchTap?.Invoke();
    }

    private void HandleFingerMove(Finger finger)
    {
        // Only process movement if it's the specific finger we locked onto
        if (!_isInteracting || finger != _activeFinger) return;

        float deltaX = finger.screenPosition.x - _joystickCenter.x;

        // --- THE FLOATING ANCHOR FIX ---
        // If the player drags past the joystick radius, pull the center anchor with them!
        // This completely eliminates the "dragging right but moving left" illusion.
        if (Mathf.Abs(deltaX) > joystickRadius)
        {
            _joystickCenter.x = finger.screenPosition.x - (Mathf.Sign(deltaX) * joystickRadius);
            deltaX = Mathf.Sign(deltaX) * joystickRadius;
        }

        float sensitivity = invertSteering ? -1f : 1f;
        float safeRadius = Mathf.Max(joystickRadius, 1f);
        float rawSteer = (deltaX / safeRadius) * sensitivity;

        _targetSteering = Mathf.Clamp(rawSteer, -1f, 1f);
    }

    private void HandleFingerUp(Finger finger)
    {
        if (!_isInteracting || finger != _activeFinger) return;

        _isInteracting = false;
        _activeFinger = null;
        _targetSteering = 0f;
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