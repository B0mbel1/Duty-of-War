using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;
    private AudioSource audioSource; // Prywatne odniesienie do głośnika

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Pobieramy AudioSource, żeby móc nim sterować
            audioSource = GetComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Nowa funkcja do zmiany muzyki
    public void ChangeMusic(AudioClip newClip)
    {
        // 1. Jeśli nowa muzyka jest taka sama jak ta co gra, nie rób nic (żeby nie restartować utworu)
        if (audioSource.clip == newClip) return;

        // 2. Jeśli jest inna: Zatrzymaj, Podmień, Graj
        audioSource.Stop();
        audioSource.clip = newClip;
        audioSource.Play();
    }
}