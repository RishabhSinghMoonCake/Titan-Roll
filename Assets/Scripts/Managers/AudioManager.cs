using System.Collections.Generic;
using UnityEngine;

// This helper class lets us set up individual sound effects in the Inspector
[System.Serializable]
public class Sound
{
    [Tooltip("The name you will use to call this sound from other scripts (e.g., 'Slap', 'Roll', 'Win').")]
    public string name;

    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Range(0.5f, 1.5f)]
    public float pitch = 1f;

    [Tooltip("Check this for looping sounds like background music or continuous rolling.")]
    public bool loop = false;

    [HideInInspector]
    public AudioSource source; // The manager will assign this automatically at runtime
}

public class AudioManager : MonoBehaviour
{
    // A static reference so any script can call AudioManager.Instance
    public static AudioManager Instance;

    [Header("Audio Library")]
    [Tooltip("Add all your game's sound effects and music here!")]
    public Sound[] sounds;

    // A dictionary for super-fast lookups by name instead of searching the array every time
    private Dictionary<string, Sound> soundDictionary;

    private void Awake()
    {
        // 1. Singleton Setup: Ensure only ONE AudioManager ever exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keeps the audio manager alive when changing scenes!
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 2. Initialize the Dictionary and AudioSources
        soundDictionary = new Dictionary<string, Sound>();

        foreach (Sound s in sounds)
        {
            // Create a dedicated AudioSource component for each sound
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;

            // Add to dictionary for fast lookups
            if (!soundDictionary.ContainsKey(s.name))
            {
                soundDictionary.Add(s.name, s);
            }
            else
            {
                Debug.LogWarning($"AudioManager: A sound with the name '{s.name}' already exists! Check your Inspector.");
            }
        }
    }

    /// <summary>
    /// Plays a sound effect by its assigned name.
    /// </summary>
    public void Play(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound s))
        {
            s.source.Play();
        }
        else
        {
            Debug.LogWarning($"AudioManager: Sound '{soundName}' not found in library!");
        }
    }

    /// <summary>
    /// Stops a sound from playing (useful for looping sounds like rolling or music).
    /// </summary>
    public void Stop(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound s))
        {
            s.source.Stop();
        }
        else
        {
            Debug.LogWarning($"AudioManager: Sound '{soundName}' not found in library!");
        }
    }

    /// <summary>
    /// Dynamically adjusts the pitch and volume of a playing sound.
    /// </summary>
    public void ModulateSound(string soundName, float pitch, float volume = 1f)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound s))
        {
            s.source.pitch = pitch;
            s.source.volume = volume;
        }
    }

    /// <summary>
    /// Checks if a sound is currently playing.
    /// </summary>
    public bool IsPlaying(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound s))
        {
            return s.source.isPlaying;
        }
        return false;
    }
}