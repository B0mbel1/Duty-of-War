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
            if (lowerTag == "selected")
            {
                // Pobieramy listę, którą kliknąłeś myszką w UI
                var manualSelection = armyManager.GetSelectedGroups();
                foreach (var g in manualSelection)
                {
                    // Sprawdzamy, czy zaznaczona jednostka należy do nas 
                    // (żeby nie sterować wrogiem przez przypadek, jeśli kliknąłeś go dla info)
                    if (myUnits.Contains(g) && !selectedUnits.Contains(g))
                    {
                        selectedUnits.Add(g);
                    }
                }
                continue;
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

        // --- OBSŁUGA ROZKAZU STOP ---
        if (cmd.action == "STOP")
        {
            foreach (var group in selectedUnits)
            {
                group.isMoving = false;
                group.currentDestination = group.groupCenter; // Reset celu na obecną pozycję
                Debug.Log($"Grupa {group.groupName} zatrzymana na rozkaz.");
            }
            return; // Kończymy funkcję
        }

        // --- OBSŁUGA FORMACJI ---
        if (cmd.action == "FORM")
        {
            // Typ formacji jest w polu target_location (np. "Turtle", "Line")
            string formType = cmd.target_location;
            foreach (var g in selectedUnits)
            {
                // Wywołujemy metodę z Managera (upewnij się, że dodałeś ją do ArmyHierarchyManager!)
                armyManager.ApplyFormation(g, formType);
                Debug.Log($"Grupa {g.groupName} zmienia formację na: {formType}");
            }
            return; // Kończymy funkcję
        }

        // --- OBSŁUGA RUCHU RELATYWNEGO (np. "Forward 100m") ---
        if (cmd.target_location.StartsWith("Relative_"))
        {
            // Parsujemy stringa: "Relative_Left_100" -> ["Relative", "Left", "100"]
            string[] parts = cmd.target_location.Split('_');
            if (parts.Length == 3)
            {
                string directionStr = parts[1];
                float distance = 0;
                if (float.TryParse(parts[2], out distance))
                {
                    Vector3 moveDir = GetDirectionVector(directionStr);

                    foreach (var group in selectedUnits)
                    {
                        // Obliczamy nowy cel względem OBECNEJ pozycji grupy
                        Vector3 newPos = group.groupCenter + (moveDir * distance);

                        // Clamp (ograniczenie do mapy)
                        float clampedX = Mathf.Clamp(newPos.x, armyManager.minX, armyManager.maxX);
                        float clampedZ = Mathf.Clamp(newPos.z, armyManager.minZ, armyManager.maxZ);

                        group.currentDestination = new Vector3(clampedX, group.groupCenter.y, clampedZ);
                        group.isMoving = true;
                    }
                    Debug.Log($"Przesuwam jednostki o {distance}m w kierunku {directionStr}");
                    return; // Kończymy
                }
            }
        }

        // --- OBSŁUGA RUCHU STANDARDOWEGO (Konkretny punkt na mapie) ---
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

    // --- POMOCNICZA FUNKCJA DO KIERUNKÓW RELATYWNYCH ---
    Vector3 GetDirectionVector(string dirName)
    {
        bool isSideA = (playerSide == "SideA");
        // SideA (Rzym): Patrzy w Z+ (Góra). Prawo to X+.
        // SideB (Goci): Patrzy w Z- (Dół). Prawo to X- (z ich perspektywy).

        Vector3 forward = isSideA ? Vector3.forward : Vector3.back;
        Vector3 right = isSideA ? Vector3.right : Vector3.left;

        switch (dirName.ToLower())
        {
            case "forward": return forward;
            case "backward": return -forward;
            case "left": return -right;
            case "right": return right;
            default: return Vector3.forward;
        }
    }

    // --- LOGIKA CELÓW ABSOLUTNYCH (Strony świata, Obiekty) ---

    bool CalculateTargetPosition(string locationName, out Vector3 position)
    {
        position = Vector3.zero;
        string loc = locationName.ToLower();

        float centerX = (armyManager.minX + armyManager.maxX) / 2;
        float centerZ = (armyManager.minZ + armyManager.maxZ) / 2;

        bool isSideA = (playerSide == "SideA");
        Vector3 myBasePos = isSideA ? new Vector3(centerX, 0, armyManager.minZ) : new Vector3(centerX, 0, armyManager.maxZ);

        // 1. Sprawdzanie dokładnych nazw (TerrainFeature)
        TerrainFeature[] allFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        foreach (var feature in allFeatures)
        {
            if (feature.featureName.ToLower() == loc)
            {
                position = feature.transform.position;
                return true;
            }
        }

        // 2. Komendy ogólne
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

        // C. GLOBALNE STRONY ŚWIATA
        float mapMargin = 200f;

        if (loc.Contains("north")) // Północ
        {
            float targetX = centerX;
            if (loc.Contains("east")) targetX = armyManager.maxX - mapMargin;
            else if (loc.Contains("west")) targetX = armyManager.minX + mapMargin;

            position = new Vector3(targetX, 0, armyManager.maxZ - mapMargin);
            return true;
        }

        if (loc.Contains("south")) // Południe
        {
            float targetX = centerX;
            if (loc.Contains("east")) targetX = armyManager.maxX - mapMargin;
            else if (loc.Contains("west")) targetX = armyManager.minX + mapMargin;

            position = new Vector3(targetX, 0, armyManager.minZ + mapMargin);
            return true;
        }

        if (loc.Contains("east")) // Wschód
        {
            position = new Vector3(armyManager.maxX - mapMargin, 0, centerZ);
            return true;
        }

        if (loc.Contains("west")) // Zachód
        {
            position = new Vector3(armyManager.minX + mapMargin, 0, centerZ);
            return true;
        }

        // 3. Fallback dla zwykłych obiektów
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
2. unit_tags: 'Infantry', 'Archer', 'Cavalry', 'All', 'Selected'{dynamicNames}.
   - 'Selected' oznacza jednostki aktualnie wybrane przez gracza (myszką).
3. target_location:
   - KIERUNKI: 'North' (Góra), 'South' (Dół), 'East' (Prawo), 'West' (Lewo).
   - RELATYWNE: 'Left_Flank', 'Right_Flank', 'Center', 'Enemy_Army', 'My_Base'.
   - TERENOWE (Preferowane): {mapLocations}.
   - DLA KOMENDY STOP: 'None'.
   - DLA PRZESUNIĘĆ (np. 100m w lewo): 'Relative_{{Kierunek}}_{{Metry}}'. Kierunki: Forward, Backward, Left, Right.
   - DLA 'FORM' (Formacje): 'Line' (Szereg), 'Double_Line' (Dwuszereg), 'Box' (Kwadrat), 'Turtle' (Żółw), 'Loose' (Luźna).
4. action: 'MOVE', 'ATTACK', 'STOP', 'FORM'.

PRZYKŁADY (Zawsze używaj tablicy 'commands'!):

User: Piechota do lasu na północy!
AI:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Infantry""], ""action"": ""MOVE"", ""target_location"": ""Forest_1_North"" }}
  ],
  ""ai_thought"": ""Wysyłam piechotę na północny skraj lasu.""
}}

User: Jazda ustawić się w dwuszeregu!
AI:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Cavalry""], ""action"": ""FORM"", ""target_location"": ""Double_Line"" }}
  ],
  ""ai_thought"": ""Jazda zmienia szyk na dwuszereg.""
}}

User: Jazda 100 metrów do przodu, łucznicy wycofać się 50 metrów!
AI:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Cavalry""], ""action"": ""MOVE"", ""target_location"": ""Relative_Forward_100"" }},
    {{ ""unit_tags"": [""Archer""], ""action"": ""MOVE"", ""target_location"": ""Relative_Backward_50"" }}
  ],
  ""ai_thought"": ""Jazda atakuje, łucznicy robią miejsce.""
}}



User: Wszyscy STÓJ! Zatrzymać się!
AI:
{{
  ""commands"": [
    {{ ""unit_tags"": [""All""], ""action"": ""STOP"", ""target_location"": ""None"" }}
  ],
  ""ai_thought"": ""Zatrzymuję całą armię.""
}}

User: Zaznaczone jednostki do lasu!
    AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Forest_1_Center"" }} ], ""ai_thought"": ""Wysyłam wybrany oddział do lasu."" }}

    User: Wybrani, formacja żółwia!
    AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""FORM"", ""target_location"": ""Turtle"" }} ], ""ai_thought"": ""Zaznaczeni formują żółwia."" }}";
    }

    string GetMapContextInfo()
    {
        int myCount = 0;
        var myUnits = (playerSide == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;
        foreach (var g in myUnits) myCount += g.unitMatrices.Count;

        return $"My Units Count: {myCount}. Map Size: {armyManager.maxX}x{armyManager.maxZ}.";
    }
}