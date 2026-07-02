using UnityEngine;
using System.Collections;

public class DebrisFader : MonoBehaviour
{
    private float lifeTime = 1.5f;
    private float fadeDuration = 1f;

    // Called manually by Destructible after spawning
    public void BeginFade()
    {
        StopAllCoroutines(); // Safety check
        StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        yield return new WaitForSeconds(lifeTime);

        float timer = 0f;
        // transform.localScale is handled by the Parent logic

        // Shrink Loop
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeDuration;
            float currentScale = Mathf.Lerp(1f, 0f, progress);

            transform.localScale = Vector3.one * currentScale;
            yield return null;
        }

        // RETURN TO POOL instead of Destroy
        ObjectPooler.Instance.ReturnToPool(gameObject);
    }
}