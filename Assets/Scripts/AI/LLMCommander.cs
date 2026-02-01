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

        // Automatyczne wykrywanie strony (SideA = Rzym/Dół, SideB = Goci/Góra)
        playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
        Debug.Log($"LLMCommander: Dowodzę stroną {playerSide}.");

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

        Vector3 targetPosition = Vector3.zero;
        bool positionFound = CalculateTargetPosition(cmd.target_location, out targetPosition);

        if (positionFound)
        {
            foreach (var group in selectedUnits)
            {
                float clampedX = Mathf.Clamp(targetPosition.x, armyManager.minX, armyManager.maxX);
                float clampedZ = Mathf.Clamp(targetPosition.z, armyManager.minZ, armyManager.maxZ);

                group.currentDestination = new Vector3(clampedX, targetPosition.y, clampedZ);
                group.isMoving = true;
                Debug.Log($"Grupa {group.groupName} rusza do {group.currentDestination}");
            }
        }
    }

    // --- NOWA LOGIKA CELÓW (STRONY ŚWIATA + OBIEKTY) ---

    bool CalculateTargetPosition(string locationName, out Vector3 position)
    {
        position = Vector3.zero;
        string loc = locationName.ToLower();

        float centerX = (armyManager.minX + armyManager.maxX) / 2;
        float centerZ = (armyManager.minZ + armyManager.maxZ) / 2;

        bool isSideA = (playerSide == "SideA");
        // Baza gracza
        Vector3 myBasePos = isSideA ? new Vector3(centerX, 0, armyManager.minZ) : new Vector3(centerX, 0, armyManager.maxZ);

        // --- 1. SPRAWDZANIE DOKŁADNYCH NAZW Z MAPY (PRIORYTET) ---
        // Jeśli AI powie "Forest_1_North", to szukamy tego konkretnego znacznika.
        TerrainFeature[] allFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        foreach (var feature in allFeatures)
        {
            if (feature.featureName.ToLower() == loc)
            {
                // Znaleziono konkretny punkt (np. "Forest_1_SouthEast")
                position = feature.transform.position;
                return true;
            }
        }

        // --- 2. KOMENDY OGÓLNE (Relatywne i Globalne) ---

        // A. Komendy Relatywne (Zależne od gracza)
        if (loc.Contains("center")) { position = new Vector3(centerX, 0, centerZ); return true; }
        if (loc.Contains("enemy") || loc.Contains("attack")) { position = GetEnemyArmyCenter(); return true; }
        if (loc.Contains("base") || loc.Contains("retreat"))
        {
            float retreatZ = isSideA ? (armyManager.minZ + 150) : (armyManager.maxZ - 150);
            position = new Vector3(centerX, 0, retreatZ);
            return true;
        }

        // B. Flanki (Lewa/Prawa - Zależne od tego, w którą stronę patrzymy)
        if (loc == "left flank" || loc == "left")
        {
            // SideA (patrzy Z+): Lewa to MinX. SideB (patrzy Z-): Lewa to MaxX.
            float xPos = isSideA ? (armyManager.minX + 250) : (armyManager.maxX - 250);
            position = new Vector3(xPos, 0, centerZ);
            return true;
        }
        if (loc == "right flank" || loc == "right")
        {
            float xPos = isSideA ? (armyManager.maxX - 250) : (armyManager.minX + 250);
            position = new Vector3(xPos, 0, centerZ);
            return true;
        }

        // C. GLOBALNE STRONY ŚWIATA (Niezależne od frakcji)
        // North = Max Z, South = Min Z, East = Max X, West = Min X
        float mapMargin = 200f; // Margines od krawędzi

        if (loc.Contains("north")) // Północ (Góra mapy)
        {
            float targetX = centerX;
            if (loc.Contains("east")) targetX = armyManager.maxX - mapMargin; // Północny Wschód
            else if (loc.Contains("west")) targetX = armyManager.minX + mapMargin; // Północny Zachód

            position = new Vector3(targetX, 0, armyManager.maxZ - mapMargin);
            return true;
        }

        if (loc.Contains("south")) // Południe (Dół mapy)
        {
            float targetX = centerX;
            if (loc.Contains("east")) targetX = armyManager.maxX - mapMargin;
            else if (loc.Contains("west")) targetX = armyManager.minX + mapMargin;

            position = new Vector3(targetX, 0, armyManager.minZ + mapMargin);
            return true;
        }

        if (loc.Contains("east")) // Wschód (Prawo mapy)
        {
            position = new Vector3(armyManager.maxX - mapMargin, 0, centerZ);
            return true;
        }

        if (loc.Contains("west")) // Zachód (Lewo mapy)
        {
            position = new Vector3(armyManager.minX + mapMargin, 0, centerZ);
            return true;
        }

        // --- 3. FALLBACK DLA OBIEKTÓW ---
        // Jeśli AI podało "River" zamiast "River_1", szukamy obiektu i stajemy PRZED nim.
        GameObject featureObj = GameObject.Find(locationName);
        if (featureObj != null)
        {
            Vector3 objPos = featureObj.transform.position;
            Vector3 directionToMe = (myBasePos - objPos).normalized;
            position = objPos + (directionToMe * 45.0f);
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
        string mapLocations = GetMapFeaturesInfo();

        return $@"Jesteś dowódcą w grze RTS. Tłumaczysz komendy gracza na format JSON.
ZASADY:
1. Odpowiadaj TYLKO w formacie JSON.
2. unit_tags: 'Infantry', 'Archer', 'Cavalry', 'All'{dynamicNames}.
3. target_location:
   - KIERUNKI: 'North' (Góra), 'South' (Dół), 'East' (Prawo), 'West' (Lewo).
   - RELATYWNE: 'Left_Flank', 'Right_Flank', 'Center', 'Enemy_Army', 'My_Base'.
   - TERENOWE (Preferowane): {mapLocations}.
4. action: 'MOVE' lub 'ATTACK'.

PRZYKŁADY (Zawsze używaj tablicy 'commands'!):

User: Piechota do lasu na północy!
AI:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Infantry""], ""action"": ""MOVE"", ""target_location"": ""Forest_1_North"" }}
  ],
  ""ai_thought"": ""Wysyłam piechotę na północny skraj lasu.""
}}

User: Jazda na wschodnią flankę, a łucznicy na środek!
AI:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Cavalry""], ""action"": ""MOVE"", ""target_location"": ""Right_Flank"" }},
    {{ ""unit_tags"": [""Archer""], ""action"": ""MOVE"", ""target_location"": ""Center"" }}
  ],
  ""ai_thought"": ""Rozdzielam siły: jazda na flankę, łucznicy centralnie.""
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