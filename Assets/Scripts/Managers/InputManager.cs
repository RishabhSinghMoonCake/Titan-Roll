using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Steering Settings")]
    [Tooltip("How many pixels of drag = Full Steering (-1 to 1)")]
    public float dragSensitivity = 150f;
    public bool invertSteering = false;

    // --- EVENTS ---
    public event Action OnLaunchTap;

    // --- PUBLIC READ-ONLY ---
    public float SteeringInput { get; private set; } // -1 (Left) to 1 (Right)

    private Vector2 _lastFrameTouchPos;
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

    private void HandleFingerDown(Finger finger)
    {
        if (Touch.activeTouches.Count > 1) return;

        // Ignore the tap if the user is touching a UI element (e.g., a button)
        if (IsPointerOverUI(finger)) return;

        _isInteracting = true;
        _lastFrameTouchPos = finger.screenPosition;

        // Trigger Launch
        OnLaunchTap?.Invoke();
    }

    private void HandleFingerMove(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        // STEERING LOGIC (Horizontal Drag Delta)
        float deltaX = finger.screenPosition.x - _lastFrameTouchPos.x;
        _lastFrameTouchPos = finger.screenPosition;

        float sensitivity = invertSteering ? -1f : 1f;
        float touchSteer = (deltaX / dragSensitivity) * sensitivity;

        // Combine Touch + Joystick
        SteeringInput = Mathf.Clamp(touchSteer + ExternalJoystickInput, -1f, 1f);
    }

    private void HandleFingerUp(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        _isInteracting = false;

        // Reset Steering on release
        SteeringInput = 0f;
    }

    // --- UI CHECK ---
    private bool IsPointerOverUI(Finger finger)
    {
        if (EventSystem.current == null) return false;

        // Check if the touch pointer is currently over a UI element
        int pointerId = finger.currentTouch.touchId;

        if (EventSystem.current.IsPointerOverGameObject(pointerId))
            return true;

        // Fallback for editor/mouse clicks
        if (EventSystem.current.IsPointerOverGameObject(-1))
            return true;

        return false;
    }
}