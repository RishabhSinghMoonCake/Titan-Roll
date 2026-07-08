using UnityEngine;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;

public enum ShakeType
{
    Bump,       // Quick, sharp hit (e.g., small obstacle)
    Short,      // Standard impact (e.g., medium damage)
    Long,       // Sustained rumble (e.g., tumbling down a steep hill)
    Epic,       // Massive screen-clearing impact (e.g., 100% max power launch)
    Custom      // Define completely via code
}

public class CustomCameraShaker : MonoBehaviour
{
    public static CustomCameraShaker Instance { get; private set; }

    [System.Serializable]
    public class ShakeProfile
    {
        public string profileName;
        [Tooltip("Maximum intensity of the camera movement.")]
        public float amplitude;
        [Tooltip("How fast/violently the camera vibrates.")]
        public float frequency;
        [Tooltip("Total duration of the shake in seconds.")]
        public float duration;
        [Tooltip("How the shake fades out over time (1 is full power, 0 is stopped).")]
        public AnimationCurve falloffCurve;

        public ShakeProfile(string name, float amp, float freq, float dur)
        {
            profileName = name;
            amplitude = amp;
            frequency = freq;
            duration = dur;
            // Default Curve: Starts at max (1), drops sharply, then fades out smoothly to 0
            falloffCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 0.5f), new Keyframe(1f, 0f));
        }
    }

    [Header("Predefined Shake Profiles")]
    public ShakeProfile bumpShake = new ShakeProfile("Bump", 1.5f, 10f, 0.15f);
    public ShakeProfile shortShake = new ShakeProfile("Short", 3.0f, 15f, 0.35f);
    public ShakeProfile longShake = new ShakeProfile("Long Rumble", 1.2f, 8f, 1.2f);
    public ShakeProfile epicShake = new ShakeProfile("Epic Launch", 6.0f, 25f, 0.8f);

    private Coroutine _activeShakeRoutine;

    // We cache the noises and their original baseline settings so we don't ruin any idle wobble you might have!
    private Dictionary<CinemachineBasicMultiChannelPerlin, Vector2> _baseNoiseStates = new Dictionary<CinemachineBasicMultiChannelPerlin, Vector2>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Scans the scene for all Virtual Cameras and caches their Noise components.
    /// Call this if you dynamically spawn new cameras mid-game.
    /// </summary>
    public void RefreshCameraNoises()
    {
        _baseNoiseStates.Clear();
        CinemachineVirtualCamera[] allCams = FindObjectsOfType<CinemachineVirtualCamera>(true);

        foreach (var cam in allCams)
        {
            var noise = cam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            if (noise != null)
            {
                // Remember their default Amplitude (x) and Frequency (y)
                _baseNoiseStates.Add(noise, new Vector2(noise.m_AmplitudeGain, noise.m_FrequencyGain));
            }
        }
    }

    /// <summary>
    /// Fire a pre-tuned shake profile by name.
    /// </summary>
    public void Shake(ShakeType type)
    {
        ShakeProfile profileToUse = bumpShake; // Default fallback

        switch (type)
        {
            case ShakeType.Bump: profileToUse = bumpShake; break;
            case ShakeType.Short: profileToUse = shortShake; break;
            case ShakeType.Long: profileToUse = longShake; break;
            case ShakeType.Epic: profileToUse = epicShake; break;
        }

        FireCustomShake(profileToUse);
    }

    /// <summary>
    /// Pass in completely custom variables on the fly from any script.
    /// </summary>
    public void FireCustomShake(float amplitude, float frequency, float duration)
    {
        ShakeProfile tempProfile = new ShakeProfile("Temp", amplitude, frequency, duration);
        FireCustomShake(tempProfile);
    }

    private void FireCustomShake(ShakeProfile profile)
    {
        // Refresh our cameras in case the scene reloaded or new ones spawned
        RefreshCameraNoises();

        // If a shake is already happening, override it with the new one
        if (_activeShakeRoutine != null)
        {
            StopCoroutine(_activeShakeRoutine);
        }

        _activeShakeRoutine = StartCoroutine(ShakeRoutine(profile));
    }

    private IEnumerator ShakeRoutine(ShakeProfile profile)
    {
        float elapsedTime = 0f;

        while (elapsedTime < profile.duration)
        {
            // CRITICAL: We use unscaledDeltaTime so the camera shakes violently 
            // even if the game is frozen in a 0.01x hit-stop!
            elapsedTime += Time.unscaledDeltaTime;

            float percentComplete = elapsedTime / profile.duration;
            float dampingFactor = profile.falloffCurve.Evaluate(percentComplete);

            float targetAmplitude = profile.amplitude * dampingFactor;
            float targetFrequency = profile.frequency * dampingFactor;

            // Apply the shake to every camera in the scene
            foreach (var kvp in _baseNoiseStates)
            {
                if (kvp.Key != null)
                {
                    // Add the shake on top of whatever their baseline idle wobble is
                    kvp.Key.m_AmplitudeGain = kvp.Value.x + targetAmplitude;
                    kvp.Key.m_FrequencyGain = kvp.Value.y + targetFrequency;
                }
            }

            yield return null;
        }

        ResetCamerasToBaseline();
        _activeShakeRoutine = null;
    }

    private void ResetCamerasToBaseline()
    {
        foreach (var kvp in _baseNoiseStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.m_AmplitudeGain = kvp.Value.x;
                kvp.Key.m_FrequencyGain = kvp.Value.y;
            }
        }
    }

    private void OnDestroy()
    {
        ResetCamerasToBaseline();
    }
}