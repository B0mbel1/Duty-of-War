using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuNavigationNavigation : MonoBehaviour
{
    // Ta funkcja ³aduje scenê na podstawie nazwy
    public void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("Nie podano nazwy sceny!");
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}


