using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;

    [Header("Animation Settings")]
    [Tooltip("How massive the text becomes when it pops. Increase this if it looks too small!")]
    public float popScale = 4.5f;
    public float floatHeight = 3f;
    public float duration = 1.2f;
    public float sideScatter = 1.5f;

    [Header("Camera Distance Compensation")]
    [Tooltip("The reference camera distance where scale equals exactly 1x popScale. As camera pulls back, scale increases proportionally to maintain exact screen size.")]
    public float referenceCameraDistance = 15f;

    private Transform _followTarget;
    private Vector3 _horizontalOffset;
    private float _animatedScale;

    private Tween _scaleTween;
    private Tween _moveTween;
    private Tween _fadeTween;

    public void Setup(string text, Color textColor, Transform target = null)
    {
        _followTarget = target;

        // 1. Reset text and color
        textMesh.text = text;
        textMesh.color = textColor;
        textMesh.alpha = 1f;

        // 2. Scatter slightly so subsequent pop-ups don't feel completely static
        float randomX = Random.Range(-sideScatter, sideScatter);
        transform.position += new Vector3(randomX, 0, 0);

        // 3. Calculate horizontal offset if following a target
        if (_followTarget != null)
        {
            _horizontalOffset = new Vector3(
                transform.position.x - _followTarget.position.x,
                0f,
                transform.position.z - _followTarget.position.z
            );
        }

        // 4. Always face the camera immediately on spawn
        if (Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
        }

        // 5. Kill any active tweens safely
        KillActiveTweens();

        // 6. THE ANIMATION: We tween a private float (_animatedScale) instead of localScale directly.
        // This allows LateUpdate to apply camera distance math without fighting DOTween!
        _animatedScale = 0f;
        _scaleTween = DOTween.To(() => _animatedScale, x => _animatedScale = x, popScale, 0.3f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);

        // Float up along the Y axis
        _moveTween = transform.DOMoveY(transform.position.y + floatHeight, duration)
            .SetEase(Ease.OutCirc)
            .SetUpdate(true);

        // Fade out and return to the pool
        _fadeTween = textMesh.DOFade(0f, duration)
            .SetEase(Ease.InExpo)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                DismissImmediate();
            });
    }

    /// <summary>
    /// Instantly halts animations and returns this object to the pool.
    /// Called automatically when duration ends OR when a new pop overrides an active one.
    /// </summary>
    public void DismissImmediate()
    {
        KillActiveTweens();
        _followTarget = null;
        gameObject.SetActive(false); // Returns to simple Object Pools automatically
    }

    private void KillActiveTweens()
    {
        _scaleTween?.Kill();
        _moveTween?.Kill();
        _fadeTween?.Kill();
        transform.DOKill();
        if (textMesh != null) textMesh.DOKill();
    }

    private void LateUpdate()
    {
        // 1. Follow target X/Z position
        if (_followTarget != null)
        {
            Vector3 currentPos = transform.position;
            currentPos.x = _followTarget.position.x + _horizontalOffset.x;
            currentPos.z = _followTarget.position.z + _horizontalOffset.z;
            transform.position = currentPos;
        }

        if (Camera.main != null)
        {
            // 2. Always face the camera billboard-style
            transform.forward = Camera.main.transform.forward;

            // 3. Dynamic Distance Compensation: Scale up as camera moves away!
            float camDistance = Vector3.Distance(transform.position, Camera.main.transform.position);
            float distanceMultiplier = Mathf.Max(0.1f, camDistance / referenceCameraDistance);

            // Apply the animated scale multiplied by the camera distance factor
            transform.localScale = Vector3.one * (_animatedScale * distanceMultiplier);
        }
        else
        {
            transform.localScale = Vector3.one * _animatedScale;
        }
    }

    private void OnDisable()
    {
        KillActiveTweens();
    }
}