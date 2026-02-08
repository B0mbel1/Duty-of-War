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
        if (cmd.action == "ATTACK")
        {
            TacticalGroup targetEnemy = null;

            // --- KROK 1: SPRAWDZAMY PRIORYTETY CELU ---

            // PRIORYTET A: Czy gracz ma ręcznie zaznaczonego wroga (Czerwony Marker)?
            // Jeśli tak, to "Do ataku" ZAWSZE oznacza "Atakuj tego zaznaczonego".
            if (armyManager.selectedEnemyGroup != null && armyManager.selectedEnemyGroup.unitMatrices.Count > 0)
            {
                targetEnemy = armyManager.selectedEnemyGroup;
            }
            // PRIORYTET B: Jeśli nie ma zaznaczenia, sprawdzamy czy AI wykryło nazwę w komendzie (np. "Atakuj Łuczników")
            else if (cmd.target_location != "Enemy_Army" && cmd.target_location != "None" && !string.IsNullOrEmpty(cmd.target_location))
            {
                targetEnemy = armyManager.GetGroupByName(cmd.target_location);
            }

            // --- KROK 2: WYKONANIE ROZKAZU ---

            // SCENARIUSZ 1: MAMY KONKRETNY CEL (Zaznaczony myszką lub wymieniony z nazwy)
            if (targetEnemy != null)
            {
                foreach (var group in selectedUnits)
                {
                    group.assignedFeature = null;

                    // Ustawiamy cel pościgu - jednostki będą gonić TEN KONKRETNY oddział
                    // Dzięki temu działa priorytetyzacja ataku z poprzedniego kroku
                    group.chaseTarget = targetEnemy;

                    Vector3 directionToEnemy = (targetEnemy.groupCenter - group.groupCenter).normalized;

                    // ApplyFormation obliczy pozycje, a chaseTarget sprawi, że będą korygować kurs w biegu
                    armyManager.ApplyFormation(group, "Line", targetEnemy.groupCenter, directionToEnemy);

                    Debug.Log($"[ATAK CELOWANY] {group.groupName} atakuje: {targetEnemy.groupName}");
                }
                return;
            }

            // SCENARIUSZ 2: OGÓLNY ATAK (Brak celu -> Każdy szuka najbliższego wroga)
            // To się wykona, gdy powiesz "Do ataku", a nikogo nie zaznaczyłeś.

            List<TacticalGroup> allEnemies = (playerSide == "SideA") ? armyManager.factionBGroups : armyManager.factionAGroups;
            Debug.Log($"[ATAK OGÓLNY] Rozkaz 'Attack Move'. Każdy oddział szuka wroga na własną rękę.");

            foreach (var group in selectedUnits)
            {
                group.assignedFeature = null;
                group.chaseTarget = null; // Resetujemy stary cel (żeby nie biegli na drugi koniec mapy do starego wroga)

                // 1. Znajdź najbliższego wroga DLA TEGO KONKRETNEGO ODDZIAŁU
                TacticalGroup myPersonalTarget = armyManager.FindClosestEnemy(group.groupCenter, allEnemies);

                if (myPersonalTarget != null)
                {
                    // Przypisujemy najbliższego wroga jako cel pościgu
                    group.chaseTarget = myPersonalTarget;

                    Vector3 directionToTarget = (myPersonalTarget.groupCenter - group.groupCenter).normalized;
                    armyManager.ApplyFormation(group, "Line", myPersonalTarget.groupCenter, directionToTarget);
                }
                else
                {
                    // Fallback: Jeśli nie ma wrogów w pobliżu, idź na środek armii wroga (szukaj guza)
                    Vector3 fallbackPos = GetEnemyArmyCenter();
                    Vector3 dir = (fallbackPos - group.groupCenter).normalized;
                    armyManager.ApplyFormation(group, "Line", fallbackPos, dir);
                }
            }
            return;
        }
        // --- OBSŁUGA ROZKAZU STOP ---
        if (cmd.action == "STOP")
        {
            foreach (var group in selectedUnits)
            {
                group.chaseTarget = null; // Przestań gonić, bo masz stać
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
                g.chaseTarget = null; // Formowanie przerywa pościg
                // Wywołujemy zmodyfikowaną funkcję z punktem docelowym
                armyManager.ApplyFormation(g, formType, targetCursorPos);
            }
            return;
        }

        if (useCursor && targetCursorPos.HasValue && cmd.action == "MOVE")
        {
            foreach (var group in selectedUnits)
            {
                group.assignedFeature = null;
                group.chaseTarget = null;
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
                        group.chaseTarget = null;
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
            // Jeśli mamy zaznaczonego konkretnego wroga, bierzemy jego pozycję jako centrum
            if (armyManager.selectedEnemyGroup != null && armyManager.selectedEnemyGroup.unitMatrices.Count > 0)
            {
                enemyCenter = armyManager.selectedEnemyGroup.groupCenter;
            }

            bool isFlankingCommand = cmd.target_location.ToLower().Contains("flank");

            foreach (var group in selectedUnits)
            {
                // Domyślny cel to punkt obliczony (np. 50m od wroga)
                Vector3 finalDest = new Vector3(Mathf.Clamp(targetPosition.x, armyManager.minX, armyManager.maxX),
                                                targetPosition.y,
                                                Mathf.Clamp(targetPosition.z, armyManager.minZ, armyManager.maxZ));

                group.chaseTarget = null;

                if (isFlankingCommand)
                {
                    // --- POPRAWKA AGRESYWNOŚCI ---

                    // 1. Obliczamy wektor, w którą stronę musimy patrzeć, żeby widzieć wroga
                    Vector3 directionToEnemy = (enemyCenter - finalDest).normalized;

                    // 2. Zamiast iść w "puste miejsce" obok wroga (finalDest), 
                    //    każemy im iść BEZPOŚREDNIO NA WROGA, ale zachowując formację.
                    //    Dzięki temu wejdą w zasięg ataku (3.5f) i kolizje ich zatrzymają na styku armii.

                    // Nadpisujemy cel podróży na środek wroga
                    Vector3 attackDest = enemyCenter;

                    // Opcjonalnie: Cofamy cel o 2-3 metry od środka wroga w naszą stronę, 
                    // żeby nie próbowali przeniknąć na drugą stronę oddziału, tylko uderzyli w pierwszą linię.
                    attackDest -= directionToEnemy * 2.0f;

                    // Wywołujemy formację celującą we wroga
                    armyManager.ApplyFormation(group, "Line", attackDest, directionToEnemy);

                    // Wymuszamy odświeżenie destination (na wypadek gdyby ApplyFormation coś nadpisało)
                    group.currentDestination = attackDest;
                }
                else
                {
                    // Zwykły ruch (nie flankujący)
                    group.currentDestination = finalDest;
                    group.targetFacingDirection = Vector3.zero;
                    group.isReforming = false;
                    group.isMoving = true;
                }

                Debug.Log($"Grupa {group.groupName} rusza do {group.currentDestination} (Flank: {isFlankingCommand})");
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
        float mapMargin = 150f; // Margines od krawędzi mapy

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

        // Baza - tu logika jest zależna od gracza, bo "baza" to pojęcie relatywne
        if (loc.Contains("base") || loc.Contains("retreat"))
        {
            bool isSideA = (playerSide == "SideA"); // SideA ma bazę na dole (MinZ)
            float retreatZ = isSideA ? (armyManager.minZ + 150) : (armyManager.maxZ - 150);
            position = new Vector3(centerX, 0, retreatZ);
            return true;
        }

        // --- ABSOLUTNE STRONY ŚWIATA (KOMPAS) ---
        // Niezależnie od tego gdzie patrzy kamera:
        // North = MAX Z
        // South = MIN Z
        // East  = MAX X
        // West  = MIN X

        if (loc == "north")
        {
            position = new Vector3(centerX, 0, armyManager.maxZ - mapMargin);
            return true;
        }
        if (loc == "south")
        {
            position = new Vector3(centerX, 0, armyManager.minZ + mapMargin);
            return true;
        }
        if (loc == "east")
        {
            position = new Vector3(armyManager.maxX - mapMargin, 0, centerZ);
            return true;
        }
        if (loc == "west")
        {
            position = new Vector3(armyManager.minX + mapMargin, 0, centerZ);
            return true;
        }

        // --- FLANKOWANIE (ABSOLUTNE) ---
        if (loc.StartsWith("flank"))
        {
            // 1. Ustalamy cel (Wroga)
            Vector3 targetPoint = GetEnemyArmyCenter();
            if (armyManager.selectedEnemyGroup != null && armyManager.selectedEnemyGroup.unitMatrices.Count > 0)
            {
                targetPoint = armyManager.selectedEnemyGroup.groupCenter;
            }

            // 2. Ustalamy offset (Dystans flankowania) - zmniejszony, aby byli blisko walki
            float offset = 15.0f; // Blisko, żeby od razu atakować

            // 3. Sztywna matematyka wektorów świata
            if (loc.Contains("north")) position = targetPoint + new Vector3(0, 0, offset);  // +Z
            else if (loc.Contains("south")) position = targetPoint + new Vector3(0, 0, -offset); // -Z
            else if (loc.Contains("east")) position = targetPoint + new Vector3(offset, 0, 0);  // +X
            else if (loc.Contains("west")) position = targetPoint + new Vector3(-offset, 0, 0); // -X
            else position = targetPoint + new Vector3(offset, 0, 0); // Domyślnie wschód

            return true;
        }

        // Fallback dla obiektów nazwanych (np. "Góra Czaszki")
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
   - STRONY ŚWIATA (Absolutne): 'North' (Mapa Z+), 'South' (Mapa Z-), 'East' (Mapa X+), 'West' (Mapa X-).
     Użyj ich, gdy gracz mówi: 'na północ', 'od wschodu', 'na południu'.
   - RELATYWNE (Z perspektywy oddziału): 'Relative_Forward', 'Relative_Backward', 'Relative_Left', 'Relative_Right'.
     Użyj ich TYLKO, gdy gracz mówi: 'naprzód', 'do tyłu', 'w lewo', 'w prawo'.
   - TERENOWE: {mapLocations}.
   - DLA KOMENDY STOP: 'None'.
   - DLA PRZESUNIĘĆ (np. 100m w lewo): 'Relative_{{Kierunek}}_{{Metry}}'. Kierunki: Forward, Backward, Left, Right.
   - DLA 'FORM' (Formacje): 'Line' (Szereg), 'Double_Line' (Dwuszereg), 'Box' (Kwadrat), 'Turtle' (Żółw), 'Loose' (Luźna).
   - SPECJALNE: 'Cursor' - UŻYJ TEGO, gdy gracz mówi 'tam', 'tutaj', 'w miejscu kliknięcia', 'do znacznika' LUB wydaje rozkaz formacji bez podania miejsca (np. 'Uformuj linię').
   - FLANKOWANIE (Absolutne): 'Flank_North' (Podejdź od strony Z+), 'Flank_South' (Od Z-), 'Flank_East' (Od X+), 'Flank_West' (Od X-).
   - 'Cursor' - gdy gracz wskazuje 'tam', 'tutaj'.
   - KONKRETNY CEL: Jeśli w CONTEXT jest 'TARGETED ENEMY TAG' (np. 'B_Infantry_1'), użyj tego TAGU jako target_location przy rozkazie ataku na 'tego wroga'.
   - 'Enemy_Army' - UŻYJ TEGO, gdy gracz mówi 'Do ataku', 'Atakować', 'Zniszczyć wroga', a nie wskazał konkretnego oddziału ani nie kliknął na nikogo.
4. action: 'MOVE', 'ATTACK' - użyj, gdy gracz mówi 'bij', 'atakuj', 'szarżuj'., 'STOP', 'FORM'.

PRZYKŁADY (Zawsze używaj tablicy 'commands'!):
User: Atakujcie od wschodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Flank_East"" }} ] }}

User: Idźcie do przodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Relative_Forward_50"" }} ] }}
User: Jazda, szarżować na tych łuczników! (W kontekście jest TARGETED ENEMY TAG: 'B_Archers_2')
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""ATTACK"", ""target_location"": ""B_Archers_2"" }} ], ""ai_thought"": ""Szarża jazdy na wskazanych łuczników."" }}

User: Zniszczyć ten oddział! (W kontekście jest TARGETED ENEMY TAG: 'A_Infantry_5')
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""ATTACK"", ""target_location"": ""A_Infantry_5"" }} ], ""ai_thought"": ""Rozkaz ataku na konkretny, zaznaczony oddział."" }}
User: Piechota do lasu na północy!
AI:
{{
  ""commands"": [
    {{ ""unit_tags"": [""Infantry""], ""action"": ""MOVE"", ""target_location"": ""Forest_1_North"" }}
  ],
  ""ai_thought"": ""Wysyłam piechotę na północny skraj lasu.""
}}
User: Bijcie ich! (Context: TARGETED ENEMY TAG: 'B_Infantry_3')
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""ATTACK"", ""target_location"": ""B_Infantry_3"" }} ] }}

User: Zajdźcie ich od wschodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Flank_East"" }} ], ""ai_thought"": ""Wysyłam oddział na wschodnią flankę wroga."" }}


User: Wszyscy do ataku!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""All""], ""action"": ""ATTACK"", ""target_location"": ""Enemy_Army"" }} ] }}

User: Jazda zniszczyć łuczników! (Context: TARGETED ENEMY TAG: 'B_Archers_2')
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""ATTACK"", ""target_location"": ""B_Archers_2"" }} ] }}

User: Zaznaczone jednostki atakować!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""ATTACK"", ""target_location"": ""Enemy_Army"" }} ] }}

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

        // NOWOŚĆ: Sprawdzamy czy mamy zaznaczonego konkretnego wroga
        string enemyInfo = "No specific enemy unit targeted.";
        // Musisz dodać publiczny getter w ArmyHierarchyManager lub zmienić selectedEnemyGroup na public
        var selectedEnemy = armyManager.selectedEnemyGroup;
        if (selectedEnemy != null && selectedEnemy.unitMatrices.Count > 0)
        {
            enemyInfo = $"TARGETED ENEMY TAG: '{selectedEnemy.groupName}'. Enemy Name: '{selectedEnemy.customName}'. Position: {selectedEnemy.groupCenter}.";
        }

        return $"My Units Count: {myCount}. Map Size: {armyManager.maxX}x{armyManager.maxZ}. {enemyInfo}";
    }
}