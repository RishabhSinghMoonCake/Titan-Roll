using UnityEngine;
using DG.Tweening;

public class SteeringTutorialUI : MonoBehaviour
{
    public static SteeringTutorialUI Instance;

    public CanvasGroup canvasGroup;
    public RectTransform handIcon;

    private void Awake()
    {
        Instance = this;
        canvasGroup.alpha = 0f;
    }

    public void ShowTutorial()
    {
        // Fade in
        canvasGroup.DOFade(1f, 0.5f);

        handIcon.DOKill();

        float dragDistance = 100f;

        handIcon.anchoredPosition = new Vector2(-dragDistance, 0);

        handIcon.DOAnchorPosX(dragDistance, 0.8f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void HideTutorial()
    {
        // IMPORTANT: Passing 'true' to DOKill() finishes the tween and 
        // stops the loop instantly.
        handIcon.DOKill(true);
        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.3f);
    }
}