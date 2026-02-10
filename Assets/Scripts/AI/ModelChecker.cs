using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ModelChecker : MonoBehaviour
{
    [SerializeField] private string apiKey = "WPISZ_SWOJ_KLUCZ_API"; 

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