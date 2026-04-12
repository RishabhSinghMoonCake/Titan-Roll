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

        SteeringInput = Mathf.Lerp(SteeringInput, finalTarget, Time.deltaTime * dynamicSmoothness);
    }

    private void HandleFingerDown(Finger finger)
    {
        if (Touch.activeTouches.Count > 1) return;
        if (IsPointerOverUI(finger.screenPosition)) return;

        _isInteracting = true;
        _joystickCenter = finger.screenPosition;
        _targetSteering = 0f;

        OnLaunchTap?.Invoke();
    }

    private void HandleFingerMove(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        float deltaX = finger.screenPosition.x - _joystickCenter.x;
        float sensitivity = invertSteering ? -1f : 1f;

        // --- THE NaN FIX ---
        // Guaranteed to never divide by zero, even if the inspector resets to 0
        float safeRadius = Mathf.Max(joystickRadius, 1f);
        float rawSteer = (deltaX / safeRadius) * sensitivity;

        _targetSteering = Mathf.Clamp(rawSteer, -1f, 1f);
    }

    private void HandleFingerUp(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        _isInteracting = false;
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