using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EnemyGeneral : MonoBehaviour
{
    [Header("Strategia Bitwy")]
    public AIStrategyProfile strategyProfile;
    public string side = "SideB";

    [Header("Dostępne Profile")]
    public AIStrategyProfile profileForGoths;
    public AIStrategyProfile profileForRomans;

    [Header("Status")]
    public int currentPhaseIndex = -1;
    public string currentPhaseName = "Oczekiwanie";

    [Header("Referencje")]
    public ArmyHierarchyManager armyManager;

    private float battleStartTime;
    private float phaseStartTime;
    private bool battleStarted = false;
    private List<TacticalGroup> myGroups = new List<TacticalGroup>();
    private List<TacticalGroup> enemyGroups = new List<TacticalGroup>();

    private int startingUnitCount = 0;

    void Start()
    {
        if (armyManager == null) armyManager = FindAnyObjectByType<ArmyHierarchyManager>();

        string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
        if (playerSide == "SideA")
        {
            side = "SideB";
            strategyProfile = profileForGoths;
        }
        else
        {
            side = "SideA";
            strategyProfile = profileForRomans;
        }

        Debug.Log($"[EnemyGeneral] Przejmuję dowodzenie nad {side} używając strategii: {(strategyProfile ? strategyProfile.name : "BRAK")}");
    }

    void Update()
    {
        if (armyManager == null || !armyManager.groupsInitialized) return;
        if (!battleStarted)
        {
            battleStarted = true;
            battleStartTime = Time.time;

            RefreshUnitLists();
            startingUnitCount = CountMyUnits();
            AdvancePhase();
        }

        if (armyManager.isDeploymentPhase) return;
        if (strategyProfile == null || currentPhaseIndex >= strategyProfile.battlePhases.Count) return;

        CheckPhaseTriggers();
    }

    void CheckPhaseTriggers()
    {
        if (currentPhaseIndex < 0) return;

        AIPhase currentPhase = strategyProfile.battlePhases[currentPhaseIndex];
        bool triggerMet = false;

        switch (currentPhase.trigger)
        {
            case AITriggerType.TimeElapsed:
                if (Time.time - phaseStartTime >= currentPhase.triggerValue) triggerMet = true;
                break;

            case AITriggerType.EnemyDistance:
                float dist = GetDistanceToNearestEnemy();
                if (dist <= currentPhase.triggerValue) triggerMet = true;
                break;

            case AITriggerType.CasualtiesPercent:
                float currentCount = CountMyUnits();
                float lossRatio = 1.0f - (currentCount / (float)startingUnitCount);
                if (lossRatio >= currentPhase.triggerValue) triggerMet = true;
                break;

            case AITriggerType.Immediate:
                triggerMet = true;
                break;
        }

        if (triggerMet)
        {
            AdvancePhase();
        }
    }

    void AdvancePhase()
    {
        currentPhaseIndex++;
        if (strategyProfile != null && currentPhaseIndex < strategyProfile.battlePhases.Count)
        {
            AIPhase nextPhase = strategyProfile.battlePhases[currentPhaseIndex];
            currentPhaseName = nextPhase.phaseName;
            phaseStartTime = Time.time;

            Debug.Log($"<color=red>[GENERAL {side}] Rozpoczynam fazę: {nextPhase.phaseName}</color>");
            ExecuteCommands(nextPhase.commands);
        }
        else
        {
            currentPhaseName = "Bitwa Zakończona / Brak Rozkazów";
        }
    }

    void ExecuteCommands(List<AICommandData> commands)
    {
        RefreshUnitLists();

        foreach (var cmd in commands)
        {
            List<TacticalGroup> selectedUnits = FilterUnits(cmd.unitTag);
            if (selectedUnits.Count == 0) continue;

            foreach (var group in selectedUnits)
            {
                ProcessSingleCommand(group, cmd);
            }
        }
    }

    void ProcessSingleCommand(TacticalGroup group, AICommandData cmd)
    {
        Vector3 targetPos = Vector3.zero;
        Vector3 lookDir = Vector3.zero;
        TerrainFeature feature = null;

        switch (cmd.action)
        {
            case AIActionType.MoveTo:
                targetPos = CalculatePosition(cmd.targetName);
                armyManager.ApplyFormation(group, cmd.formation, targetPos);
                break;

            case AIActionType.DefendFeature:
                feature = FindFeature(cmd.targetName);
                if (feature != null)
                {
                    armyManager.MoveGroupToLineFeature(group, feature, "Top");
                }
                break;

            case AIActionType.AttackMove:
                Vector3 enemyCenter = GetEnemyArmyCenter();
                lookDir = (enemyCenter - group.groupCenter).normalized;
                group.chaseTarget = null;
                armyManager.ApplyFormation(group, cmd.formation, enemyCenter, lookDir);
                break;

            case AIActionType.ChargeTarget:
                TacticalGroup specificEnemy = FindEnemyByNameOrTag(cmd.targetName);
                if (specificEnemy != null)
                {
                    group.chaseTarget = specificEnemy;
                    armyManager.ApplyFormation(group, cmd.formation, specificEnemy.groupCenter);
                }
                else
                {
                    Vector3 eCenter = GetEnemyArmyCenter();
                    armyManager.ApplyFormation(group, cmd.formation, eCenter);
                }
                break;

            case AIActionType.Flank:
                Vector3 enemyC = GetEnemyArmyCenter();
                Vector3 flankOffset = Vector3.zero;

                if (cmd.targetName.Contains("West")) flankOffset = new Vector3(-100, 0, 0);
                else if (cmd.targetName.Contains("East")) flankOffset = new Vector3(100, 0, 0);
                else if (cmd.targetName.Contains("North")) flankOffset = new Vector3(0, 0, 100);
                else if (cmd.targetName.Contains("South")) flankOffset = new Vector3(0, 0, -100);

                Vector3 flankPos = enemyC + flankOffset;
                Vector3 flankDir = (enemyC - flankPos).normalized;

                armyManager.ApplyFormation(group, cmd.formation, flankPos, flankDir);
                break;

            case AIActionType.HoldPosition:
                group.isMoving = false;
                group.chaseTarget = null;
                break;
        }
    }

    List<TacticalGroup> FilterUnits(string tag)
    {
        if (tag.ToLower() == "all") return new List<TacticalGroup>(myGroups);

        List<TacticalGroup> result = new List<TacticalGroup>();
        foreach (var g in myGroups)
        {
            bool nameMatch = g.groupName.ToLower().Contains(tag.ToLower());
            bool typeMatch = g.archetypes.Count > 0 && g.archetypes[0].type.ToString().ToLower() == tag.ToLower();

            if (nameMatch || typeMatch) result.Add(g);
        }
        return result;
    }

    Vector3 CalculatePosition(string name)
    {
        // 1. Najpierw sprawdź, czy istnieje obiekt o dokładnej nazwie (np. "Center" albo jakiś ręczny marker)
        GameObject directObj = GameObject.Find(name);
        if (directObj != null) return directObj.transform.position;

        // 2. Obsługa sufiksów (_Front, _Back, _Top) dla obiektów z TerrainScanner
        string baseName = name;
        string suffix = "";

        if (name.Contains("_Front")) { baseName = name.Replace("_Front", ""); suffix = "Front"; }
        else if (name.Contains("_Back")) { baseName = name.Replace("_Back", ""); suffix = "Back"; }
        else if (name.Contains("_Top")) { baseName = name.Replace("_Top", ""); suffix = "Top"; }

        // Szukamy obiektu bazowego (np. szukamy "Auto_Wzgórze Północ" gdy AI chce "Wzgórze Północ")
        // Twój Scanner dodaje prefiks "Auto_", więc musimy to uwzględnić, jeśli AI poda samą nazwę
        GameObject featureObj = GameObject.Find(baseName);
        if (featureObj == null) featureObj = GameObject.Find("Auto_" + baseName);

        if (featureObj != null)
        {
            TerrainFeature feature = featureObj.GetComponent<TerrainFeature>();
            if (feature != null)
            {
                // Pobieramy kierunek "szerokości" (linii) obiektu
                Vector3 rightDir = feature.GetWorldDirection();
                // Obliczamy wektor "do przodu" (prostopadły do linii)
                Vector3 forwardDir = Vector3.Cross(Vector3.up, rightDir).normalized;

                // Zabezpieczenie: Czasami Cross product wychodzi w stronę góry mapy lub dołu, 
                // w zależności od tego jak ułożyła się linia. 
                // Zakładamy, że "Front" to strona skierowana do środka mapy (dla wroga) lub w stronę gracza.
                // Ale najprościej przyjąć offset z TerrainFeature:

                float offsetDist = feature.widthOffset; // Używamy widthOffset z Twojego skryptu TerrainFeature

                switch (suffix)
                {
                    case "Front":
                        // Pozycja "przed" obiektem (zakładamy, że front to -forwardDir lub +forwardDir)
                        // Tutaj prosty hack: Front to zawsze "bliżej środka mapy"
                        return featureObj.transform.position - (forwardDir * offsetDist);

                    case "Back":
                        // Pozycja "za" obiektem (ukryta)
                        return featureObj.transform.position + (forwardDir * offsetDist);

                    case "Top":
                    default:
                        return featureObj.transform.position;
                }
            }
            return featureObj.transform.position;
        }

        // 3. Fallbacki
        if (name == "Center") return new Vector3((armyManager.minX + armyManager.maxX) / 2, 0, (armyManager.minZ + armyManager.maxZ) / 2);
        if (name == "Enemy_Army") return GetEnemyArmyCenter();

        // Obsługa Flankowania (jeśli nie znaleziono obiektu, a nazwa to np. Flank_East)
        if (name.Contains("Flank"))
        {
            Vector3 enemyC = GetEnemyArmyCenter();
            if (name.Contains("West")) return enemyC + new Vector3(-100, 0, 0);
            if (name.Contains("East")) return enemyC + new Vector3(100, 0, 0);
            if (name.Contains("North")) return enemyC + new Vector3(0, 0, 100);
            if (name.Contains("South")) return enemyC + new Vector3(0, 0, -100);
        }

        Debug.LogWarning($"[EnemyGeneral] Nie znaleziono celu: {name}. Kieruję na środek armii wroga.");
        return GetEnemyArmyCenter();
    }

    TerrainFeature FindFeature(string name)
    {
        var features = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        foreach (var f in features)
        {
            if (f.featureName.ToLower().Contains(name.ToLower())) return f;
        }
        return null;
    }

    TacticalGroup FindEnemyByNameOrTag(string name)
    {
        foreach (var e in enemyGroups)
        {
            if (e.groupName.ToLower().Contains(name.ToLower())) return e;
        }
        return null;
    }

    void RefreshUnitLists()
    {
        myGroups.Clear();
        enemyGroups.Clear();
        if (armyManager == null) return;

        if (side == "SideB")
        {
            myGroups.AddRange(armyManager.factionBGroups);
            enemyGroups.AddRange(armyManager.factionAGroups);
        }
        else
        {
            myGroups.AddRange(armyManager.factionAGroups);
            enemyGroups.AddRange(armyManager.factionBGroups);
        }
        myGroups.RemoveAll(x => x == null);
        enemyGroups.RemoveAll(x => x == null);

        foreach (var g in myGroups) g.RecalculateCenter();
        foreach (var g in enemyGroups) g.RecalculateCenter();
    }

    int CountMyUnits()
    {
        int count = 0;
        foreach (var g in myGroups) count += g.unitMatrices.Count;
        return count;
    }

    float GetDistanceToNearestEnemy()
    {
        Vector3 myCenter = GetArmyCenter(myGroups);
        float minDist = float.MaxValue;
        foreach (var e in enemyGroups)
        {
            float d = Vector3.Distance(myCenter, e.groupCenter);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    Vector3 GetArmyCenter(List<TacticalGroup> groups)
    {
        if (groups.Count == 0) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        foreach (var g in groups) sum += g.groupCenter;
        return sum / groups.Count;
    }

    Vector3 GetEnemyArmyCenter() => GetArmyCenter(enemyGroups);
}