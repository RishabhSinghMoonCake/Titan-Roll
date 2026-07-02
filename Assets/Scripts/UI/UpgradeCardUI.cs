using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening; // <-- ADDED DOTWEEN

[RequireComponent(typeof(CanvasGroup))]
public class UpgradeCardUI : MonoBehaviour
{
    [Header("Core Settings")]
    public string upgradeType;
    public Button upgradeButton;

    [Header("Text References")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI IncomeMultText;

    [Header("Segment Visuals")]
    public Image[] segments;
    public Color activeSegmentColor = Color.green;
    public Color inactiveSegmentColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Header("Juice & Feedback")]
    [Range(0f, 1f)] public float dullAlpha = 0.5f;
    private CanvasGroup _canvasGroup;

    private Vector3 _originalScale;
    private Sequence _shakeSequence; // <-- REPLACED COROUTINE WITH SEQUENCE

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _originalScale = transform.localScale;

        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }
    }

    public void RefreshCardUI()
    {
        int level = GetLevel();
        double cost = PlayerDataManager.Instance.GetUpgradeCost(upgradeType);
        bool canAfford = PlayerDataManager.Instance.data.gold >= cost;

        // 1. Update Texts
        if (levelText != null) levelText.text = "LVL " + level;
        if (costText != null) costText.text = FormatMoney(cost);

        // 2. Modulus 5 Segment Math
        int filledSegments = level % segments.Length;
        if (filledSegments == 0 && level > 0) filledSegments = segments.Length;

        for (int i = 0; i < segments.Length; i++)
        {
            segments[i].color = (i < filledSegments) ? activeSegmentColor : inactiveSegmentColor;
        }

        
        int currentLevel = PlayerDataManager.Instance.data.greedLevel;

        // Fetch the exact multiplier from the new system
        float currentMult = RewardManager.Instance.GetIncomeMultiplier();

        // Format it to show the "x" and exactly one decimal point
        if (IncomeMultText != null)
        {
            IncomeMultText.text = $"x{currentMult:F1}";
        }
        

        // 3. Handle Dull vs Bright
        _canvasGroup.alpha = canAfford ? 1.0f : dullAlpha;

        // 4. Handle DOTween Animations
        if (canAfford)
        {
            StartIdleAnimation();
        }
        else
        {
            StopIdleAnimation();
        }
    }

    private void OnUpgradeClicked()
    {
        if (upgradeType == "Mass") GameLevelManager.Instance.BuyMassUpgrade();
        else if (upgradeType == "Strength") GameLevelManager.Instance.BuyStrengthUpgrade();
        else if (upgradeType == "Greed") GameLevelManager.Instance.BuyGreedUpgrade();
    }

    private int GetLevel()
    {
        return upgradeType switch
        {
            "Mass" => PlayerDataManager.Instance.data.massLevel,
            "Strength" => PlayerDataManager.Instance.data.strengthLevel,
            "Greed" => PlayerDataManager.Instance.data.greedLevel,
            _ => 1
        };
    }

    private string FormatMoney(double amount)
    {
        if (amount >= 1000000000) return (amount / 1000000000D).ToString("0.##") + "B";
        if (amount >= 1000000) return (amount / 1000000D).ToString("0.##") + "M";
        if (amount >= 1000) return (amount / 1000D).ToString("0.##") + "K";
        return amount.ToString("N0");
    }

    // --- DOTWEEN ANIMATION SYSTEM ---

    private void StartIdleAnimation()
    {
        // Don't start a new sequence if one is already happily running
        if (_shakeSequence != null && _shakeSequence.IsActive()) return;

        // Create a new sequence
        _shakeSequence = DOTween.Sequence();

        // Add a 2-second pause at the beginning of every loop
        _shakeSequence.AppendInterval(2.0f);

        // Scale up to 1.05 and tilt right slightly (takes 0.15s)
        _shakeSequence.Append(transform.DOScale(_originalScale * 1.05f, 0.15f).SetEase(Ease.OutSine));
        _shakeSequence.Join(transform.DOLocalRotate(new Vector3(0, 0, 2f), 0.15f).SetEase(Ease.OutSine));

        // Scale back to normal and tilt left slightly (takes 0.15s)
        _shakeSequence.Append(transform.DOScale(_originalScale, 0.15f).SetEase(Ease.InSine));
        _shakeSequence.Join(transform.DOLocalRotate(new Vector3(0, 0, -2f), 0.15f).SetEase(Ease.InOutSine));

        // Snap rotation back to center (takes 0.1s)
        _shakeSequence.Append(transform.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.InSine));

        // Tell the sequence to loop forever
        _shakeSequence.SetLoops(-1);
    }

    private void StopIdleAnimation()
    {
        if (_shakeSequence != null)
        {
            _shakeSequence.Kill(); // Instantly destroys the animation
            _shakeSequence = null;
        }

        // Hard reset just in case it was killed mid-shake
        transform.localScale = _originalScale;
        transform.localRotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        // ALWAYS kill DOTweens when an object is destroyed to prevent memory leaks!
        StopIdleAnimation();
    }
}