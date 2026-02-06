using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Potrzebne do sortowania i filtrowania

// Typy osobowości bota (Realizacja punktu: Odgrywanie scenariuszy)
public enum GeneralPersonality
{
    Balanced,   // Zrównoważony: Atakuje, gdy ma przewagę
    Aggressive, // Rusher: Cała naprzód, ignoruje straty
    Defensive,  // Camper: Trzyma pozycję, kontratakuje tylko gdy wróg blisko
    Flanker     // Taktyk: Próbuje zajść od boków
}

public class EnemyGeneral : MonoBehaviour
{
    [Header("Główne Zależności")]
    public ArmyHierarchyManager armyManager;
    public GeminiNetwork geminiNetwork;

    [Header("Ustawienia Bota")]
    public string myFaction = "SideB"; // Zwykle SideB to wróg (Góra mapy)
    public float decisionInterval = 10.0f; // Co ile sekund myśli?
    
    [Header("Scenariusz i Osobowość")]
    public GeneralPersonality personality = GeneralPersonality.Balanced;
    [TextArea] public string scenarioInstruction = ""; // Np. "Broń rzeki za wszelką cenę."

    [Header("Stan")]
    public bool isAiActive = false;
    public bool isWaitingForResponse = false;
    private float timer = 0f;

    // Cache
    private List<TacticalGroup> myUnits;
    private List<TacticalGroup> enemyUnits;

    void Start()
    {
        // Inicjalizacja list
        RefreshUnitLists();
    }

    public void StartAI()
    {
        isAiActive = true;
        Debug.Log($"<color=red>Generał AI ({personality}) przejmuje dowodzenie!</color>");
        // Pierwsza decyzja od razu po starcie (z małym opóźnieniem żeby wszystko się załadowało)
        timer = decisionInterval - 2.0f; 
    }

    void Update()
    {
        if (!isAiActive || isWaitingForResponse) return;

        timer += Time.deltaTime;
        if (timer >= decisionInterval)
        {
            timer = 0;
            RefreshUnitLists();
            
            // Jeśli nie mam wojska, nie mam czym dowodzić
            if (myUnits.Count == 0) return;

            MakeStrategicDecision();
        }
    }

    void RefreshUnitLists()
    {
        if (myFaction == "SideA")
        {
            myUnits = armyManager.factionAGroups;
            enemyUnits = armyManager.factionBGroups;
        }
        else
        {
            myUnits = armyManager.factionBGroups;
            enemyUnits = armyManager.factionAGroups;
        }
        
        // Czyścimy martwe jednostki z listy lokalnej (dla pewności)
        myUnits = myUnits.Where(g => g.unitMatrices.Count > 0).ToList();
        enemyUnits = enemyUnits.Where(g => g.unitMatrices.Count > 0).ToList();
    }

    // --- 1. MÓZG: GENEROWANIE RAPORTU I PROMPTU ---

    void MakeStrategicDecision()
    {
        isWaitingForResponse = true;

        // A. Analiza Pola Bitwy (Gdzie jest gracz?)
        string battleReport = GenerateDetailedBattleReport();

        // B. Budowanie Promptu w zależności od OSOBOWOŚCI
        string systemPrompt = ConstructSystemPrompt();
        
        string fullPrompt = systemPrompt + "\n\nCURRENT BATTLE REPORT:\n" + battleReport + "\n\nGive me JSON orders.";

        // C. Wysyłka
        StartCoroutine(SendBotRequest(fullPrompt));
    }

    string GenerateDetailedBattleReport()
    {
        // 1. Policz moje siły
        int myInf = 0, myArc = 0, myCav = 0;
        foreach (var g in myUnits)
        {
            if (g.groupName.Contains("Infantry")) myInf++;
            else if (g.groupName.Contains("Archer")) myArc++;
            else if (g.groupName.Contains("Cavalry")) myCav++;
        }

        // 2. Znajdź centrum armii gracza i jego flanki
        Vector3 enemyCenter = Vector3.zero;
        if (enemyUnits.Count > 0)
        {
            foreach (var e in enemyUnits) enemyCenter += e.groupCenter;
            enemyCenter /= enemyUnits.Count;
        }
        else
        {
            // Jeśli nie widzę wroga, zakładam, że jest w swojej bazie (dół mapy dla SideA)
            enemyCenter = new Vector3((armyManager.minX + armyManager.maxX) / 2, 0, armyManager.minZ + 100);
        }

        // 3. Status
        string status = $"My Forces: {myInf} Infantry Groups, {myArc} Archer Groups, {myCav} Cavalry Groups.";
        status += $"\nEnemy Center Position: {enemyCenter}.";
        
        // Dodaj informację o odległości (czy jesteśmy blisko walki?)
        float distToEnemy = Vector3.Distance(GetMyArmyCenter(), enemyCenter);
        status += $"\nDistance to Enemy: {distToEnemy:F0} meters.";

        return status;
    }

    Vector3 GetMyArmyCenter()
    {
        if (myUnits.Count == 0) return Vector3.zero;
        Vector3 center = Vector3.zero;
        foreach (var u in myUnits) center += u.groupCenter;
        return center / myUnits.Count;
    }

    string ConstructSystemPrompt()
    {
        string personaDesc = "";
        switch (personality)
        {
            case GeneralPersonality.Aggressive:
                personaDesc = "Jesteś BRUTALNYM generałem. Twoim celem jest natychmiastowa szarża. Nie cofasz się. Atakuj 'Enemy_Center' wszystkimi siłami.";
                break;
            case GeneralPersonality.Defensive:
                personaDesc = "Jesteś OSTROŻNYM generałem. Trzymaj pozycję ('HOLD'). Atakuj tylko, gdy wróg podejdzie bardzo blisko (< 100m). Używaj łuczników.";
                break;
            case GeneralPersonality.Flanker:
                personaDesc = "Jesteś SPRYTNYM taktykiem. Wysyłaj Piechotę ('Infantry') na środek ('Enemy_Center'), ale Kawalerię ('Cavalry') wyślij na flanki ('Flank_Left' lub 'Flank_Right').";
                break;
            case GeneralPersonality.Balanced:
            default:
                personaDesc = "Jesteś DOŚWIADCZONYM generałem. Dostosuj się. Używaj formacji.";
                break;
        }

        if (!string.IsNullOrEmpty(scenarioInstruction))
        {
            personaDesc += $" DODATKOWY ROZKAZ SCENARIUSZA: {scenarioInstruction}";
        }

        return $@"Wcielasz się w Generała AI w grze RTS.
{personaDesc}
Mapa: X(0-2000), Z(0-2000). Ty jesteś '{myFaction}'.
ZASADY:
1. Odpowiadaj TYLKO JSON (format AICommandResponse).
2. target_location (Możliwe wartości):
   - 'Enemy_Center' (Główne siły wroga)
   - 'Flank_Left' (Lewa flanka wroga)
   - 'Flank_Right' (Prawa flanka wroga)
   - 'My_Base' (Wycofanie)
   - 'Hold_Position' (Stanie w miejscu)
3. action: 'ATTACK' (Ruch ofensywny), 'MOVE' (Manewr), 'STOP'.
4. unit_tags: 'Infantry', 'Archer', 'Cavalry', 'All'.

Przykład JSON:
{{ ""commands"": [ {{ ""unit_tags"": [""Cavalry""], ""action"": ""ATTACK"", ""target_location"": ""Flank_Left"" }} ], ""ai_thought"": ""Jazda flankuje wroga."" }}";
    }

    // --- 2. KOMUNIKACJA (PLUMBING) ---

    IEnumerator SendBotRequest(string prompt)
    {
        // Hack: Podpinamy się pod event GeminiNetwork tylko na chwilę, żeby złapać odpowiedź dla bota
        System.Action<string> botHandler = null;
        bool received = false;

        botHandler = (responseJson) =>
        {
            ExecuteBotOrders(responseJson);
            received = true;
        };

        geminiNetwork.OnResponseReceived += botHandler;
        geminiNetwork.SendRequest(prompt);

        // Czekamy na odpowiedź max 10 sekund (żeby nie zwiesić bota na zawsze)
        float timeout = 10f;
        while (!received && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        geminiNetwork.OnResponseReceived -= botHandler; // Odpinamy się
        isWaitingForResponse = false;
    }

    // --- 3. WYKONANIE (ACTION) ---

    void ExecuteBotOrders(string jsonResponse)
    {
        try
        {
            AICommandResponse response = JsonUtility.FromJson<AICommandResponse>(jsonResponse);

            if (!string.IsNullOrEmpty(response.ai_thought))
                Debug.Log($"<color=orange>[AI Generał]: {response.ai_thought}</color>");

            foreach (var cmd in response.commands)
            {
                ProcessBotCommand(cmd);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Błąd Bota: " + e.Message);
            isWaitingForResponse = false;
        }
    }

    void ProcessBotCommand(AICommand cmd)
    {
        // 1. Wybierz jednostki
        List<TacticalGroup> selectedGroups = new List<TacticalGroup>();
        foreach (string tag in cmd.unit_tags)
        {
            string lowerTag = tag.ToLower();
            if (lowerTag == "all") { selectedGroups.AddRange(myUnits); break; }
            
            foreach (var group in myUnits)
            {
                if (group.groupName.ToLower().Contains(lowerTag))
                {
                    if (!selectedGroups.Contains(group)) selectedGroups.Add(group);
                }
            }
        }

        if (selectedGroups.Count == 0) return;

        // 2. Oblicz cel (Gdzie to jest na mapie?)
        Vector3 targetPos = CalculateTargetPosition(cmd.target_location);
        
        // 3. Wykonaj rozkaz używając ArmyHierarchyManager (żeby fizyka i unikanie działały!)
        foreach (var group in selectedGroups)
        {
            if (cmd.action == "STOP" || cmd.target_location == "Hold_Position")
            {
                group.isMoving = false;
                group.chaseTarget = null;
                continue;
            }

            // Używamy ApplyFormation, bo ona obsługuje logikę ruchu, formacji i PendingOrder
            // Dzięki temu bot "myśli" tak samo jak gracz
            Vector3 direction = (targetPos - group.groupCenter).normalized;
            
            // Logika ataku: jeśli bot atakuje "Enemy_Center", to każemy mu szukać celów (tak jak u gracza)
            if (cmd.action == "ATTACK")
            {
                // Znajdź najbliższego wroga dla tej grupy (żeby nie zbiegali się w jeden punkt)
                TacticalGroup closestEnemy = armyManager.FindClosestEnemy(group.groupCenter, enemyUnits);
                
                if (closestEnemy != null)
                {
                    group.chaseTarget = closestEnemy;
                    targetPos = closestEnemy.groupCenter;
                    direction = (targetPos - group.groupCenter).normalized;
                }
            }

            armyManager.ApplyFormation(group, "Line", targetPos, direction);
        }
    }

    Vector3 CalculateTargetPosition(string locKey)
    {
        Vector3 enemyCenter = Vector3.zero;
        if (enemyUnits.Count > 0)
        {
            foreach (var u in enemyUnits) enemyCenter += u.groupCenter;
            enemyCenter /= enemyUnits.Count;
        }
        else
        {
            // Domyślna pozycja wroga (Baza A)
            enemyCenter = new Vector3((armyManager.minX + armyManager.maxX) / 2, 0, armyManager.minZ + 100);
        }

        Vector3 myBase = new Vector3((armyManager.minX + armyManager.maxX) / 2, 0, armyManager.maxZ - 100);

        switch (locKey)
        {
            case "Enemy_Center":
                return enemyCenter;
            
            case "Flank_Left":
                // Lewa strona WROGA (czyli nasze prawo, jeśli patrzymy z góry, ale prościej: X+)
                return enemyCenter + new Vector3(300, 0, 0); 
            
            case "Flank_Right":
                return enemyCenter + new Vector3(-300, 0, 0);
            
            case "My_Base":
                return myBase;

            default:
                return enemyCenter; // Domyślnie atak
        }
    }
}