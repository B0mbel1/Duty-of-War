using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BackToMenu : MonoBehaviour
{
    [Header("Przypisz tutaj ca³y obiekt panelu Menu (to co ma znikn¹æ)")]
    public GameObject menuPanel;

    // --- Ta funkcja jest do przycisku "Wróæ do Menu" ---
    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    // --- NOWA FUNKCJA: Do przycisku "Wróæ do gry" ---
    public void ResumeGame()
    {
        // 1. Odpauzowanie czasu (wa¿ne, ¿eby gra ruszy³a)
        Time.timeScale = 1f;

        // 2. Wy³¹czenie panelu menu
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        else
        {
            // Opcja zapasowa: jeœli zapomnia³eœ przypisaæ panelu, 
            // a skrypt le¿y bezpoœrednio na nim, to wy³¹czy sam siebie.
            Debug.LogWarning("BackToMenu: Nie przypisano 'menuPanel', próbujê wy³¹czyæ ten obiekt.");
            gameObject.SetActive(false);
        }
    }

    IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        yield return null;
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("BackToMenu: Nie wpisano nazwy sceny w przycisku!");
            yield break;
        }

        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncOperation.allowSceneActivation = true;

        while (!asyncOperation.isDone)
        {
            yield return null;
        }
    }
}