using UnityEngine;
using TMPro;
using DG.Tweening;

public class BoulderComboText : MonoBehaviour
{
    public static BoulderComboText Instance;

    [Header("References")]
    [Tooltip("Drag your TextMeshPro component here")]
    public TextMeshPro textMesh;

    [Header("Settings")]
    public Vector3 offset = new Vector3(0, 3.5f, 0); // Height above the boulder
    [Tooltip("How long the combo stays on screen before fading out")]
    public float fadeDelay = 1.2f;

    private float _currentCombo = 0f;
    private float _fadeTimer = 0f;
    private bool _isFading = false;

    private Transform boulder;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Start completely hidden
        if (textMesh != null) textMesh.alpha = 0f;
    }

    private void Start()
    {
        if (ArcadeBoulder.Instance != null)
        {
            boulder = ArcadeBoulder.Instance.transform;
        }
    }
    void LateUpdate()
    {
        // 1. Follow the boulder's position exactly without jitter
        if (boulder != null)
        {
            // Only add half of the Y scale so it stays perfectly centered above the rock!
            float heightCompensation = boulder.localScale.y / 2f;
            transform.position = boulder.position + offset + (Vector3.up * heightCompensation);

            // Always face the camera so the text remains readable
            if (Camera.main != null)
            {
                transform.forward = Camera.main.transform.forward;
            }
        }
        else if (ArcadeBoulder.Instance != null)
        {
            // Fallback: If the boulder respawned or initialized late, grab the new transform
            boulder = ArcadeBoulder.Instance.transform;
        }

        // 2. Handle the fade-out timer
        if (_currentCombo > 0)
        {
            _fadeTimer -= Time.deltaTime;

            if (_fadeTimer <= 0 && !_isFading)
            {
                ResetCombo();
            }
        }
    }

    public void AddGold(float amount)
    {
        _currentCombo += amount;
        _fadeTimer = fadeDelay; // Reset the timer so it stays on screen!
        _isFading = false;

        // Update the text
        textMesh.text = $"+{Mathf.RoundToInt(_currentCombo)}";

        // Kill any ongoing tweens so the animations don't glitch if they smash 10 things at once
        transform.DOKill();
        textMesh.DOKill();

        // Reset to full visibility and normal scale
        textMesh.alpha = 1f;
        transform.localScale = Vector3.one;

        // Punch the scale for that satisfying "Pop"
        transform.DOPunchScale(new Vector3(0.4f, 0.4f, 0.4f), 0.25f, vibrato: 5, elasticity: 1f);
    }

    private void ResetCombo()
    {
        _isFading = true;

        // Smoothly fade out over 0.5 seconds
        textMesh.DOFade(0f, 0.5f).OnComplete(() =>
        {
            // Once fully faded, reset the math back to 0
            _currentCombo = 0f;
            _isFading = false;
        });
    }
}