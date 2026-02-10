using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    public AudioClip musicForThisScene;

    void Start()
    {
        if (MusicManager.instance != null)
        {
            MusicManager.instance.ChangeMusic(musicForThisScene);
        }
    }
}