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

    private Color originalTextColor;

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
            originalTextColor = commandInput.textComponent.color;

            commandInput.onSubmit.AddListener(val =>
             {
                 OnSendButtonClicked();
                 commandInput.DeactivateInputField();
             });

            // Ważne: nasłuchiwanie kliknięcia
            commandInput.onSelect.AddListener(OnInputSelected);
        }

        if (commandInput != null)
        {
            originalTextColor = commandInput.textComponent.color;

            // --- TUTAJ JEST ZMIANA ---
            commandInput.onSubmit.AddListener(val =>
            {
                if (!string.IsNullOrWhiteSpace(val))
                {
                    // 1. Konstruujemy prompt
                    string systemPrompt = ConstructSystemPrompt();
                    string fullPrompt = systemPrompt + "\nUser: " + val;

                    // 2. Wysyłamy do AI
                    geminiNetwork.SendRequest(fullPrompt);

                    // 3. Czyścimy pole tekstowe i blokujemy je na chwilę
                    commandInput.text = "";
                    commandInput.ActivateInputField(); // Żeby nie trzeba było klikać znowu

                    // 4. NOWOŚĆ: Ukrywamy znacznik kursora natychmiast po wciśnięciu Enter
                    armyManager.HideCursorMarker();
                }

                // Opcjonalnie: przywracamy focus na grę, żebyś mógł ruszać kamerą
                // EventSystem.current.SetSelectedGameObject(null); 
            });
        }
    }

    public void OnSendButtonClicked()
    {
        if (commandInput != null && !string.IsNullOrEmpty(commandInput.text))
        {
            // --- DODAJ TO SPRAWDZENIE: ---
            if (commandInput.textComponent.color == Color.red) return;

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

    void ShowErrorInInput()
    {
        if (commandInput != null)
        {
            // Jeśli już leci jakieś odliczanie, zatrzymaj je (żeby nie mrugało)
            StopAllCoroutines();

            commandInput.text = "Generale! Polecenia są niejasne!";
            commandInput.textComponent.color = Color.red;

            // Uruchom odliczanie do zniknięcia
            StartCoroutine(HideErrorAfterDelay());
        }
    }

    // Czyści błąd, gdy gracz kliknie w pole, żeby pisać od nowa
    void OnInputSelected(string val)
    {
        if (commandInput.textComponent.color == Color.red)
        {
            StopAllCoroutines(); // Anulujemy znikanie, bo gracz przejął kontrolę
            commandInput.text = "";
            commandInput.textComponent.color = originalTextColor; // Przywracamy czarny (lub domyślny)
        }
    }

    System.Collections.IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSeconds(2.0f); // Czekaj 2 sekundy

        // Sprawdzamy, czy tekst nadal jest czerwony (czy gracz nie zaczął pisać w międzyczasie)
        if (commandInput != null && commandInput.textComponent.color == Color.red)
        {
            commandInput.text = "";
            commandInput.textComponent.color = originalTextColor; // Wracamy do czarnego koloru gotowości
        }
    }

    void ExecuteAICommands(string jsonResponse)
    {
        Debug.Log("AI Odpowiedziało: " + jsonResponse);

        try
        {
            AICommandResponse response = JsonUtility.FromJson<AICommandResponse>(jsonResponse);
            if (response == null || response.commands == null || response.commands.Count == 0)
            {
                ShowErrorInInput(); // Wyświetlamy błąd logiczny (AI nie zwróciło komend)
                return;
            }
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
            ShowErrorInInput(); // Wyświetlamy błąd parsowania
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
        bool useCursor = (cmd.target_location.ToLower() == "cursor" || cmd.target_location.ToLower() == "marker");

        // Jeśli LLM prosi o kursor, ale gracz nie kliknął - przerywamy lub używamy pozycji grupy
        Vector3? targetCursorPos = null;
        if (useCursor)
        {
            if (armyManager.hasCursorPosition)
                targetCursorPos = armyManager.lastCursorPosition;
            else
                Debug.LogWarning("Rozkaz dotyczy kursora, ale nie zaznaczono miejsca na mapie!");
        }

        if (selectedUnits.Count == 0) return;

        // --- OBSŁUGA ROZKAZU STOP ---
        if (cmd.action == "STOP")
        {
            foreach (var group in selectedUnits)
            {
                group.isMoving = false;
                group.isReforming = false;
                group.currentDestination = group.groupCenter;
                group.targetFacingDirection = Vector3.zero; // Resetujemy wymuszony obrót
                Debug.Log($"Grupa {group.groupName} zatrzymana na rozkaz.");
                group.assignedFeature = null;
            }
            return;
        }

        // --- OBSŁUGA FORMACJI ---
        if (cmd.action == "FORM")
        {
            string formType = cmd.target_location; // Domyślnie typ formacji jest w target_location

            // Czasami LLM może zwrócić np. target_location: "Cursor", a typ w jakimś innym polu, 
            // ale dla uproszczenia załóżmy, że jeśli to formacja, to nazwa formacji jest kluczowa.
            // Zróbmy tak: Jeśli gracz kliknął na mapę, a komenda to FORM, to ZAWSZE używamy kursora jako celu.

            if (armyManager.hasCursorPosition)
            {
                targetCursorPos = armyManager.lastCursorPosition;
            }

            foreach (var g in selectedUnits)
            {
                // Wywołujemy zmodyfikowaną funkcję z punktem docelowym
                armyManager.ApplyFormation(g, formType, targetCursorPos);
            }
            return;
        }

        if (useCursor && targetCursorPos.HasValue && cmd.action == "MOVE")
        {
            foreach (var group in selectedUnits)
            {
                group.assignedFeature = null; // Resetujemy przypisanie do terenu

                // Prosta logika ruchu do punktu (ArmyManager ustawi formację 'Default' w locie w Update, 
                // lub możemy wymusić ApplyFormation("Line", pos) żeby szli ładnie).

                // Użyjmy ApplyFormation jako metody ruchu, bo to zapewnia, że nie zrobią bloba, 
                // tylko pójdą uformowani w linię w tamto miejsce.
                armyManager.ApplyFormation(group, "Line", targetCursorPos.Value);
            }
            return;
        }

        // --- OBSŁUGA RUCHU RELATYWNEGO (np. "Forward 100m", "Backward 50m") ---
        if (cmd.target_location.StartsWith("Relative_"))
        {
            string[] parts = cmd.target_location.Split('_');
            if (parts.Length == 3)
            {
                string directionStr = parts[1].ToLower(); // np. "forward", "backward"
                float distance = 0;
                if (float.TryParse(parts[2], out distance))
                {
                    foreach (var group in selectedUnits)
                    {
                        if (group.unitMatrices.Count == 0) continue;

                        // 1. Pobieramy rotację i pozycję
                        Quaternion groupRotation = group.unitMatrices[0].rotation;
                        Vector3 currentForward = groupRotation * Vector3.forward;

                        // 2. Obliczamy wektor ruchu LOKALNIE
                        Vector3 localDirection = Vector3.forward;

                        switch (directionStr)
                        {
                            case "forward": localDirection = groupRotation * Vector3.forward; break;
                            case "backward": localDirection = groupRotation * Vector3.back; break;
                            case "left": localDirection = groupRotation * Vector3.left; break;
                            case "right": localDirection = groupRotation * Vector3.right; break;
                        }

                        // 3. LOGIKA OBROTU (Fix dla wycofywania)
                        // Jeśli idziemy do tyłu, chcemy nadal patrzeć na wroga (przód)
                        if (directionStr == "backward")
                        {
                            group.targetFacingDirection = currentForward;
                        }
                        else if (directionStr == "forward")
                        {
                            group.targetFacingDirection = currentForward;
                        }
                        else
                        {
                            // Przy ruchu w bok, niech patrzą tam gdzie idą (chyba że wolisz inaczej)
                            group.targetFacingDirection = localDirection.normalized;
                        }

                        // 4. Aplikujemy przesunięcie
                        Vector3 newPos = group.groupCenter + (localDirection * distance);

                        // Clamp (ograniczenie do mapy)
                        float clampedX = Mathf.Clamp(newPos.x, armyManager.minX, armyManager.maxX);
                        float clampedZ = Mathf.Clamp(newPos.z, armyManager.minZ, armyManager.maxZ);

                        group.currentDestination = new Vector3(clampedX, group.groupCenter.y, clampedZ);
                        group.isMoving = true;
                        group.isReforming = true; // Włączamy reforming, żeby użyli precyzyjnego ruchu
                    }
                    Debug.Log($"Przesuwam jednostki o {distance}m ({directionStr})");
                    return;
                }
            }
        }

        // --- OBSŁUGA RUCHU DO KONKRETNEGO OBIEKTU (Lasy, Rzeki) ---
        TerrainFeature targetFeature = null;
        TerrainFeature[] allFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);

        foreach (var f in allFeatures)
        {
            if (f.featureName.ToLower() == cmd.target_location.ToLower())
            {
                targetFeature = f;
                break;
            }
        }

        // JEŚLI TO FEATURE Z LINIĄ (Las, Rzeka) -> Użyj logiki "MoveGroupToLineFeature"
        if (targetFeature != null && (targetFeature.lineLength > 1.0f || targetFeature.isRiver))
        {
            foreach (var group in selectedUnits)
            {
                // Manager obliczy pozycję wzdłuż linii/rzeki i ustawi odpowiedni targetFacingDirection
                armyManager.MoveGroupToLineFeature(group, targetFeature);
            }
            return;
        }

        // --- OBSŁUGA RUCHU STANDARDOWEGO (Zwykły punkt na mapie) ---
        Vector3 targetPosition = Vector3.zero;
        bool positionFound = CalculateTargetPosition(cmd.target_location, out targetPosition);

        if (positionFound)
        {
            Vector3 enemyCenter = GetEnemyArmyCenter();
            bool isFlankingCommand = cmd.target_location.ToLower().Contains("flank");

            foreach (var group in selectedUnits)
            {
                float clampedX = Mathf.Clamp(targetPosition.x, armyManager.minX, armyManager.maxX);
                float clampedZ = Mathf.Clamp(targetPosition.z, armyManager.minZ, armyManager.maxZ);
                Vector3 dest = new Vector3(clampedX, targetPosition.y, clampedZ);

                // Ustawiamy ogólny cel (dla pewności)
                group.currentDestination = dest;

                if (isFlankingCommand)
                {
                    // --- LOGIKA FLANKOWANIA (POPRAWIONA) ---
                    // Obliczamy wektor patrzenia NA WROGA
                    Vector3 directionToEnemy = (enemyCenter - dest).normalized;

                    // Wywołujemy ApplyFormation:
                    // 1. Idź do 'dest'
                    // 2. Ustaw się w 'Line'
                    // 3. Patrz w kierunku 'directionToEnemy'
                    armyManager.ApplyFormation(group, "Line", dest, directionToEnemy);
                }
                else
                {
                    // --- ZWYKŁY RUCH ---
                    // Tutaj używamy zwykłego ruchu "blob" (bez formationTargets),
                    // bo jest płynniejszy na długie dystanse.
                    group.targetFacingDirection = Vector3.zero;
                    group.isReforming = false;
                    group.isMoving = true;
                }

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

        if (loc.StartsWith("flank"))
        {
            Vector3 enemyCenter = GetEnemyArmyCenter();
            float flankDistance = 50.0f; // Dystans od środka armii wroga (żeby nie wbiec w nich od razu)

            if (loc.Contains("north"))
            {
                // Flanka północna = jesteśmy NA PÓŁNOC od wroga (Z+)
                position = enemyCenter + new Vector3(0, 0, flankDistance);
            }
            else if (loc.Contains("south"))
            {
                // Flanka południowa = jesteśmy NA POŁUDNIE od wroga (Z-)
                position = enemyCenter + new Vector3(0, 0, -flankDistance);
            }
            else if (loc.Contains("east"))
            {
                // Flanka wschodnia = jesteśmy NA WSCHÓD od wroga (X+)
                position = enemyCenter + new Vector3(flankDistance, 0, 0);
            }
            else if (loc.Contains("west"))
            {
                // Flanka zachodnia = jesteśmy NA ZACHÓD od wroga (X-)
                position = enemyCenter + new Vector3(-flankDistance, 0, 0);
            }
            else
            {
                // Domyślny flank (jeśli nie podano strony - np. z boku)
                // Wybieramy stronę bliższą naszej bazy lub po prostu prawo
                position = enemyCenter + new Vector3(flankDistance, 0, 0);
            }
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
   - SPECJALNE: 'Cursor' - UŻYJ TEGO, gdy gracz mówi 'tam', 'tutaj', 'w miejscu kliknięcia', 'do znacznika' LUB wydaje rozkaz formacji bez podania miejsca (np. 'Uformuj linię').
   - DLA FLANKOWANIA: 'Flank_North' (Od północy wroga), 'Flank_South', 'Flank_East', 'Flank_West'.
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
User: Zajdźcie ich od wschodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Flank_East"" }} ], ""ai_thought"": ""Wysyłam oddział na wschodnią flankę wroga."" }}


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

User: Idźcie tam! (Gracz kliknął na mapę)
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Cursor"" }} ], ""ai_thought"": ""Ruch w miejsce wskazane kursorem."" }}

User: Wszyscy formować żółwia w tym miejscu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""All""], ""action"": ""FORM"", ""target_location"": ""Turtle"" }} ], ""ai_thought"": ""Cała armia formuje żółwia w miejscu kursora."" }}

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