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
    private bool initialDeploymentDone = false;
    private List<TacticalGroup> myGroups = new List<TacticalGroup>();
    private List<TacticalGroup> enemyGroups = new List<TacticalGroup>();

    private int startingUnitCount = 0;
    public enum GeneralPersonality { Aggressive, Defensive, Balanced, Tactician }
    public enum DynamicState { Idle, Advancing, Skirmishing, AllOutAttack, FallingBack }

    [Header("Ustawienia Trybu")]
    public bool forceDynamicMode = false;
    public bool isDynamicBattle = false;

    [Header("Dynamiczne AI (Custom Maps)")]
    public GeneralPersonality personality;
    public float decisionInterval = 5.0f;
    private float lastDecisionTime;
    private DynamicState dynamicState = DynamicState.Idle;

    private List<TacticalGroup> myCavalry = new List<TacticalGroup>();
    private List<TacticalGroup> myInfantry = new List<TacticalGroup>();
    void Start()
    {
        if (armyManager == null) armyManager = FindAnyObjectByType<ArmyHierarchyManager>();

        // 1. Ustalanie stron
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

        // 2. DECYZJA O TRYBIE
        if (strategyProfile == null || forceDynamicMode)
        {
            isDynamicBattle = true;
            strategyProfile = null;

            personality = (GeneralPersonality)Random.Range(0, 4);
            Debug.Log($"<color=cyan>[EnemyGeneral] TRYB DYNAMICZNY (Gotowy). Osobowość: {personality}</color>");
        }
        else
        {
            isDynamicBattle = false;
            Debug.Log($"<color=green>[EnemyGeneral] TRYB HISTORYCZNY: {strategyProfile.name}</color>");
        }
    }

    void Update()
    {
        if (armyManager == null || !armyManager.groupsInitialized) return;

        if (!initialDeploymentDone)
        {
            RefreshUnitLists();
            CategorizeUnits();
            startingUnitCount = CountMyUnits();

            // Scenariusz (Historyczny) 
            if (!isDynamicBattle && strategyProfile != null && strategyProfile.battlePhases.Count > 0)
            {
                currentPhaseIndex = 0;
                var phase0 = strategyProfile.battlePhases[0];
                currentPhaseName = phase0.phaseName;
                ExecuteCommands(phase0.commands);
            }
            // Dynamiczny (Custom)
            else if (isDynamicBattle)
            {
                PerformDynamicDeployment();
            }

            initialDeploymentDone = true;
        }

        if (armyManager.isDeploymentPhase)
        {
            return;
        }

        if (!battleStarted)
        {
            battleStarted = true;
            battleStartTime = Time.time;
            phaseStartTime = Time.time;

            if (isDynamicBattle) lastDecisionTime = -decisionInterval;

            Debug.Log($"<color=orange>BITWA ROZPOCZĘTA! Czas: {battleStartTime}</color>");
            return;
        }

        RefreshUnitLists();

        if (isDynamicBattle)
        {
            UpdateDynamicAI();
        }
        else
        {
            if (strategyProfile == null || currentPhaseIndex >= strategyProfile.battlePhases.Count) return;
            CheckPhaseTriggers();
        }
    }
    public void OrderAttackOnPlayer(string attackerTag, string targetName = "Nearest")
    {
        List<TacticalGroup> attackers = FilterUnits(attackerTag);

        if (attackers.Count == 0)
        {
            Debug.LogWarning($"[EnemyGeneral] Nie znaleziono jednostek AI o tagu: {attackerTag}");
            return;
        }

        foreach (var attackerGroup in attackers)
        {
            TacticalGroup finalTarget = null;

            if (string.IsNullOrEmpty(targetName) || targetName.ToLower() == "nearest")
            {
                finalTarget = armyManager.FindClosestEnemy(attackerGroup.groupCenter, enemyGroups);
            }
            else
            {
                finalTarget = FindEnemyByNameOrTag(targetName);

                if (finalTarget == null)
                {
                    finalTarget = armyManager.FindClosestEnemy(attackerGroup.groupCenter, enemyGroups);
                }
            }

            if (finalTarget != null && finalTarget.unitMatrices.Count > 0)
            {
                attackerGroup.chaseTarget = finalTarget;
                attackerGroup.assignedFeature = null;
                Vector3 directionToEnemy = (finalTarget.groupCenter - attackerGroup.groupCenter).normalized;
                armyManager.ApplyFormation(attackerGroup, "Line", finalTarget.groupCenter, directionToEnemy);

                Debug.Log($"<color=red>[GENERAL AI]</color> {attackerGroup.groupName} otrzymał rozkaz ataku na: {finalTarget.groupName}");
            }
            else
            {
                Vector3 fallbackPos = GetEnemyArmyCenter();
                armyManager.ApplyFormation(attackerGroup, "Line", fallbackPos, (fallbackPos - attackerGroup.groupCenter).normalized);
            }
        }
    }
    Vector3 GetTacticalPosition(Vector3 basePoint, Vector3 direction, float distance)
    {
        Vector3 targetPos = basePoint + (direction.normalized * distance);
        UnityEngine.AI.NavMeshHit hit;

        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out hit, 30.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }

        Vector3 closerPos = Vector3.Lerp(basePoint, targetPos, 0.5f);
        if (UnityEngine.AI.NavMesh.SamplePosition(closerPos, out hit, 30.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }

        return basePoint;
    }
    void UpdateDynamicAI()
    {
        if (Time.time - lastDecisionTime < decisionInterval) return;
        lastDecisionTime = Time.time;

        float myStrength = CountMyUnits();
        float enemyStrength = GetEnemyUnitCount();
        float ratio = (enemyStrength > 0) ? myStrength / enemyStrength : 2.0f;
        float distToEnemy = GetDistanceToNearestEnemy();

        DynamicState nextState = dynamicState;

        if (ratio < 0.4f)
        {
            // Panika lub desperacki atak
            nextState = (personality == GeneralPersonality.Aggressive) ? DynamicState.AllOutAttack : DynamicState.FallingBack;
        }
        else if (distToEnemy < 80.0f)
        {
            // Wróg jest bardzo blisko -> Atakuj!
            nextState = DynamicState.AllOutAttack;
        }
        else
        {
            switch (personality)
            {
                case GeneralPersonality.Aggressive:
                    nextState = DynamicState.AllOutAttack;
                    break;

                case GeneralPersonality.Defensive:
                    if (distToEnemy > 150f) nextState = DynamicState.Advancing;
                    else if (ratio > 1.3f) nextState = DynamicState.Advancing;
                    else nextState = DynamicState.Idle;
                    break;

                case GeneralPersonality.Tactician:
                    if (distToEnemy > 200f) nextState = DynamicState.Advancing;
                    else if (distToEnemy > 80f) nextState = DynamicState.Skirmishing;
                    else nextState = DynamicState.AllOutAttack;
                    break;

                case GeneralPersonality.Balanced:
                default:
                    if (ratio > 0.9f) nextState = DynamicState.Advancing;
                    else nextState = DynamicState.Idle;
                    break;
            }
        }

        ExecuteDynamicTactics(nextState, distToEnemy);
        dynamicState = nextState;

        Debug.Log($"[AI] State: {dynamicState} | Dist: {distToEnemy:F1} | Ratio: {ratio:F2}");
    }

    void ExecuteDynamicTactics(DynamicState state, float distance)
    {
        Vector3 enemyCenter = GetEnemyArmyCenter();
        Vector3 myArmyCenter = GetArmyCenter(myGroups);

        switch (state)
        {
            case DynamicState.Idle:
                foreach (var g in myGroups)
                    armyManager.ApplyFormation(g, "Line", g.groupCenter, (enemyCenter - g.groupCenter).normalized);
                break;

            case DynamicState.Advancing:
                Vector3 advancePoint = GetTacticalPosition(enemyCenter, (myArmyCenter - enemyCenter), 40.0f);
                MoveArmyIdeally(advancePoint, 0);
                break;

            case DynamicState.AllOutAttack:
                OrderAttackOnPlayer("All", "Nearest");
                break;

            case DynamicState.Skirmishing:
                if (myInfantry.Count > 0)
                {
                    Vector3 infantryPoint = GetTacticalPosition(enemyCenter, (GetArmyCenter(myInfantry) - enemyCenter), 50.0f);

                    foreach (var inf in myInfantry)
                    {
                        armyManager.ApplyFormation(inf, "Line", infantryPoint, (enemyCenter - infantryPoint).normalized);
                    }
                }

                if (myCavalry.Count > 0)
                {
                    for (int i = 0; i < myCavalry.Count; i++)
                    {
                        bool goRight = (i % 2 == 0);

                        Vector3 forward = (enemyCenter - myArmyCenter).normalized;
                        Vector3 right = Vector3.Cross(Vector3.up, forward);
                        Vector3 flankDir = goRight ? right : -right;
                        Vector3 finalDir = (flankDir * 1.5f + forward * 0.5f).normalized;
                        Vector3 flankPoint = GetTacticalPosition(enemyCenter, finalDir, 80.0f);

                        armyManager.ApplyFormation(myCavalry[i], "Wedge", flankPoint, (enemyCenter - flankPoint).normalized);
                    }
                }
                break;

            case DynamicState.FallingBack:
                Vector3 retreatDir = (myArmyCenter - enemyCenter).normalized;
                Vector3 retreatPoint = GetTacticalPosition(myArmyCenter, retreatDir, 60.0f);

                MoveArmyIdeally(retreatPoint, 0);
                break;
        }
    }

    void MoveArmyIdeally(Vector3 targetCenter, float stopDistance)
    {
        Vector3 armyCenter = GetArmyCenter(myGroups);
        Vector3 dirToTarget = (targetCenter - armyCenter).normalized;

        if (dirToTarget == Vector3.zero) dirToTarget = Vector3.forward;

        Vector3 targetPos = targetCenter - (dirToTarget * stopDistance);
        Vector3 rightDir = Vector3.Cross(Vector3.up, dirToTarget).normalized;

        var sortedGroups = myGroups.OrderBy(g => Vector3.Dot(g.groupCenter - armyCenter, rightDir)).ToList();
        float spacing = 70.0f;

        for (int i = 0; i < sortedGroups.Count; i++)
        {
            float offset = (i - (sortedGroups.Count - 1) / 2.0f) * spacing;
            Vector3 desiredPos = targetPos + (rightDir * offset);
            UnityEngine.AI.NavMeshHit hit;

            if (UnityEngine.AI.NavMesh.SamplePosition(desiredPos, out hit, 20.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                armyManager.ApplyFormation(sortedGroups[i], "Line", hit.position, dirToTarget);
            }
            else
            {
                armyManager.ApplyFormation(sortedGroups[i], "Line", desiredPos, dirToTarget);
            }
        }
    }

    void CategorizeUnits()
    {
        myCavalry.Clear(); myInfantry.Clear();
        foreach (var g in myGroups)
        {
            if (g.groupName.ToLower().Contains("cavalry") || g.groupName.ToLower().Contains("horse")) myCavalry.Add(g);
            else myInfantry.Add(g);
        }
    }

    void PerformDynamicDeployment() => MoveArmyIdeally(GetEnemyArmyCenter(), 300.0f);

    int GetEnemyUnitCount() { int c = 0; foreach (var g in enemyGroups) c += g.unitMatrices.Count; return c; }
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
            if (cmd.action == AIActionType.ChargeTarget)
            {
                string smartTargetName = (string.IsNullOrEmpty(cmd.targetName) || cmd.targetName == "Enemy_Army")
                                         ? "Nearest"
                                         : cmd.targetName;

                OrderAttackOnPlayer(cmd.unitTag, smartTargetName);
                continue;
            }
      
            List<TacticalGroup> selectedUnits = FilterUnits(cmd.unitTag);
            if (selectedUnits.Count == 0) continue;

            selectedUnits.Sort((a, b) => a.groupName.CompareTo(b.groupName));
            Vector3 enemyCenter = GetEnemyArmyCenter();
            Vector3 myArmyCenter = GetArmyCenter(selectedUnits);
            string cleanName = cmd.targetName.Replace("_Front", "").Replace("_Back", "").Replace("_Top", "");

            TerrainFeature feature = FindFeature(cleanName);
            if (feature == null) feature = FindFeature("Auto_" + cleanName);

            bool alignToFeature = (feature != null && (cmd.action == AIActionType.DefendFeature || cmd.action == AIActionType.MoveTo));

            Vector3 rightDir = Vector3.zero;
            Vector3 mainLookDir = Vector3.zero;

            if (alignToFeature)
            {
                Vector3 featureDir = feature.GetWorldDirection();
                rightDir = featureDir;
                Vector3 perpDir = Vector3.Cross(Vector3.up, rightDir).normalized;
                Vector3 toEnemy = (enemyCenter - feature.transform.position).normalized;

                if (Vector3.Dot(perpDir, toEnemy) < 0)
                {
                    perpDir = -perpDir;
                    rightDir = -rightDir;
                }

                mainLookDir = perpDir;
            }
            else
            {
                mainLookDir = (enemyCenter - myArmyCenter).normalized;
                if (mainLookDir == Vector3.zero) mainLookDir = Vector3.forward;
                rightDir = Vector3.Cross(Vector3.up, mainLookDir).normalized;
            }

            float spacing = 65.0f;

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                float offset = (i - (selectedUnits.Count - 1) / 2.0f) * spacing;
                Vector3 positionOffset = rightDir * offset;
                ProcessSingleCommand(selectedUnits[i], cmd, positionOffset, mainLookDir);
            }
        }
    }

    void ProcessSingleCommand(TacticalGroup group, AICommandData cmd, Vector3 offsetVector, Vector3 forcedLookDir)
    {
        Vector3 basePos = CalculatePosition(cmd.targetName);
        Vector3 finalPos = basePos + offsetVector;

        if (forcedLookDir == Vector3.zero) forcedLookDir = (GetEnemyArmyCenter() - group.groupCenter).normalized;

        switch (cmd.action)
        {
            case AIActionType.MoveTo:
                armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                break;

            case AIActionType.DefendFeature:
                TerrainFeature feature = FindFeature(cmd.targetName);
                if (feature != null)
                {
                    armyManager.MoveGroupToLineFeature(group, feature, "Top");
                }
                else
                {
                    armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                }
                break;

            case AIActionType.AttackMove:
                group.chaseTarget = null;
                armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                break;

            case AIActionType.ChargeTarget:
                {
                    TacticalGroup specificEnemy = FindEnemyByNameOrTag(cmd.targetName);
                    if (specificEnemy != null)
                    {
                        group.chaseTarget = specificEnemy;
                        armyManager.ApplyFormation(group, cmd.formation, specificEnemy.groupCenter);
                    }
                    else
                    {
                        armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                    }
                }
                break;

            case AIActionType.Flank:
                Vector3 enemyC = GetEnemyArmyCenter();
                Vector3 flankBaseOffset = Vector3.zero;

                if (cmd.targetName.Contains("West")) flankBaseOffset = new Vector3(-100, 0, 0);
                else if (cmd.targetName.Contains("East")) flankBaseOffset = new Vector3(100, 0, 0);
                else if (cmd.targetName.Contains("North")) flankBaseOffset = new Vector3(0, 0, 100);
                else if (cmd.targetName.Contains("South")) flankBaseOffset = new Vector3(0, 0, -100);

                Vector3 flankPos = enemyC + flankBaseOffset + offsetVector;
                Vector3 flankDir = (enemyC - flankPos).normalized;

                armyManager.ApplyFormation(group, cmd.formation, flankPos, flankDir);
                break;
            case AIActionType.HoldPosition:
                group.chaseTarget = null;
                group.assignedFeature = null;
                Vector3 holdPos = (string.IsNullOrEmpty(cmd.targetName) || cmd.targetName.ToLower() == "current" || cmd.targetName.ToLower() == "none")
                                    ? group.groupCenter
                                    : finalPos;
                armyManager.ApplyFormation(group, cmd.formation, holdPos, forcedLookDir);

                Debug.Log($"<color=yellow>[GENERAL AI]</color> {group.groupName} otrzymał rozkaz utrzymania pozycji (HoldPosition)!");
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
        GameObject directObj = GameObject.Find(name);
        if (directObj != null) return directObj.transform.position;

        string baseName = name;
        string suffix = "";

        if (name.Contains("_Front")) { baseName = name.Replace("_Front", ""); suffix = "Front"; }
        else if (name.Contains("_Back")) { baseName = name.Replace("_Back", ""); suffix = "Back"; }
        else if (name.Contains("_Top")) { baseName = name.Replace("_Top", ""); suffix = "Top"; }

        GameObject featureObj = GameObject.Find(baseName);
        if (featureObj == null) featureObj = GameObject.Find("Auto_" + baseName);

        if (featureObj != null)
        {
            TerrainFeature feature = featureObj.GetComponent<TerrainFeature>();
            if (feature != null)
            {
                Vector3 rightDir = feature.GetWorldDirection();
                Vector3 forwardDir = Vector3.Cross(Vector3.up, rightDir).normalized;
                float offsetDist = feature.widthOffset;

                switch (suffix)
                {
                    case "Front": return featureObj.transform.position - (forwardDir * offsetDist);
                    case "Back": return featureObj.transform.position + (forwardDir * offsetDist);
                    case "Top": default: return featureObj.transform.position;
                }
            }
            return featureObj.transform.position;
        }

        if (name == "Center") return new Vector3((armyManager.minX + armyManager.maxX) / 2, 0, (armyManager.minZ + armyManager.maxZ) / 2);
        if (name == "Enemy_Army") return GetEnemyArmyCenter();

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
        float globalMinDist = float.MaxValue;

        foreach (var myGroup in myGroups)
        {
            if (myGroup.unitMatrices.Count == 0) continue;
            foreach (var enemyGroup in enemyGroups)
            {
                if (enemyGroup.unitMatrices.Count == 0) continue;

                float d = Vector3.Distance(myGroup.groupCenter, enemyGroup.groupCenter);
                float actualDistance = d - myGroup.groupRadius - enemyGroup.groupRadius;

                if (actualDistance < 0) actualDistance = 0;

                if (actualDistance < globalMinDist)
                {
                    globalMinDist = actualDistance;
                }
            }
        }
        return globalMinDist;
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