using System;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [Header("Launch Settings")]
    public float dragDistanceForMaxPower = 400f;

    [Header("Steering Settings")]
    [Tooltip("How many pixels of drag = Full Steering (-1 to 1)")]
    public float dragSensitivity = 150f;
    public bool invertSteering = false;

    // --- EVENTS ---
    public event Action OnDragStart;
    public event Action<float> OnDragUpdate; // Launch Power
    public event Action<float> OnDragEnd;

    // --- PUBLIC READ-ONLY ---
    public float SteeringInput { get; private set; } // -1 (Left) to 1 (Right)

    private Vector2 _startTouchPosition;
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

        _isInteracting = true;
        _startTouchPosition = finger.screenPosition;
        _lastFrameTouchPos = finger.screenPosition; // Reset delta

        OnDragStart?.Invoke();
    }

    private void HandleFingerMove(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        // 1. LAUNCH LOGIC (Vertical)
        float launchPower = CalculatePull(finger.screenPosition);
        OnDragUpdate?.Invoke(launchPower);

        // 2. STEERING LOGIC (Horizontal Drag Delta)
        // We calculate how much the finger moved *this frame*
        float deltaX = finger.screenPosition.x - _lastFrameTouchPos.x;
        _lastFrameTouchPos = finger.screenPosition; // Update for next frame

        // Add to our current steering value (Accumulative Drag)
        // Or use direct position relative to center?
        // "Sled Surfers" style is usually Delta-based (Drag left to go left)

        float sensitivity = invertSteering ? -1f : 1f;
        float touchSteer = (deltaX / dragSensitivity) * sensitivity;

        // Combine Touch + Joystick
        // We clamp it so you can't steer 200% speed
        SteeringInput = Mathf.Clamp(touchSteer + ExternalJoystickInput, -1f, 1f);
    }

    private void HandleFingerUp(Finger finger)
    {
        if (!_isInteracting || finger.index != 0) return;

        float finalPull = CalculatePull(finger.screenPosition);
        _isInteracting = false;

        // Reset Steering on release (Optional - usually feels better for joystick)
        SteeringInput = 0f;

        OnDragEnd?.Invoke(finalPull);
    }

    private float CalculatePull(Vector2 currentPos)
    {
        float verticalDrag = _startTouchPosition.y - currentPos.y;
        if (verticalDrag < 0) verticalDrag = 0;
        return Mathf.Clamp01(verticalDrag / dragDistanceForMaxPower);
    }
}