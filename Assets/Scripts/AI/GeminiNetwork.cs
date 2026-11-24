using System;
using System.Collections;
using System.Text;
using UnityEngine;
using System.IO;
using UnityEngine.Networking;

public class GeminiNetwork : MonoBehaviour
{
    [Header("Testowanie Wydajności")]
    public bool simulateSynchronousLag = false;

    private string apiKey;
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    public event Action<string> OnResponseReceived;

    [Serializable]
    private class SecretConfig
    {
        public string geminiApiKey;
    }

    private void Awake()
    {
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "secrets.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            SecretConfig config = JsonUtility.FromJson<SecretConfig>(json);
            apiKey = config != null ? config.geminiApiKey : string.Empty;
        }
        else
        {
            Debug.LogWarning("[GeminiNetwork] Brak pliku Assets/StreamingAssets/secrets.json. Skopiuj secrets.example.json i uzupełnij klucz API.");
        }
    }


    public void SendRequest(string prompt)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "WPISZ_SWOJ_KLUCZ_API")
        {
            Debug.LogWarning("[GeminiNetwork] Brak klucza API w secrets.json.");
            return;
        }
        StartCoroutine(PostRequest(prompt));
    }

    IEnumerator PostRequest(string promptText)
    {
        string escapedText = EscapeJson(promptText);

        string jsonPayload = "{ \"contents\": [{ \"parts\": [{ \"text\": \"" + escapedText + "\" }] }], \"generationConfig\": { \"responseMimeType\": \"application/json\" } }";

        using (UnityWebRequest webRequest = new UnityWebRequest($"{apiUrl}?key={apiKey}", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            if (simulateSynchronousLag)
            {
                webRequest.SendWebRequest();
                System.Threading.Thread.Sleep(1500);
            }
            else
            {
                yield return webRequest.SendWebRequest();
            }
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Gemini Error: " + webRequest.error + "\nTreść błędu: " + webRequest.downloadHandler.text);
            }
            else
            {
                string responseText = ParseGeminiResponse(webRequest.downloadHandler.text);

                if (!string.IsNullOrEmpty(responseText))
                {
                    OnResponseReceived?.Invoke(responseText);
                }
            }
        }
    }

    string EscapeJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }

    string ParseGeminiResponse(string rawJson)
    {
        try
        {
            GeminiResponseWrapper wrapper = JsonUtility.FromJson<GeminiResponseWrapper>(rawJson);

            if (wrapper != null && wrapper.candidates != null && wrapper.candidates.Length > 0)
            {
                return wrapper.candidates[0].content.parts[0].text;
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError("Błąd parsowania JSON z Google: " + e.Message);
            return null;
        }
    }

    [Serializable]
    public class GeminiResponseWrapper
    {
        public Candidate[] candidates;
    }

    [Serializable]
    public class Candidate
    {
        public Content content;
    }

    [Serializable]
    public class Content
    {
        public Part[] parts;
    }

    [Serializable]
    public class Part
    {
        public string text;
    }
}