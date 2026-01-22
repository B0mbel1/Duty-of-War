using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyGeneral : MonoBehaviour
{
    [Header("Ustawienia")]
    public ArmyHierarchyManager armyManager;
    public GeminiNetwork geminiNetwork;
    public string myFaction = "SideB"; // Komputer gra stroną B
    public float decisionInterval = 15.0f; // Co ile sekund AI podejmuje decyzję?
    
    [Header("Stan")]
    public bool isAiActive = false;
    private float timer = 0f;

    void Start()
    {
        // Jeśli chcemy, żeby bot grał od razu po starcie bitwy
        if (geminiNetwork != null)
        {
            // Podpinamy się pod to samo zdarzenie co gracz, 
            // ale musimy uważać, żeby nie wykonywać rozkazów gracza jako swoje!
            // Dlatego stworzymy osobny handler dla bota w GeminiNetwork lub tutaj przefiltrujemy.
            // PROSTSZE ROZWIĄZANIE: Bot używa osobnej metody Callback.
        }
    }

    public void StartAI()
    {
        isAiActive = true;
        Debug.Log($"<color=red>Generał Frakcji {myFaction} przejmuje dowodzenie!</color>");
    }

    void Update()
    {
        if (!isAiActive) return;

        timer += Time.deltaTime;
        if (timer >= decisionInterval)
        {
            timer = 0;
            MakeStrategicDecision();
        }
    }

    void MakeStrategicDecision()
    {
        // 1. Zbieramy dane o bitwie (Co widzi bot?)
        string battleReport = GenerateBattleReport();

        // 2. Budujemy prompt dla Generała AI
        string systemPrompt = ConstructEnemySystemPrompt();
        string fullPrompt = systemPrompt + "\n\nSITUATION REPORT:\n" + battleReport;

        // 3. Wysyłamy do Gemini
        // UWAGA: Musimy zmodyfikować GeminiNetwork, żeby obsługiwało callbacki (odpowiedź do konkretnej osoby),
        // albo zrobimy to "na piechotę" tutaj, tworząc jednorazowe żądanie.
        // Najbezpieczniej dla Ciebie teraz: Użyjmy istniejącego GeminiNetwork, 
        // ale dodajmy flagę, że to zapytanie od Bota.
        
        StartCoroutine(SendBotRequest(fullPrompt));
    }

    // Kopia logiki wysyłania z GeminiNetwork, ale z obsługą odpowiedzi TUTAJ
    IEnumerator SendBotRequest(string prompt)
    {
        Debug.Log($"<color=orange>Generał {myFaction} analizuje pole bitwy...</color>");
        
        // Wywołujemy publiczną metodę GeminiNetwork, ale musimy przechwycić wynik.
        // Ponieważ GeminiNetwork w Twojej wersji wysyła event globalnie, 
        // zrobimy mały "hack" - tymczasowo podepniemy się pod event i zaraz odepniemy.
        
        System.Action<string> tempHandler = null;
        tempHandler = (response) => 
        {
            ExecuteBotOrders(response);
            geminiNetwork.OnResponseReceived -= tempHandler; // Sprzątamy po sobie
        };

        geminiNetwork.OnResponseReceived += tempHandler;
        geminiNetwork.SendRequest(prompt);
        
        yield return null;
    }

    void ExecuteBotOrders(string jsonResponse)
    {
        // To samo co w LLMCommander, ale dla bota
        try
        {
            AICommandResponse response = JsonUtility.FromJson<AICommandResponse>(jsonResponse);
            
            if(!string.IsNullOrEmpty(response.ai_thought)) 
                Debug.Log($"<color=red>Myśl Wroga: {response.ai_thought}</color>");

            foreach (var cmd in response.commands)
            {
                ProcessBotCommand(cmd);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Błąd Generała AI: " + e.Message);
        }
    }

    void ProcessBotCommand(AICommand cmd)
    {
        // Pobieramy jednostki TEJ frakcji (Bota)
        List<TacticalGroup> myUnits = (myFaction == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;
        List<TacticalGroup> selectedUnits = new List<TacticalGroup>();

        // Filtrowanie jednostek (Infantry, Cavalry itp.)
        foreach (string tag in cmd.unit_tags)
        {
            string lowerTag = tag.ToLower();
            if (lowerTag == "all") { selectedUnits.AddRange(myUnits); break; }
            foreach (var group in myUnits)
            {
                if (group.groupName.ToLower().Contains(lowerTag))
                {
                    if (!selectedUnits.Contains(group)) selectedUnits.Add(group);
                }
            }
        }

        if (selectedUnits.Count == 0) return;

        // Interpretacja celu (Gdzie mają iść?)
        Vector3 targetPos = CalculateBotTarget(cmd.target_location);

        // Wykonanie ruchu
        foreach (var group in selectedUnits)
        {
            float clampedX = Mathf.Clamp(targetPos.x, armyManager.minX, armyManager.maxX);
            float clampedZ = Mathf.Clamp(targetPos.z, armyManager.minZ, armyManager.maxZ);
            group.currentDestination = new Vector3(clampedX, targetPos.y, clampedZ);
            group.isMoving = true;
        }
    }

    Vector3 CalculateBotTarget(string locationName)
    {
        string loc = locationName.ToLower();
        float centerX = (armyManager.minX + armyManager.maxX) / 2;
        float centerZ = (armyManager.minZ + armyManager.maxZ) / 2;
        
        // Dla bota "Enemy" to Gracz (SideA)
        List<TacticalGroup> playerUnits = (myFaction == "SideA") ? armyManager.factionBGroups : armyManager.factionAGroups;
        Vector3 playerArmyPos = Vector3.zero;
        if (playerUnits.Count > 0) playerArmyPos = playerUnits[0].groupCenter; // Uproszczenie: pierwszy oddział

        if (loc.Contains("center")) return new Vector3(centerX, 0, centerZ);
        if (loc.Contains("enemy") || loc.Contains("attack")) return playerArmyPos;
        if (loc.Contains("base")) return new Vector3(centerX, 0, (myFaction == "SideB" ? armyManager.maxZ : armyManager.minZ)); // Baza B jest na górze (MaxZ)
        
        // Logika flankowania
        if (loc.Contains("left")) return new Vector3(armyManager.minX + 150, 0, centerZ);
        if (loc.Contains("right")) return new Vector3(armyManager.maxX - 150, 0, centerZ);

        return new Vector3(centerX, 0, centerZ); // Domyślnie środek
    }

    string GenerateBattleReport()
    {
        // Tutaj tworzymy tekstowy opis dla AI
        int myInf = 0, myCav = 0, myArc = 0;
        List<TacticalGroup> myUnits = (myFaction == "SideA") ? armyManager.factionAGroups : armyManager.factionBGroups;
        
        foreach(var g in myUnits)
        {
            if (g.groupName.Contains("Infantry")) myInf += g.unitMatrices.Count;
            if (g.groupName.Contains("Cavalry")) myCav += g.unitMatrices.Count;
            if (g.groupName.Contains("Archer")) myArc += g.unitMatrices.Count;
        }

        // Pobieramy pozycję wroga (uśrednioną)
        List<TacticalGroup> enemyUnits = (myFaction == "SideA") ? armyManager.factionBGroups : armyManager.factionAGroups;
        Vector3 enemyPos = Vector3.zero;
        if (enemyUnits.Count > 0 && enemyUnits[0].unitMatrices.Count > 0) enemyPos = enemyUnits[0].groupCenter;

        return $"My Army: Infantry: {myInf}, Cavalry: {myCav}, Archers: {myArc}. Enemy is roughly at position X:{enemyPos.x:F0}, Z:{enemyPos.z:F0}. Map Size: {armyManager.maxX}x{armyManager.maxZ}.";
    }

    string ConstructEnemySystemPrompt()
    {
        return $@"Jesteś Generałem Frakcji {myFaction} w bitwie Total War. 
Twój cel: Pokonać wroga.
Twoja baza jest na {(myFaction == "SideB" ? "Górze (Max Z)" : "Dole (Min Z)")} mapy.
Otrzymasz raport o siłach. Zdecyduj o strategii.
ODPOWIADAMY TYLKO JSON (format AICommandResponse).
Dostępne akcje: MOVE, ATTACK.
Dostępne cele: Center, Enemy_Army, Left_Flank, Right_Flank, My_Base.
Myśl strategicznie (np. flankuj kawalerią).";
    }
}