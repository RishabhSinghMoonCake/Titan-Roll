using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HandTutorial : MonoBehaviour
{
    [Header("References")]
    public Image handImage;
    public RectTransform handRect;

    [Header("Sprites")]
    public Sprite openHandSprite;
    public Sprite grabHandSprite;

    [Header("Animation Settings")]
    public float startY = 200f;
    public float endY = -200f;
    public float fadeDuration = 0.3f;
    public float dragDuration = 1.0f;
    [Tooltip("How long the hand pauses before grabbing/fading")]
    public float pauseDuration = 0.25f;

    private Sequence _tutorialSequence;

    public void PlayTutorial()
    {
        gameObject.SetActive(true);
        _tutorialSequence?.Kill();

        // Initial State Setup
        handRect.anchoredPosition = new Vector2(handRect.anchoredPosition.x, startY);
        handImage.sprite = openHandSprite;
        handRect.localScale = Vector3.one; // Ensure it starts at 100% scale

        Color c = handImage.color;
        c.a = 0f;
        handImage.color = c;

        _tutorialSequence = DOTween.Sequence();

        // Fade In
        _tutorialSequence.Append(handImage.DOFade(1f, fadeDuration));
        _tutorialSequence.AppendInterval(pauseDuration);

        // Swap to Grab + 60% Scale
        _tutorialSequence.AppendCallback(() =>
        {
            handImage.sprite = grabHandSprite;
            handRect.localScale = Vector3.one * 0.6f;
        });

        // Drag down
        _tutorialSequence.Append(handRect.DOAnchorPosY(endY, dragDuration).SetEase(Ease.InOutSine));

        // Swap back to Open + 100% Scale
        _tutorialSequence.AppendCallback(() =>
        {
            handImage.sprite = openHandSprite;
            handRect.localScale = Vector3.one;
        });

        _tutorialSequence.AppendInterval(pauseDuration);

        // Fade Out
        _tutorialSequence.Append(handImage.DOFade(0f, fadeDuration));

        // Pause before looping
        _tutorialSequence.AppendInterval(0.5f);

        _tutorialSequence.SetLoops(-1, LoopType.Restart);
    }

    public void StopTutorial()
    {
        _tutorialSequence?.Kill();
        // Smoothly fade out the hand no matter where it is in the animation
        handImage.DOFade(0f, 0.15f).OnComplete(() => gameObject.SetActive(false));
    }

    private void OnDestroy()
    {
        _tutorialSequence?.Kill();
    }
}