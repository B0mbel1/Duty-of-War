using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;


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
        if (armyManager == null)
        {
            armyManager = FindAnyObjectByType<ArmyHierarchyManager>();

            if (armyManager == null)
            {
                Debug.LogError("BŁĄD KRYTYCZNY: LLMCommander nie znalazł ArmyHierarchyManager na tej mapie!");
                return;
            }
        }

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

            commandInput.onSelect.AddListener(OnInputSelected);
        }

        if (commandInput != null)
        {
            originalTextColor = commandInput.textComponent.color;

            commandInput.onSubmit.AddListener(val =>
            {
                if (!string.IsNullOrWhiteSpace(val))
                {
                    string systemPrompt = ConstructSystemPrompt();
                    string fullPrompt = systemPrompt + "\nUser: " + val;

                    geminiNetwork.SendRequest(fullPrompt);
                    commandInput.text = "";
                    commandInput.ActivateInputField();
                    armyManager.HideCursorMarker();
                }
            });
        }
    }

    public void OnSendButtonClicked()
    {
        if (commandInput != null && !string.IsNullOrEmpty(commandInput.text))
        {
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
            StopAllCoroutines();
            commandInput.text = "Generale! Polecenia są niejasne!";
            commandInput.textComponent.color = Color.red;
            StartCoroutine(HideErrorAfterDelay());
        }
    }

    void OnInputSelected(string val)
    {
        if (commandInput.textComponent.color == Color.red)
        {
            StopAllCoroutines();
            commandInput.text = "";
            commandInput.textComponent.color = originalTextColor;
        }
    }

    System.Collections.IEnumerator HideErrorAfterDelay()
    {
        yield return new WaitForSeconds(2.0f);

        if (commandInput != null && commandInput.textComponent.color == Color.red)
        {
            commandInput.text = "";
            commandInput.textComponent.color = originalTextColor;
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
                ShowErrorInInput();
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
            ShowErrorInInput();
        }
    }

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
            if (lowerTag == "selected")
            {
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
            Vector3? flankPosition = null;

            if (cmd.target_location.StartsWith("Flank"))
            {
                Vector3 centerPoint = GetEnemyArmyCenter();
                if (armyManager.selectedEnemyGroup != null && armyManager.selectedEnemyGroup.unitMatrices.Count > 0)
                {
                    centerPoint = armyManager.selectedEnemyGroup.groupCenter;
                    targetEnemy = armyManager.selectedEnemyGroup;
                }

                float offset = 70.0f;
                string dir = cmd.target_location.ToLower();

                if (dir.Contains("north")) flankPosition = centerPoint + new Vector3(0, 0, offset);
                else if (dir.Contains("south")) flankPosition = centerPoint + new Vector3(0, 0, -offset);
                else if (dir.Contains("east")) flankPosition = centerPoint + new Vector3(offset, 0, 0);
                else if (dir.Contains("west")) flankPosition = centerPoint + new Vector3(-offset, 0, 0);

                if (flankPosition == null)
                {
                    Debug.LogWarning($"[LLMCommander] AI podało nieznaną flankę: '{cmd.target_location}'. Ustawiam domyślną (East).");
                    flankPosition = centerPoint + new Vector3(offset, 0, 0);
                }

                foreach (var group in selectedUnits)
                {
                    Vector3 lookDir = (centerPoint - flankPosition.Value).normalized;
                    StartCoroutine(FlankAndStrikeSequence(group, flankPosition.Value, lookDir, targetEnemy));
                }
                return;
            }
            else if (armyManager.selectedEnemyGroup != null && armyManager.selectedEnemyGroup.unitMatrices.Count > 0)
            {
                targetEnemy = armyManager.selectedEnemyGroup;
            }
            else if (cmd.target_location != "Enemy_Army" && !string.IsNullOrEmpty(cmd.target_location))
            {
                targetEnemy = armyManager.GetGroupByName(cmd.target_location);
            }

            if (flankPosition.HasValue)
            {
                foreach (var group in selectedUnits)
                {
                    group.assignedFeature = null;
                    group.chaseTarget = targetEnemy;

                    Vector3 lookDir = (GetEnemyArmyCenter() - flankPosition.Value).normalized;
                    if (targetEnemy != null) lookDir = (targetEnemy.groupCenter - flankPosition.Value).normalized;

                    armyManager.ApplyFormation(group, "Line", flankPosition.Value, lookDir);
                }
                return;
            }

            if (targetEnemy != null)
            {
                foreach (var group in selectedUnits)
                {
                    group.assignedFeature = null;
                    group.chaseTarget = targetEnemy;
                    Vector3 directionToEnemy = (targetEnemy.groupCenter - group.groupCenter).normalized;
                    armyManager.ApplyFormation(group, "Line", targetEnemy.groupCenter, directionToEnemy);
                }
                return;
            }

            List<TacticalGroup> allEnemies = (playerSide == "SideA") ? armyManager.factionBGroups : armyManager.factionAGroups;
            foreach (var group in selectedUnits)
            {
                group.assignedFeature = null;
                group.chaseTarget = null;
                TacticalGroup myPersonalTarget = armyManager.FindClosestEnemy(group.groupCenter, allEnemies);

                if (myPersonalTarget != null)
                {
                    group.chaseTarget = myPersonalTarget;
                    Vector3 directionToTarget = (myPersonalTarget.groupCenter - group.groupCenter).normalized;
                    armyManager.ApplyFormation(group, "Line", myPersonalTarget.groupCenter, directionToTarget);
                }
                else
                {
                    Vector3 fallbackPos = GetEnemyArmyCenter();
                    Vector3 dir = (fallbackPos - group.groupCenter).normalized;
                    armyManager.ApplyFormation(group, "Line", fallbackPos, dir);
                }
            }
            return;
        }
        if (cmd.action == "STOP")
        {
            foreach (var group in selectedUnits)
            {
                group.chaseTarget = null;
                group.isMoving = false;
                group.isReforming = false;
                group.currentDestination = group.groupCenter;
                group.targetFacingDirection = Vector3.zero;
                Debug.Log($"Grupa {group.groupName} zatrzymana na rozkaz.");
                group.assignedFeature = null;
            }
            return;
        }

        if (cmd.action == "FORM")
        {
            string formType = cmd.target_location;

            if (armyManager.hasCursorPosition)
            {
                targetCursorPos = armyManager.lastCursorPosition;
            }

            foreach (var g in selectedUnits)
            {
                g.chaseTarget = null;
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

        if (cmd.target_location.StartsWith("Relative_"))
        {
            string[] parts = cmd.target_location.Split('_');
            if (parts.Length == 3)
            {
                string directionStr = parts[1].ToLower();
                float distance = 0;
                if (float.TryParse(parts[2], out distance))
                {
                    foreach (var group in selectedUnits)
                    {
                        if (group.unitMatrices.Count == 0) continue;

                        Quaternion groupRotation = group.unitMatrices[0].rotation;
                        Vector3 currentForward = groupRotation * Vector3.forward;
                        Vector3 localDirection = Vector3.forward;

                        switch (directionStr)
                        {
                            case "forward": localDirection = groupRotation * Vector3.forward; break;
                            case "backward": localDirection = groupRotation * Vector3.back; break;
                            case "left": localDirection = groupRotation * Vector3.left; break;
                            case "right": localDirection = groupRotation * Vector3.right; break;
                        }

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
                            group.targetFacingDirection = localDirection.normalized;
                        }

                        Vector3 newPos = group.groupCenter + (localDirection * distance);

                        float clampedX = Mathf.Clamp(newPos.x, armyManager.minX, armyManager.maxX);
                        float clampedZ = Mathf.Clamp(newPos.z, armyManager.minZ, armyManager.maxZ);
                        group.chaseTarget = null;
                        group.currentDestination = new Vector3(clampedX, group.groupCenter.y, clampedZ);
                        group.isMoving = true;
                        group.isReforming = true;
                    }
                    Debug.Log($"Przesuwam jednostki o {distance}m ({directionStr})");
                    return;
                }
            }
        }

        TerrainFeature targetFeature = null;
        string targetSuffix = "";
        string cleanLocName = cmd.target_location;

        if (cleanLocName.EndsWith("_Front")) { targetSuffix = "Front"; cleanLocName = cleanLocName.Replace("_Front", ""); }
        else if (cleanLocName.EndsWith("_Back")) { targetSuffix = "Back"; cleanLocName = cleanLocName.Replace("_Back", ""); }
        else if (cleanLocName.EndsWith("_Top")) { targetSuffix = "Top"; cleanLocName = cleanLocName.Replace("_Top", ""); }

        TerrainFeature[] allFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        foreach (var f in allFeatures)
        {
            if (f.featureName.Trim().ToLower() == cleanLocName.Trim().ToLower())
            {
                targetFeature = f;
                break;
            }
        }

        if (targetFeature == null && !cleanLocName.Contains("Relative") && !cleanLocName.Contains("Flank") && !useCursor && !cleanLocName.Contains("Enemy"))
        {
            string available = string.Join(", ", allFeatures.Select(f => f.featureName));
            Debug.LogWarning($"[LLMCommander] AI zwróciło nazwę: '{cleanLocName}', ale nie znaleziono takiego obiektu!\nDOSTĘPNE: {available}");
        }
        if (targetFeature != null && (targetFeature.lineLength > 1.0f || targetFeature.isRiver || targetFeature.isHill))
        {
            foreach (var group in selectedUnits)
            {
                armyManager.MoveGroupToLineFeature(group, targetFeature, targetSuffix);
            }
            return;
        }

        Vector3 targetPosition = Vector3.zero;
        bool positionFound = CalculateTargetPosition(cmd.target_location, out targetPosition);

        if (positionFound)
        {
            Vector3 enemyCenter = GetEnemyArmyCenter();
            if (armyManager.selectedEnemyGroup != null && armyManager.selectedEnemyGroup.unitMatrices.Count > 0)
            {
                enemyCenter = armyManager.selectedEnemyGroup.groupCenter;
            }

            bool isFlankingCommand = cmd.target_location.ToLower().Contains("flank");

            foreach (var group in selectedUnits)
            {
                Vector3 finalDest = new Vector3(Mathf.Clamp(targetPosition.x, armyManager.minX, armyManager.maxX),
                                                targetPosition.y,
                                                Mathf.Clamp(targetPosition.z, armyManager.minZ, armyManager.maxZ));

                group.chaseTarget = null;

                if (isFlankingCommand)
                {
                    Vector3 directionToEnemy = (enemyCenter - finalDest).normalized;
                    Vector3 attackDest = enemyCenter;

                    attackDest -= directionToEnemy * 2.0f;
                    armyManager.ApplyFormation(group, "Line", attackDest, directionToEnemy);
                    group.currentDestination = attackDest;
                }
                else
                {
                    group.currentDestination = finalDest;
                    group.targetFacingDirection = Vector3.zero;
                    group.isReforming = false;
                    group.isMoving = true;
                }

                Debug.Log($"Grupa {group.groupName} rusza do {group.currentDestination} (Flank: {isFlankingCommand})");
            }
        }
    }

    Vector3 GetDirectionVector(string dirName)
    {
        bool isSideA = (playerSide == "SideA");

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

    bool CalculateTargetPosition(string locationName, out Vector3 position)
    {
        position = Vector3.zero;
        string loc = locationName.ToLower();

        float centerX = (armyManager.minX + armyManager.maxX) / 2;
        float centerZ = (armyManager.minZ + armyManager.maxZ) / 2;
        float mapMargin = 150f;

        TerrainFeature[] allFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        foreach (var feature in allFeatures)
        {
            if (feature.featureName.ToLower() == loc)
            {
                position = feature.transform.position;
                return true;
            }
        }

        if (loc.Contains("center")) { position = new Vector3(centerX, 0, centerZ); return true; }
        if (loc.Contains("enemy") || loc.Contains("attack")) { position = GetEnemyArmyCenter(); return true; }
        if (loc.Contains("base") || loc.Contains("retreat"))
        {
            bool isSideA = (playerSide == "SideA");
            float retreatZ = isSideA ? (armyManager.minZ + 150) : (armyManager.maxZ - 150);
            position = new Vector3(centerX, 0, retreatZ);
            return true;
        }

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

        if (loc.StartsWith("flank"))
        {
            Vector3 targetPoint = GetEnemyArmyCenter();
            if (armyManager.selectedEnemyGroup != null && armyManager.selectedEnemyGroup.unitMatrices.Count > 0)
            {
                targetPoint = armyManager.selectedEnemyGroup.groupCenter;
            }

            float offset = 15.0f; 

            if (loc.Contains("north")) position = targetPoint + new Vector3(0, 0, offset); 
            else if (loc.Contains("south")) position = targetPoint + new Vector3(0, 0, -offset); 
            else if (loc.Contains("east")) position = targetPoint + new Vector3(offset, 0, 0);  
            else if (loc.Contains("west")) position = targetPoint + new Vector3(-offset, 0, 0); 
            else position = targetPoint + new Vector3(offset, 0, 0); 

            return true;
        }

        GameObject featureObj = GameObject.Find(locationName);
        if (featureObj != null)
        {
            position = featureObj.transform.position;
            return true;
        }

        return false;
    }
    System.Collections.IEnumerator FlankAndStrikeSequence(TacticalGroup group, Vector3 flankPos, Vector3 lookDir, TacticalGroup targetEnemy)
    {
        group.chaseTarget = null;
        group.assignedFeature = null;
        armyManager.ApplyFormation(group, "Line", flankPos, lookDir);

        Debug.Log($"[FLANK] {group.groupName} rozpoczyna manewr na pozycję...");

        while (group != null && group.unitMatrices.Count > 0)
        {
            float dist = Vector3.Distance(group.groupCenter, flankPos);

            if (dist < 12.0f && !group.isMoving) break;
            if (Vector3.Distance(group.currentDestination, flankPos) > 1.0f)
            {
                Debug.Log($"[FLANK] Manewr przerwany przez gracza dla {group.groupName}");
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        if (group == null || group.unitMatrices.Count == 0) yield break;

        Debug.Log($"[FLANK] {group.groupName} na pozycji. Wyrównanie szyku...");
        yield return new WaitForSeconds(1.5f); // 1.5 sekundy pauzy

        if (targetEnemy != null && targetEnemy.unitMatrices.Count > 0)
        {
            Debug.Log($"[FLANK] {group.groupName} SZARŻUJE na {targetEnemy.groupName}!");
            group.chaseTarget = targetEnemy;
            Vector3 attackDir = (targetEnemy.groupCenter - group.groupCenter).normalized;
            armyManager.ApplyFormation(group, "Line", targetEnemy.groupCenter, attackDir);
        }
        else
        {
            Vector3 fallback = GetEnemyArmyCenter();
            armyManager.ApplyFormation(group, "Line", fallback, (fallback - group.groupCenter).normalized);
        }
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
   - TŁUMACZENIE NAZW (Mianownik -> Nazwa Obiektu):
     * Jeśli gracz mówi 'Północne wzgórze' lub 'Wzgórza na północy' -> szukaj obiektu zawierającego 'Północ' (np. 'Wzgórze Północ').
     * Jeśli gracz mówi 'Wschodnie wzgórze' -> szukaj 'Wzgórze Wschód'.
     * Jeśli gracz mówi 'Południowy las' -> szukaj 'Las Południe'.
     * 'Zachodnie' -> 'Zachód', 'Centralne' -> 'Centrum'.
   - SUFIKSY (Dla wzgórz):
     * 'Zajmij', 'Wejdź', 'Na szczyt' -> Dodaj '_Top' (np. 'Wzgórze Północ_Top').
     * 'Przed', 'U podnóża', 'Pod' -> Dodaj '_Front' (np. 'Wzgórze Północ_Front').
     * 'Za', 'Schowaj się', 'Druga strona' -> Dodaj '_Back' (np. 'Wzgórze Północ_Back').
   - DLA KOMENDY STOP: 'None'.
   - DLA PRZESUNIĘĆ (np. 100m w lewo): 'Relative_{{Kierunek}}_{{Metry}}'. Kierunki: Forward, Backward, Left, Right.
   - DLA 'FORM' (Formacje): 'Line', 'Double_Line', 'Box', 'Turtle', 'Loose' (Luźna), 'Wedge' (Klin), 'Square' (Czworobok), 'Column' (Kolumna).
   - SPECJALNE: 'Cursor' - UŻYJ TEGO, gdy gracz mówi 'tam', 'tutaj', 'w miejscu kliknięcia', 'do znacznika' LUB wydaje rozkaz formacji bez podania miejsca (np. 'Uformuj linię').
   - FLANKOWANIE (Absolutne): 'Flank_North' (Podejdź od strony Z+), 'Flank_South' (Od Z-), 'Flank_East' (Od X+), 'Flank_West' (Od X-).
   - 'Cursor' - gdy gracz wskazuje 'tam', 'tutaj'.
   - KONKRETNY CEL: Jeśli w CONTEXT jest 'TARGETED ENEMY TAG' (np. 'B_Infantry_1'), użyj tego TAGU jako target_location przy rozkazie ataku na 'tego wroga'.
   - 'Enemy_Army' - UŻYJ TEGO, gdy gracz mówi 'Do ataku', 'Atakować', 'Zniszczyć wroga', a nie wskazał konkretnego oddziału ani nie kliknął na nikogo.
4. action: 'MOVE', 'ATTACK' - użyj, gdy gracz mówi 'bij', 'atakuj', 'szarżuj','oflankuj'., 'STOP', 'FORM'.

PRZYKŁADY (Zawsze używaj tablicy 'commands'!):
User: Atakujcie od wschodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Flank_East"" }} ] }}

User: Idźcie do przodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Relative_Forward_50"" }} ] }}
User: Jazda, szarżować na tych łuczników! (W kontekście jest TARGETED ENEMY TAG: 'B_Archers_2')
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""ATTACK"", ""target_location"": ""B_Archers_2"" }} ], ""ai_thought"": ""Szarża jazdy na wskazanych łuczników."" }}
User: Łucznicy zająć wzgórze na północy!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Archer""], ""action"": ""MOVE"", ""target_location"": ""Hill_North_Top"" }} ] }}
User: Zajmijcie północne wzgórze!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Wzgórze Północ_Top"" }} ] }}

User: Jazda, schowajcie się za wschodnim lasem!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""MOVE"", ""target_location"": ""Las Wschód_Back"" }} ] }}

User: Ustawcie się przed południowym wzgórzem.
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""MOVE"", ""target_location"": ""Wzgórze Południe_Front"" }} ] }}
User: Piechota ustawić się przed wzgórzem!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Infantry""], ""action"": ""MOVE"", ""target_location"": ""Hill_North_Front"" }} ] }}
User: Jazda ustawić się przed wzgórzem!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""MOVE"", ""target_location"": ""Wzgórze Czaszki_Front"" }} ] }}
User: Jazda, schowajcie się za wzgórzem!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""MOVE"", ""target_location"": ""Hill_North_Back"" }} ] }}
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
User: Atakujcie od wschodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""ATTACK"", ""target_location"": ""Flank_East"" }} ] }}

User: Jazda, oflankować ich od zachodu!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""ATTACK"", ""target_location"": ""Flank_West"" }} ] }}

User: Zaznaczeni atak od południa!
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""ATTACK"", ""target_location"": ""Flank_South"" }} ] }}

User: Do ataku! (Bez kierunku)
AI: {{ ""commands"": [ {{ ""unit_tags"": [""Selected""], ""action"": ""ATTACK"", ""target_location"": ""Enemy_Army"" }} ] }}

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

        string enemyInfo = "No specific enemy unit targeted.";
        var selectedEnemy = armyManager.selectedEnemyGroup;
        if (selectedEnemy != null && selectedEnemy.unitMatrices.Count > 0)
        {
            enemyInfo = $"TARGETED ENEMY TAG: '{selectedEnemy.groupName}'. Enemy Name: '{selectedEnemy.customName}'. Position: {selectedEnemy.groupCenter}.";
        }

        return $"My Units Count: {myCount}. Map Size: {armyManager.maxX}x{armyManager.maxZ}. {enemyInfo}";
    }
}