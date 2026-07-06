using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class DistanceVisualizerUI : MonoBehaviour
{
    [Header("UI References")]
    public Image fillBarImage;
    public RectTransform ballIconRect;
    public TextMeshProUGUI percentageText;
    [Tooltip("The main container RectTransform to slide. Defaults to this object if empty.")]
    public RectTransform panelRect;

    [Header("Settings")]
    public float maxRunwayDistance = 2500f;
    public bool isVerticalBar = false;

    [Header("Animation")]
    [Tooltip("How many pixels to the right it starts from before popping in.")]
    public float slideOffset = 300f;
    public float animDuration = 0.5f;

    [Tooltip("Adjust this to perfectly align the ball's starting position (e.g., -50).")]
    public float ballPositionOffset = -50f;

    private float _barSize = 0f;
    private CanvasGroup _canvasGroup;
    private Vector2 _originalPos;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (panelRect == null) panelRect = GetComponent<RectTransform>();

        _originalPos = panelRect.anchoredPosition;

        // 1. DISABLE NORMALLY: Instantly hide and disable interaction on boot
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        panelRect.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (fillBarImage != null)
        {
            RectTransform barRect = fillBarImage.rectTransform;
            _barSize = isVerticalBar ? barRect.rect.height : barRect.rect.width;
        }
    }

    /// <summary>
    /// Slides the UI in from the right while fading it up.
    /// </summary>
    public void ShowVisualizer()
    {
        panelRect.gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = true;

        _canvasGroup.DOKill();
        panelRect.DOKill();

        // Snap to starting offset
        _canvasGroup.alpha = 0f;
        panelRect.anchoredPosition = new Vector2(_originalPos.x + slideOffset, _originalPos.y);

        // Animate!
        _canvasGroup.DOFade(1f, animDuration);
        panelRect.DOAnchorPos(_originalPos, animDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Fades the UI out and turns it off.
    /// </summary>
    public void HideVisualizer()
    {
        _canvasGroup.blocksRaycasts = false;

        _canvasGroup.DOKill();
        panelRect.DOKill();

        _canvasGroup.DOFade(0f, animDuration).OnComplete(() =>
        {
            panelRect.gameObject.SetActive(false);
        });
    }

    public void UpdateVisualizer(float currentDistance)
    {
        if (fillBarImage == null || ballIconRect == null) return;

        float progress = Mathf.Clamp01(currentDistance / maxRunwayDistance);
        fillBarImage.fillAmount = progress;

        if (percentageText != null)
        {
            int percentageInt = Mathf.FloorToInt(progress * 100f);
            percentageText.text = $"{percentageInt}%";
        }

        if (isVerticalBar)
        {
            float targetY = (_barSize * progress) + ballPositionOffset;
            ballIconRect.anchoredPosition = new Vector2(ballIconRect.anchoredPosition.x, targetY);
        }
        else
        {
            float targetX = (_barSize * progress) + ballPositionOffset;
            ballIconRect.anchoredPosition = new Vector2(targetX, ballIconRect.anchoredPosition.y);
        }
    }
}