using UnityEngine;
using UnityEngine.Audio;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;
    public AudioMixer audioMixer;
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

    void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat("savedVolume", 1f);
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