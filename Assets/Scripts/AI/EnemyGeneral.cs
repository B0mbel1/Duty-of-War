using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Definicja stanów AI
public enum GeneralState
{
    Deployment,      // Faza rozstawiania (oczekiwanie)
    InitialAdvance,  // Marsz w stronę wroga
    Combat,          // Walka kontaktowa
    Chase            // Pościg
}

public class EnemyGeneral : MonoBehaviour
{
    [Header("Konfiguracja AI")]
    // Jak często AI podejmuje decyzje (w sekundach).
    public float decisionInterval = 5.0f; 
    
    // To zostanie nadpisane automatycznie w Start(), ale widoczne w inspektorze
    public string side = "SideB"; 

    [Header("Referencje")]
    public ArmyHierarchyManager armyManager;

    // Zmienne wewnętrzne
    private GeneralState currentState = GeneralState.Deployment;
    private float deploymentTimer = 0f; // Zmieniłem nazwę z deploymentTime na Timer dla jasności
    private float nextDecisionTime = 0f;

    // Listy jednostek (Cache)
    private List<TacticalGroup> myGroups = new List<TacticalGroup>();
    private List<TacticalGroup> enemyGroups = new List<TacticalGroup>();

    void Start()
    {
        // 1. Znajdź Managera Armii
        if (armyManager == null)
            armyManager = FindAnyObjectByType<ArmyHierarchyManager>();

        if (armyManager == null)
        {
            Debug.LogError("EnemyGeneral: KRYTYCZNY BŁĄD - Brak ArmyHierarchyManager na scenie!");
            this.enabled = false;
            return;
        }

        // 2. AUTOMATYCZNY WYBÓR STRONY
        string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");

        if (playerSide == "SideA")
        {
            side = "SideB"; // Gracz = Rzym, AI = Goci
            Debug.Log($"EnemyGeneral: Gracz to {playerSide}. AI przejmuje dowodzenie nad {side} (Goci).");
        }
        else
        {
            side = "SideA"; // Gracz = Goci, AI = Rzym
            Debug.Log($"EnemyGeneral: Gracz to {playerSide}. AI przejmuje dowodzenie nad {side} (Rzym).");
        }

        // 3. Startowe ustawienia
        currentState = GeneralState.Deployment;
        deploymentTimer = Time.time + 5.0f; // 5 sekund na start
    }

    void Update()
    {
        // --- FAZA 1: DEPLOYMENT ---
        if (currentState == GeneralState.Deployment)
        {
            if (Time.time > deploymentTimer)
            {
                currentState = GeneralState.InitialAdvance;
                
                // Odblokowanie ruchu jednostek w Managerze
                if (armyManager != null)
                {
                    armyManager.isDeploymentPhase = false;
                }
                
                Debug.Log($"EnemyGeneral ({side}): Koniec rozstawiania. Atakuję!");
            }
            return;
        }

        // --- FAZA 2: TIMER DECYZYJNY ---
        if (Time.time < nextDecisionTime) return;
        nextDecisionTime = Time.time + decisionInterval;

        // --- FAZA 3: ODŚWIEŻENIE DANYCH ---
        RefreshUnitLists();

        if (myGroups.Count == 0) return; // Przegrana

        // --- FAZA 4: LOGIKA STANU ---
        switch (currentState)
        {
            case GeneralState.InitialAdvance:
                ExecuteAdvanceLogic();
                break;

            case GeneralState.Combat:
                ExecuteCombatLogic();
                break;

            case GeneralState.Chase:
                ExecuteChaseLogic();
                break;
        }
    }

    // --- LOGIKA TAKTYCZNA ---

    void ExecuteAdvanceLogic()
    {
        if (enemyGroups.Count == 0) return;

        Vector3 myCenter = GetArmyCenter(myGroups);
        Vector3 enemyCenter = GetArmyCenter(enemyGroups);
        float dist = Vector3.Distance(myCenter, enemyCenter);

        // Jeśli blisko (np. 100m), przejdź do zwarcia
        if (dist < 100.0f)
        {
            currentState = GeneralState.Combat;
            Debug.Log("EnemyGeneral: Kontakt wzrokowy! Przełączam na tryb COMBAT.");
            ExecuteCombatLogic();
        }
        else
        {
            // Marsz całą armią w stronę wroga
            Vector3 dir = (enemyCenter - myCenter).normalized;
            OrderAllGroups("Line", enemyCenter, dir);
        }
    }

    void ExecuteCombatLogic()
    {
        if (enemyGroups.Count == 0)
        {
            currentState = GeneralState.Chase;
            return;
        }

        foreach (var group in myGroups)
        {
            if (group == null) continue;

            // Używamy groupCenter zamiast transform.position
            TacticalGroup target = FindClosestEnemy(group.groupCenter);

            if (target != null)
            {
                group.chaseTarget = target;
                // Używamy groupCenter zamiast transform.position
                Vector3 direction = (target.groupCenter - group.groupCenter).normalized;
                armyManager.ApplyFormation(group, "Line", target.groupCenter, direction);
            }
        }
    }

    void ExecuteChaseLogic()
    {
        ExecuteCombatLogic();
    }

    // --- METODY POMOCNICZE ---

    void RefreshUnitLists()
    {
        myGroups.Clear();
        enemyGroups.Clear();

        if (armyManager == null) return;

        if (side == "SideB") // AI to Goci
        {
            if (armyManager.factionBGroups != null) myGroups.AddRange(armyManager.factionBGroups);
            if (armyManager.factionAGroups != null) enemyGroups.AddRange(armyManager.factionAGroups);
        }
        else // AI to Rzym (SideA)
        {
            if (armyManager.factionAGroups != null) myGroups.AddRange(armyManager.factionAGroups);
            if (armyManager.factionBGroups != null) enemyGroups.AddRange(armyManager.factionBGroups);
        }

        myGroups.RemoveAll(x => x == null);
        enemyGroups.RemoveAll(x => x == null);
    }

    Vector3 GetArmyCenter(List<TacticalGroup> groups)
    {
        if (groups == null || groups.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        foreach (var g in groups) 
        {
            // POPRAWKA: Używamy groupCenter zamiast transform.position
            if(g != null) sum += g.groupCenter;
        }
        return sum / groups.Count;
    }

    TacticalGroup FindClosestEnemy(Vector3 fromPos)
    {
        TacticalGroup bestTarget = null;
        float minDst = float.MaxValue;

        foreach (var enemy in enemyGroups)
        {
            if (enemy == null) continue;
            // POPRAWKA: Używamy groupCenter zamiast transform.position
            float d = Vector3.Distance(fromPos, enemy.groupCenter);
            if (d < minDst)
            {
                minDst = d;
                bestTarget = enemy;
            }
        }
        return bestTarget;
    }

    void OrderAllGroups(string formation, Vector3 targetPos, Vector3 lookDir)
    {
        foreach (var group in myGroups)
        {
            if (group != null)
            {
                armyManager.ApplyFormation(group, formation, targetPos, lookDir);
            }
        }
    }
}