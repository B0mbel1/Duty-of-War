using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuPrefab;
    private GameObject pauseMenuInstance;
    private bool isPaused = false;

    void Start()
    {
        if (pauseMenuPrefab != null)
        {
            pauseMenuInstance = Instantiate(pauseMenuPrefab);
            pauseMenuInstance.SetActive(false);
        }
        else
        {
            Debug.LogError("Prefab Pause Menu nie został przypisany!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    void Pause()
    {
        if (pauseMenuInstance != null)
        {
            pauseMenuInstance.SetActive(true);
            Time.timeScale = 0f;
            isPaused = true;
        }
    }

    void Resume()
    {
        if (pauseMenuInstance != null)
        {
            pauseMenuInstance.SetActive(false);
            Time.timeScale = 1f;
            isPaused = false;
        }
    }

    public void OnResumeButton()
    {
        Resume();
    }
}
