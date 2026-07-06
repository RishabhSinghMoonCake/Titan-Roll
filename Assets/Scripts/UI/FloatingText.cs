using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;

    [Header("Animation Settings")]
    [Tooltip("How massive the text becomes when it pops. Increase this if it looks too small!")]
    public float popScale = 4.5f; // <-- NEW: Dedicated Scale Knob!
    public float floatHeight = 3f;
    public float duration = 1.2f;
    public float sideScatter = 1.5f;

    private Transform _followTarget;
    private Vector3 _horizontalOffset;

    public void Setup(string text, Color textColor, Transform target = null)
    {
        _followTarget = target;

        // 1. Reset text and color
        textMesh.text = text;
        textMesh.color = textColor;
        textMesh.alpha = 1f;

        // 2. Scatter slightly so multiple pop-ups don't perfectly overlap
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

        // 4. Always face the camera
        if (Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
        }

        // 5. Kill any old tweens just in case the pooler grabbed it early
        transform.DOKill();
        textMesh.DOKill();

        // 6. THE ANIMATION (Now scaling up to your custom popScale!)
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one * popScale, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

        // Float up along the Y axis
        transform.DOMoveY(transform.position.y + floatHeight, duration).SetEase(Ease.OutCirc).SetUpdate(true);

        // Fade out and return to the pool
        textMesh.DOFade(0f, duration).SetEase(Ease.InExpo).SetUpdate(true).OnComplete(() =>
        {
            _followTarget = null;
            gameObject.SetActive(false);
        });
    }

    private void LateUpdate()
    {
        if (_followTarget != null)
        {
            Vector3 currentPos = transform.position;
            currentPos.x = _followTarget.position.x + _horizontalOffset.x;
            currentPos.z = _followTarget.position.z + _horizontalOffset.z;
            transform.position = currentPos;
        }

        if (Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
        }
    }
}