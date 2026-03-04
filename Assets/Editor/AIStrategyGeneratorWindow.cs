using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AIStrategyGeneratorWindow : EditorWindow
{
    string battleDescription = "Np. Rzymianie atakują środkiem, a gdy podejda na 50m, kawaleria uderza z flanki wschodniej...";
    string strategyName = "NowaStrategia_Kanny";
    string jsonResult = "";

    Vector2 scrollPos;

    [MenuItem("AI Tools/Strategy Generator")] 
    public static void ShowWindow()
    {
        GetWindow<AIStrategyGeneratorWindow>("AI Strategy Gen");
    }

    void OnGUI()
    {
        GUILayout.Label("KROK 1: Opisz Bitwę", EditorStyles.boldLabel);
        strategyName = EditorGUILayout.TextField("Nazwa Pliku:", strategyName);
        
        GUILayout.Label("Opis Taktyki:");
        battleDescription = EditorGUILayout.TextArea(battleDescription, GUILayout.Height(60));

        if (GUILayout.Button("1. Generuj Prompt dla AI"))
        {
            GeneratePrompt();
        }

        GUILayout.Space(10);
        GUILayout.Label("KROK 2: Wklej wynik JSON z AI", EditorStyles.boldLabel);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(100));
        jsonResult = EditorGUILayout.TextArea(jsonResult, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("2. Stwórz Asset (.asset)"))
        {
            CreateAssetFromJson();
        }
    }

    void GeneratePrompt()
    {
        string mapFeatures = "";
        foreach(var f in FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None)) 
            mapFeatures += f.featureName + ", ";

        if(string.IsNullOrEmpty(mapFeatures)) mapFeatures = "Brak nazwanych obiektów na scenie (używaj nazw ogólnych).";

        string prompt = $@"
Jesteś projektantem poziomów do gry RTS. Twoim zadaniem jest zamiana opisu historycznej bitwy na strukturę danych JSON.

KONTEKST MAPY (Twoje TerrainFeature): {mapFeatures}
JEDNOSTKI: Infantry, Archer, Cavalry.
AKCJE (Enum): 0=MoveTo, 1=AttackMove, 2=DefendFeature, 3=Flank, 4=HoldPosition, 5=ChargeTarget.
TRIGGERY (Enum): 0=Immediate, 1=TimeElapsed (sec), 2=EnemyDistance (m), 3=CasualtiesPercent (0.0-1.0).

OPIS BITWY: ""{battleDescription}""

ZADANIE: Stwórz CZYSTY JSON (bez markdown ```json) pasujący do tej struktury:
{{
  ""description"": ""krótki opis"",
  ""battlePhases"": [
    {{
      ""phaseName"": ""string"",
      ""trigger"": int (wybierz z listy wyżej),
      ""triggerValue"": float,
      ""commands"": [
         {{ ""action"": int, ""unitTag"": ""string"", ""targetName"": ""string"", ""formation"": ""Line"" }}
      ]
    }}
  ]
}}
WAŻNE: targetName musi być nazwą z KONTEKSTU MAPY lub: 'Center', 'Enemy_Army', 'Flank_West', 'Flank_East'.";

        Debug.Log("PROMPT SKOPIOWANY DO SCHOWKA!");
        GUIUtility.systemCopyBuffer = prompt; 
        EditorUtility.DisplayDialog("Sukces", "Prompt został skopiowany do schowka! Wklej go teraz do Gemini/ChatGPT.", "OK");
    }

    void CreateAssetFromJson()
    {
        if (string.IsNullOrEmpty(jsonResult)) {
            Debug.LogError("Pole JSON jest puste!");
            return;
        }

        AIStrategyProfile profile = CreateInstance<AIStrategyProfile>();
        
        try {
            JsonUtility.FromJsonOverwrite(jsonResult, profile);
        }
        catch (System.Exception e) {
            Debug.LogError($"Błąd parsowania JSON: {e.Message}. Upewnij się, że AI nie dodało ```json na początku.");
            return;
        }

        string path = $"Assets/{strategyName}.asset";
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = profile;

        Debug.Log($"<color=green>SUKCES! Utworzono strategię: {path}</color>");
    }
}