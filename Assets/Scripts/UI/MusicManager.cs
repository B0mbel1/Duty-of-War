using UnityEngine;
using UnityEngine.Audio; // 1. DODANO: Aby móc sterować mikserem

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    public AudioMixer audioMixer; // 2. DODANO: Musisz tu przypisać swój AudioMixer w Inspektorze!

    private AudioSource audioSource;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 3. DODANO: Ta funkcja wykona się automatycznie na samym starcie gry
    void Start()
    {
        // Pobieramy zapisaną głośność (domyślnie 1, czyli 100%)
        float savedVolume = PlayerPrefs.GetFloat("savedVolume", 1f);

        // Aplikujemy ją do AudioMixera (to samo wyliczenie co w SettingsScript)
        if (audioMixer != null)
        {
            float dbVolume = (savedVolume <= 0.0001f) ? -80f : Mathf.Log10(savedVolume) * 20;
            audioMixer.SetFloat("MasterVolume", dbVolume);
        }
    }

    public void ChangeMusic(AudioClip newClip)
    {
        if (audioSource.clip == newClip) return;

        audioSource.Stop();
        audioSource.clip = newClip;
        audioSource.Play();
    }
}