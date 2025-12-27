using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BackToMenu : MonoBehaviour
{
    // Ta funkcja pojawi siê w przycisku i pozwoli wpisaæ nazwê sceny
    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f; // Odpauzowanie czasu
        StartCoroutine(LoadSceneAsyncRoutine(sceneName));
    }

    IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        // Czekamy klatkê na odœwie¿enie UI
        yield return null;

        // Czyszczenie pamiêci (wa¿ne po bitwie)
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        // Sprawdzenie czy nazwa nie jest pusta
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("BackToMenu: Nie wpisano nazwy sceny w przycisku!");
            yield break;
        }

        // £adowanie asynchroniczne
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        asyncOperation.allowSceneActivation = true;

        while (!asyncOperation.isDone)
        {
            yield return null;
        }
    }
}