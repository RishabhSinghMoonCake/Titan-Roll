using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Remove if not using TextMeshPro

public class Bootloader: MonoBehaviour
{
    [Header("Scene To Load")]
    public string sceneToLoad = "MainMenu";

    [Header("UI References")]
    public Slider progressBar;
    public TextMeshProUGUI progressText; // Change to standard 'Text' if needed
    public Image animatedIcon;           // The UI Image component that will display the sprites!

    [Header("Sprite Animation Settings")]
    [Tooltip("Drag your animation frames here in order (Frame 0 to Frame X).")]
    public Sprite[] animationSprites;

    [Tooltip("Check = Sprites change based on loading percentage.\nUncheck = Sprites loop continuously over time like a GIF.")]
    public bool animateByProgress = true;

    [Tooltip("Only used if 'Animate By Progress' is unchecked. Frames per second.")]
    public float framesPerSecond = 12f;

    [Header("Loading Settings")]
    public float minLoadingTime = 1.5f;

    private float timer = 0f;

    private void Start()
    {
        StartCoroutine(LoadGameAsync());
    }

    private void Update()
    {
        // Handle Time-Based looping animation if disabled by progress
        if (!animateByProgress && animatedIcon != null && animationSprites.Length > 0)
        {
            timer += Time.deltaTime;
            int frameIndex = (int)(timer * framesPerSecond) % animationSprites.Length;
            animatedIcon.sprite = animationSprites[frameIndex];
        }
    }

    private IEnumerator LoadGameAsync()
    {
        float elapsedTime = 0f;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneToLoad);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            elapsedTime += Time.deltaTime;

            float rawProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsedTime / minLoadingTime);
            float finalProgress = Mathf.Min(rawProgress, timeProgress);

            // Update Slider & Text
            if (progressBar != null) progressBar.value = finalProgress;
            if (progressText != null) progressText.text = $"Rolling... {(int)(finalProgress * 100)}%";

            // Handle Progress-Based sprite animation
            if (animateByProgress && animatedIcon != null && animationSprites.Length > 0)
            {
                // Map the 0-1 progress to our sprite array index
                int frameIndex = Mathf.FloorToInt(finalProgress * (animationSprites.Length - 1));
                animatedIcon.sprite = animationSprites[frameIndex];
            }

            // Launch Game!
            if (operation.progress >= 0.9f && elapsedTime >= minLoadingTime)
            {
                if (progressText != null) progressText.text = "SLAP!";

                // Optional: Force the very last sprite frame when reaching 100%
                if (animatedIcon != null && animationSprites.Length > 0)
                {
                    animatedIcon.sprite = animationSprites[animationSprites.Length - 1];
                }

                yield return new WaitForSeconds(0.25f);
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}