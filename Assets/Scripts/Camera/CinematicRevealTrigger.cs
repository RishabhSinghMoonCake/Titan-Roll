using UnityEngine;
using Cinemachine;
using System.Collections;

[RequireComponent(typeof(BoxCollider))]
public class CinematicRevealTrigger : MonoBehaviour
{
    [Header("Cinematic Settings")]
    [Tooltip("The camera pointing directly at your high-value target")]
    public CinemachineVirtualCamera targetRevealCamera;

    [Tooltip("How long the game pauses to look at the target (in real seconds)")]
    public float revealDuration = 1.5f;

    [Header("Save Data")]
    [Tooltip("A unique name for this object so the game remembers we've seen it")]
    public string targetID = "EpicGoldenPig";

    private bool _hasTriggered = false;

    private void Awake()
    {
        // Make sure the collider is set to Trigger so the boulder doesn't crash into it
        GetComponent<Collider>().isTrigger = true;

        // Ensure this camera starts inactive
        if (targetRevealCamera != null) targetRevealCamera.Priority = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Safety checks: Only run once, and only if the Boulder hits it
        if (_hasTriggered) return;
        if (other.GetComponentInParent<ArcadeBoulder>() == null) return;

        // 2. Persistent Save Check: Have we EVER seen this across any run?
        // PlayerPrefs saves locally to the device. 1 = Yes, 0 = No.
        if (PlayerPrefs.GetInt("Revealed_" + targetID, 0) == 1) return;

        // 3. Mark it as seen forever!
        _hasTriggered = true;
        PlayerPrefs.SetInt("Revealed_" + targetID, 1);
        PlayerPrefs.Save();

        // 4. Start the show
        StartCoroutine(RevealSequence());
    }

    private IEnumerator RevealSequence()
    {
        // --- FREEZE TIME ---
        // Setting timeScale to 0 instantly pauses all physics and standard animations
        Time.timeScale = 0f;

        // --- CUT TO TARGET ---
        // Elevate priority to instantly switch the camera
        if (targetRevealCamera != null) targetRevealCamera.Priority = 100;

        // CRITICAL: Because Time.timeScale is 0, normal WaitForSeconds won't work!
        // We MUST use WaitForSecondsRealtime so the code keeps counting while the game is paused.
        yield return new WaitForSecondsRealtime(revealDuration);

        // --- CUT BACK TO BOULDER ---
        if (targetRevealCamera != null) targetRevealCamera.Priority = 0;

        // Optional: Give a tiny 0.1s delay so the camera visually cuts back BEFORE physics resume
        yield return new WaitForSecondsRealtime(0.1f);

        // --- RESUME TIME ---
        Time.timeScale = 1f;
    }
}