using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiNetwork : MonoBehaviour
{
    [Header("API Settings")]
    // Pamiętaj, aby wkleić tu swój klucz z Google AI Studio
    [SerializeField] private string apiKey = "WPISZ_SWOJ_KLUCZ_API";

    // Używamy modelu Flash (szybki i tani/darmowy)
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    // Event, który odpali się, gdy przyjdzie odpowiedź
    public event Action<string> OnResponseReceived;

    public void SendRequest(string prompt)
    {
        StartCoroutine(PostRequest(prompt));
    }

    IEnumerator PostRequest(string promptText)
    {
        // 1. Budujemy JSON zapytania ręcznie (najbezpieczniejsza metoda bez zewnętrznych bibliotek)
        // EscapeJson zabezpiecza przed cudzysłowami w tekście gracza
        string escapedText = EscapeJson(promptText);

        string jsonPayload = "{ \"contents\": [{ \"parts\": [{ \"text\": \"" + escapedText + "\" }] }], \"generationConfig\": { \"responseMimeType\": \"application/json\" } }";

        using (UnityWebRequest webRequest = new UnityWebRequest($"{apiUrl}?key={apiKey}", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Wysyłamy i czekamy
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Gemini Error: " + webRequest.error + "\nTreść błędu: " + webRequest.downloadHandler.text);
            }
            else
            {
                // Sukces! Parsujemy odpowiedź
                string responseText = ParseGeminiResponse(webRequest.downloadHandler.text);

                if (!string.IsNullOrEmpty(responseText))
                {
                    OnResponseReceived?.Invoke(responseText);
                }
            }
        }
    }

    // Funkcja czyszcząca tekst do formatu JSON
    string EscapeJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }

    string ParseGeminiResponse(string rawJson)
    {
        // Używamy Unity JsonUtility i naszych wrapperów (zdefiniowanych na dole pliku)
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

    // --- STRUKTURY DANYCH DO ODBIORU ODPOWIEDZI OD GOOGLE ---
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