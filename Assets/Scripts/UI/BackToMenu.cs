using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BackToMenu : MonoBehaviour
{
    [Header("Przypisz tutaj cały obiekt panelu Menu (to co ma zniknąć)")]
    public GameObject menuPanel;

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("BackToMenu: Nie przypisano 'menuPanel', próbuję wyłączyć ten obiekt.");
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