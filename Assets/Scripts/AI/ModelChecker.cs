using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
public class ModelChecker : MonoBehaviour
{
    private string apiKey;

    [System.Serializable]
    private class SecretConfig
    {
        public string geminiApiKey;
    }

    private void Awake()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "secrets.json");
        if (File.Exists(path))
        {
            SecretConfig config = JsonUtility.FromJson<SecretConfig>(File.ReadAllText(path));
            apiKey = config != null ? config.geminiApiKey : string.Empty;
        }
    }

    public void CheckModels()
    {
        StartCoroutine(GetAvailableModels());
    }

    IEnumerator GetAvailableModels()
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Błąd sprawdzania modeli: " + webRequest.error + "\n" + webRequest.downloadHandler.text);
            }
            else
            {
                Debug.Log("--- DOSTĘPNE MODELE ---");
                Debug.Log(webRequest.downloadHandler.text);
                Debug.Log("-----------------------");
            }
        }
    }
}