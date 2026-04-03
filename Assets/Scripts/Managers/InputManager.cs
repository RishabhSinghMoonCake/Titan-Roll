using System;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [Tooltip("Base speed at which the boulder responds to your steering")]
    public float baseSteeringSmoothness = 10f;
    [Tooltip("How much the boulder's mass slows down steering (Higher = Harder to move heavy boulders)")]
    public float massEffectWeight = 0.01f;

    // Set this from GameLevelManager whenever the player buys a size/mass upgrade
    [HideInInspector] public float currentBoulderMass = 100f;

    // --- EVENTS ---
    public event Action OnLaunchTap;

    // --- PUBLIC READ-ONLY ---
    public float SteeringInput { get; private set; } // -1 (Left) to 1 (Right)

    // --- PRIVATE STATE ---
    private Vector2 _joystickCenter;
    private float _targetSteering;
    private bool _isInteracting;

    // External Input (Joystick) can write to this
    public float ExternalJoystickInput { get; set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += HandleFingerDown;
        Touch.onFingerMove += HandleFingerMove;
        Touch.onFingerUp += HandleFingerUp;
    }

    void OnDisable()
    {
        Touch.onFingerDown -= HandleFingerDown;
        Touch.onFingerMove -= HandleFingerMove;
        Touch.onFingerUp -= HandleFingerUp;
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        // 1. Calculate dynamic smoothness based on mass
        // As mass increases, the divisor gets larger, making the smoothness slower/heavier
        float dynamicSmoothness = baseSteeringSmoothness / (1f + (currentBoulderMass * massEffectWeight));

        // 2. Combine our touch target with any external joystick input
        float finalTarget = Mathf.Clamp(_targetSteering + ExternalJoystickInput, -1f, 1f);

        // 3. Smoothly interpolate the actual steering towards the target
        SteeringInput = Mathf.Lerp(SteeringInput, finalTarget, Time.deltaTime * dynamicSmoothness);
    }

    private void HandleFingerDown(Finger finger)
    {
        if (Touch.activeTouches.Count > 1) return;

        // Ignore the tap if the user is touching a UI element (e.g., a button)
        // UPDATED: Pass the exact screen position
        if (IsPointerOverUI(finger.screenPosition)) return;

        _isInteracting = true;

        // Anchor the center of our virtual joystick
        _joystickCenter = finger.screenPosition;
        _targetSteering = 0f;

        // Trigger Launch
        OnLaunchTap?.Invoke();
    }

    private void HandleFingerMove(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        // Calculate distance from the original tap position
        float deltaX = finger.screenPosition.x - _joystickCenter.x;

        float sensitivity = invertSteering ? -1f : 1f;

        // Normalize the input based on our defined joystick radius
        float rawSteer = (deltaX / joystickRadius) * sensitivity;

        // Clamp the target so we don't steer past 100%
        _targetSteering = Mathf.Clamp(rawSteer, -1f, 1f);
    }

    private void HandleFingerUp(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        _isInteracting = false;

        // Releasing the screen snaps the target back to center (straight forward)
        _targetSteering = 0f;
    }

    // --- UI CHECK ---
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        // If the raycast hit anything on the UI layer, return true!
        return results.Count > 0;
    }
}