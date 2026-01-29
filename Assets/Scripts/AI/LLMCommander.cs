using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LLMCommander : MonoBehaviour
{
    [Header("Dependencies")]
    public ArmyHierarchyManager armyManager;
    public GeminiNetwork geminiNetwork;

    [Header("UI")]
    public TMP_InputField commandInput;

    [Header("Player Settings")]
    public string playerSide = "SideA";

    void Start()
    {
        // --- Auto-wykrywanie Managera ---
        if (armyManager == null)
        {
            armyManager = FindAnyObjectByType<ArmyHierarchyManager>();

            if (armyManager == null)
            {
                Debug.LogError("BŁĄD KRYTYCZNY: LLMCommander nie znalazł ArmyHierarchyManager na tej mapie!");
                return;
            }
        }

        // Automatyczne wykrywanie strony
        playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");

        if (geminiNetwork != null)
            geminiNetwork.OnResponseReceived += ExecuteAICommands;

        if (commandInput != null)
        {
            commandInput.onSubmit.AddListener(val =>
            {
                OnSendButtonClicked();
                commandInput.DeactivateInputField();
            });
        }
    }

    public void OnSendButtonClicked()
    {
        if (commandInput != null && !string.IsNullOrEmpty(commandInput.text))
        {
            string textToSend = commandInput.text;
            commandInput.text = "";
            SendOrderToAI(textToSend);
        }
        else
        {
            Debug.LogWarning("Puste pole rozkazu!");
        }
    }

    void SendOrderToAI(string playerText)
    {
        string systemPrompt = ConstructSystemPrompt();
        string contextInfo = GetMapContextInfo();

        string finalPrompt = systemPrompt + "\nCONTEXT:\n" + contextInfo + "\n\nUSER COMMAND: " + playerText;

        Debug.Log("Wysyłam zapytanie do AI...");
        geminiNetwork.SendRequest(finalPrompt);
    }

    void ExecuteAICommands(string jsonResponse)
    {
        Debug.Log("AI Odpowiedziało: " + jsonResponse);

        try
        {
            AICommandResponse response = JsonUtility.FromJson<AICommandResponse>(jsonResponse);

            if (!string.IsNullOrEmpty(response.ai_thought))
                Debug.Log("<color=cyan>Generał myśli: " + response.ai_thought + "</color>");

            foreach (var cmd in response.commands)
            {
                ProcessSingleCommand(cmd);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Błąd interpretacji rozkazu: " + e.Message);
        }
    }

    // --- LOGIKA JEDNOSTEK ---

    string GetCustomUnitNames()
    {
        List<string> customNames = new List<string>();
        var myUnits = (playerSide == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;

        foreach (var group in myUnits)
        {
            if (!string.IsNullOrEmpty(group.customName))
            {
                customNames.Add($"'{group.customName}'");
            }
        }

        if (customNames.Count == 0) return "";
        return ", " + string.Join(", ", customNames);
    }

    void ProcessSingleCommand(AICommand cmd)
    {
        // 1. Znajdź jednostki
        List<TacticalGroup> myUnits = (playerSide == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;
        List<TacticalGroup> selectedUnits = new List<TacticalGroup>();

        foreach (string tag in cmd.unit_tags)
        {
            string lowerTag = tag.ToLower();
            if (lowerTag == "all")
            {
                selectedUnits.AddRange(myUnits);
                break;
            }

            foreach (var group in myUnits)
            {
                if (group.groupName.ToLower().Contains(lowerTag) || group.customName.ToLower().Contains(lowerTag))
                {
                    if (!selectedUnits.Contains(group)) selectedUnits.Add(group);
                }
            }
        }

        if (selectedUnits.Count == 0) return;

        // 2. Oblicz cel ruchu
        Vector3 targetPosition = Vector3.zero;
        bool positionFound = CalculateTargetPosition(cmd.target_location, out targetPosition);

        // 3. Wykonaj rozkaz
        if (positionFound)
        {
            foreach (var group in selectedUnits)
            {
                float clampedX = Mathf.Clamp(targetPosition.x, armyManager.minX, armyManager.maxX);
                float clampedZ = Mathf.Clamp(targetPosition.z, armyManager.minZ, armyManager.maxZ);

                group.currentDestination = new Vector3(clampedX, targetPosition.y, clampedZ);
                group.isMoving = true;
            }
        }
    }

    // --- LOGIKA CELÓW I MAPY ---

    bool CalculateTargetPosition(string locationName, out Vector3 position)
    {
        position = Vector3.zero;
        string loc = locationName.ToLower();

        float centerX = (armyManager.minX + armyManager.maxX) / 2;
        float centerZ = (armyManager.minZ + armyManager.maxZ) / 2;
        Vector3 enemyAvgPos = GetEnemyArmyCenter();

        // 1. Sprawdź komendy standardowe
        if (loc.Contains("center"))
        {
            position = new Vector3(centerX, 0, centerZ);
            return true;
        }
        if (loc.Contains("enemy") || loc.Contains("attack"))
        {
            position = enemyAvgPos;
            return true;
        }
        if (loc.Contains("base") || loc.Contains("retreat"))
        {
            float baseZ = (playerSide == "SideA") ? armyManager.minZ + 100 : armyManager.maxZ - 100;
            position = new Vector3(centerX, 0, baseZ);
            return true;
        }
        if (loc.Contains("left"))
        {
            position = new Vector3(armyManager.minX + 200, 0, centerZ);
            return true;
        }
        if (loc.Contains("right"))
        {
            position = new Vector3(armyManager.maxX - 200, 0, centerZ);
            return true;
        }

        // 2. NOWOŚĆ: Szukanie po featureName w komponentach TerrainFeature
        // Zamiast szukać po nazwie obiektu, szukamy po nazwie, którą daliśmy dla AI
        TerrainFeature[] allFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);

        foreach (var feature in allFeatures)
        {
            // Porównujemy nazwę zwróconą przez AI z nazwą w Twoim skrypcie TerrainFeature
            if (feature.featureName.ToLower() == loc)
            {
                position = feature.transform.position;
                return true;
            }
        }

        // Fallback: Jeśli nic nie znalazł w skryptach, spróbuj znaleźć po nazwie obiektu (stary sposób)
        GameObject featureObj = GameObject.Find(locationName);
        if (featureObj != null)
        {
            position = featureObj.transform.position;
            return true;
        }

        return false;
    }

    string GetMapFeaturesInfo()
    {
        TerrainFeature[] features = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        if (features.Length == 0) return "Brak";

        List<string> descriptions = new List<string>();
        foreach (var f in features)
        {
            // Dodajemy nazwę w cudzysłowie, żeby AI wiedziało, że to dokładny string
            descriptions.Add($"'{f.featureName}'");
        }

        return string.Join(", ", descriptions);
    }

    Vector3 GetEnemyArmyCenter()
    {
        List<TacticalGroup> enemies = (playerSide == "SideA") ? armyManager.factionBGroups : armyManager.factionAGroups;
        if (enemies.Count == 0) return new Vector3((armyManager.minX + armyManager.maxX) / 2, 0, (armyManager.minZ + armyManager.maxZ) / 2);

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var g in enemies)
        {
            if (g.unitMatrices.Count > 0)
            {
                sum += g.groupCenter;
                count++;
            }
        }
        return (count > 0) ? sum / count : sum;
    }

    string ConstructSystemPrompt()
    {
        string dynamicNames = GetCustomUnitNames();
        string mapLocations = GetMapFeaturesInfo(); // Teraz pobieramy listę z mapy

        // Wstawiamy {mapLocations} do promptu w sekcji DOSTĘPNE LOKACJE
        return $@"Jesteś dowódcą w grze RTS. Tłumaczysz komendy gracza na format JSON.
ZASADY:
1. Odpowiadaj TYLKO w formacie JSON.
2. unit_tags: 'Infantry', 'Archer', 'Cavalry', 'All'{dynamicNames}.
3. target_location:
   - STAŁE: 'Center', 'Enemy_Army', 'My_Base', 'Left_Flank', 'Right_Flank'.
   - TERENOWE (Na tej mapie): {mapLocations}.
4. action: 'MOVE' lub 'ATTACK'.

PRZYKŁAD:
User: Piechota do lasu!
(Jeśli na liście TERENOWE jest 'Forest_North')
JSON:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Infantry""], ""action"": ""MOVE"", ""target_location"": ""Forest_North"" }}
  ],
  ""ai_thought"": ""Wysyłam piechotę do lasu północnego.""
}}";
    }

    string GetMapContextInfo()
    {
        int myCount = 0;
        var myUnits = (playerSide == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;
        foreach (var g in myUnits) myCount += g.unitMatrices.Count;

        return $"My Units Count: {myCount}. Map Size: {armyManager.maxX}x{armyManager.maxZ}.";
    }
}