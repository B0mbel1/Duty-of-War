using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    public AudioClip musicForThisScene; // Tu wrzucisz muzykę dla tej konkretnej mapy

    void Start()
    {
        // Przy starcie sceny mówimy Managerowi: "Zagraj mój kawałek"
        if (MusicManager.instance != null)
        {
            MusicManager.instance.ChangeMusic(musicForThisScene);
        }
    }
}