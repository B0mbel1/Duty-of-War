using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuPrefab; // Twój prefab menu
    private GameObject pauseMenuInstance;
    private bool isPaused = false;

    void Start()
    {
        if (pauseMenuPrefab != null)
        {
            // Tworzymy instancjê prefab, ale od razu ukrywamy
            pauseMenuInstance = Instantiate(pauseMenuPrefab);
            pauseMenuInstance.SetActive(false);
        }
        else
        {
            Debug.LogError("Prefab Pause Menu nie zosta³ przypisany!");
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
            Time.timeScale = 0f; // zatrzymuje grê
            isPaused = true;
        }
    }

    void Resume()
    {
        if (pauseMenuInstance != null)
        {
            pauseMenuInstance.SetActive(false);
            Time.timeScale = 1f; // wznawia grê
            isPaused = false;
        }
    }

    // Funkcja dla przycisku "Kontynuuj" w menu
    public void OnResumeButton()
    {
        Resume();
    }
}
