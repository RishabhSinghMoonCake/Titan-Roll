using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [SerializeField]private TextMeshPro textMesh;

    [Header("Animation Settings")]
    public float floatHeight = 3f;
    public float duration = 1.2f;
    public float sideScatter = 1.5f;

    public void Setup(string text, Color textColor)
    {
        // 1. Reset text and color
        textMesh.text = text;
        textMesh.color = textColor;
        textMesh.alpha = 1f; // Reset transparency

        // 2. Scatter slightly so if you smash 3 things at once, the numbers don't perfectly overlap
        float randomX = Random.Range(-sideScatter, sideScatter);
        transform.position += new Vector3(randomX, 0, 0);

        // 3. Always face the camera (assuming a standard follow cam)
        if (Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
        }

        // 4. Kill any old tweens just in case the pooler grabbed it early
        transform.DOKill();
        textMesh.DOKill();

        // 5. THE ANIMATION
        // Pop the scale up slightly, then settle
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

        // Float up
        transform.DOMoveY(transform.position.y + floatHeight, duration).SetEase(Ease.OutCirc);

        // Fade out and return to the pool
        textMesh.DOFade(0f, duration).SetEase(Ease.InExpo).OnComplete(() =>
        {
            gameObject.SetActive(false); // Your pooler will see it's inactive and reuse it!
        });
    }
}