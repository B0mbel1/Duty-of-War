using UnityEngine;
using UnityEngine.UI;
using TMPro; // Potrzebne do obsługi Input Fielda
using System.Collections.Generic;

public class LLMCommander : MonoBehaviour
{
    [Header("Dependencies")]
    public ArmyHierarchyManager armyManager; // Twój główny skrypt (przeciągnij w Inspectorze)
    public GeminiNetwork geminiNetwork;      // Skrypt sieciowy (przeciągnij w Inspectorze)

    [Header("UI")]
    public TMP_InputField commandInput;      // Przeciągnij tutaj swoje pole tekstowe z gry

    [Header("Player Settings")]
    public string playerSide = "SideA";

    void Start()
    {
        // Automatyczne wykrywanie strony (jeśli używasz wyboru w menu)
        playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");

        if (geminiNetwork != null)
            geminiNetwork.OnResponseReceived += ExecuteAICommands;

        // Obsługa ENTERA
        if (commandInput != null)
        {
            commandInput.onSubmit.AddListener(val =>
            {
                OnSendButtonClicked();
                commandInput.DeactivateInputField(); // Przywraca sterowanie grą
            });
        }
    }

    // --- TO JEST FUNKCJA DLA PRZYCISKU (Zastępuje GeminiManager) ---
    public void OnSendButtonClicked()
    {
        if (commandInput != null && !string.IsNullOrEmpty(commandInput.text))
        {
            string textToSend = commandInput.text;
            commandInput.text = ""; // Czyścimy pole po wysłaniu
            SendOrderToAI(textToSend);
        }
        else
        {
            Debug.LogWarning("Puste pole rozkazu!");
        }
    }
    // ---------------------------------------------------------------

    void SendOrderToAI(string playerText)
    {
        string systemPrompt = ConstructSystemPrompt();
        // Dodajemy kontekst mapy (gdzie jest środek, gdzie wróg)
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
            // Parsujemy JSON na obiekt
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

    string GetCustomUnitNames()
    {
        List<string> customNames = new List<string>();

        // Sprawdzamy, którą stroną gramy
        var myUnits = (playerSide == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;

        foreach (var group in myUnits)
        {
            // Jeśli grupa ma customName (i nie jest pusty), dodajemy go do listy
            if (!string.IsNullOrEmpty(group.customName))
            {
                // Dodajemy w cudzysłowach, np. 'Armia 1'
                customNames.Add($"'{group.customName}'");
            }
        }

        if (customNames.Count == 0) return "";

        // Zwracamy stringa: , 'Armia 1', 'Zakon', 'Straż'
        return ", " + string.Join(", ", customNames);
    }

    void ProcessSingleCommand(AICommand cmd)
    {
        // 1. Znajdź nasze jednostki
        List<TacticalGroup> myUnits = (playerSide == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;
        List<TacticalGroup> selectedUnits = new List<TacticalGroup>();

        // Proste filtrowanie po tagach
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
                // Sprawdzamy nazwę systemową (np. A_Archers) i nazwę gracza (jeśli nadał)
                if (group.groupName.ToLower().Contains(lowerTag) || group.customName.ToLower().Contains(lowerTag))
                {
                    if (!selectedUnits.Contains(group)) selectedUnits.Add(group);
                }
            }
        }

        if (selectedUnits.Count == 0) return;

        // 2. Podświetl je w UI (używając twojej metody SelectGroup)
        // Czyścimy obecne zaznaczenie (opcjonalnie)
        // foreach(var g in selectedUnits) armyManager.SelectGroup(g); 

        // 3. Oblicz cel ruchu
        Vector3 targetPosition = Vector3.zero;
        bool positionFound = CalculateTargetPosition(cmd.target_location, out targetPosition);

        // 4. Wykonaj rozkaz
        if (positionFound)
        {
            foreach (var group in selectedUnits)
            {
                // To jest logika skopiowana z twojego IssueMoveOrder, żeby działała bez klikania myszką
                float clampedX = Mathf.Clamp(targetPosition.x, armyManager.minX, armyManager.maxX);
                float clampedZ = Mathf.Clamp(targetPosition.z, armyManager.minZ, armyManager.maxZ);

                group.currentDestination = new Vector3(clampedX, targetPosition.y, clampedZ);
                group.isMoving = true;
            }
        }
    }

    bool CalculateTargetPosition(string locationName, out Vector3 position)
    {
        position = Vector3.zero;
        string loc = locationName.ToLower();

        float centerX = (armyManager.minX + armyManager.maxX) / 2;
        float centerZ = (armyManager.minZ + armyManager.maxZ) / 2;
        Vector3 enemyAvgPos = GetEnemyArmyCenter();

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
            // Baza to krawędź startowa
            float baseZ = (playerSide == "SideA") ? armyManager.minZ + 100 : armyManager.maxZ - 100;
            // Dla pewności bierzemy pozycję spawnu A lub B jeśli są dostępne w Managerze (tu upraszczamy)
            position = new Vector3(centerX, 0, baseZ);
            return true;
        }
        if (loc.Contains("left")) // Lewa flanka (patrząc od dołu mapy)
        {
            position = new Vector3(armyManager.minX + 200, 0, centerZ);
            return true;
        }
        if (loc.Contains("right")) // Prawa flanka
        {
            position = new Vector3(armyManager.maxX - 200, 0, centerZ);
            return true;
        }

        return false;
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
        // Pobieramy nazwy dynamicznie
        string dynamicNames = GetCustomUnitNames();

        return $@"Jesteś dowódcą w grze RTS. Tłumaczysz komendy gracza na format JSON.
ZASADY:
1. Odpowiadaj TYLKO w formacie JSON.
2. unit_tags: 'Infantry', 'Archer', 'Cavalry', 'All'{dynamicNames}.
3. target_location: 'Center', 'Enemy_Army', 'My_Base', 'Left_Flank', 'Right_Flank'.
4. action: 'MOVE' lub 'ATTACK'.

PRZYKŁAD:
User: Armia 1 do ataku!
JSON:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Armia 1""], ""action"": ""ATTACK"", ""target_location"": ""Enemy_Army"" }}
  ],
  ""ai_thought"": ""Wysyłam zgrupowanie Armia 1 do ataku.""
}}";
    }
    string GetMapContextInfo()
    {
        // Tutaj możemy dodać informacje dla AI o stanie bitwy (opcjonalne, ale pomocne)
        int myCount = 0;
        var myUnits = (playerSide == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;
        foreach (var g in myUnits) myCount += g.unitMatrices.Count;

        return $"My Units Count: {myCount}. Map Size: {armyManager.maxX}x{armyManager.maxZ}.";
    }
}