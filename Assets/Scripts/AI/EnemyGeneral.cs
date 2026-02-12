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
    private bool initialDeploymentDone = false; // Nowa flaga: czy wydano rozkazy startowe?
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

        // KROK 1: Wykonaj rozkazy Fazy 0 (np. "Zajmij Wzgórze") natychmiast po starcie sceny.
        // Dzięki temu AI rusza się, gdy gracz jeszcze planuje.
        if (!initialDeploymentDone)
        {
            RefreshUnitLists();
            startingUnitCount = CountMyUnits();

            // Uruchamiamy komendy Fazy 0, ale NIE włączamy jeszcze zegara tej fazy
            if (strategyProfile != null && strategyProfile.battlePhases.Count > 0)
            {
                currentPhaseIndex = 0; // Ustawiamy indeks na 0
                var phase0 = strategyProfile.battlePhases[0];
                currentPhaseName = phase0.phaseName;
                ExecuteCommands(phase0.commands); // AI rusza na pozycje
                Debug.Log($"<color=orange>[AI PRE-BATTLE]</color> Wykonuję manewry wstępne: {currentPhaseName}");
            }

            initialDeploymentDone = true;
        }

        // KROK 2: Jeśli trwa faza rozstawiania (gracz NIE kliknął START),
        // przerywamy Update. AI stoi na pozycjach z Fazy 0 i nie sprawdza triggerów.
        if (armyManager.isDeploymentPhase)
        {
            return;
        }

        // KROK 3: Moment kliknięcia przycisku "Start Bitwy"
        // Wykonuje się tylko RAZ, w pierwszej klatce po kliknięciu przycisku.
        if (!battleStarted)
        {
            battleStarted = true;
            battleStartTime = Time.time;

            // RESETUJEMY LICZNIK CZASU DLA FAZY 0
            // Dzięki temu te 60 sekund z Inspektora zacznie lecieć dopiero TERAZ.
            phaseStartTime = Time.time;

            Debug.Log($"Bitwa rozpoczęta! AI będzie bronić pozycji przez czas określony w Trigger Value.");
            return;
        }

        // KROK 4: Normalna logika bitewna (sprawdzanie czy przejść do Fazy 1)
        if (strategyProfile == null || currentPhaseIndex >= strategyProfile.battlePhases.Count) return;

        CheckPhaseTriggers();
    }
    public void OrderAttackOnPlayer(string attackerTag, string targetName = "Nearest")
    {
        // 1. Pobierz jednostki AI, które mają wykonać atak
        List<TacticalGroup> attackers = FilterUnits(attackerTag);

        if (attackers.Count == 0)
        {
            Debug.LogWarning($"[EnemyGeneral] Nie znaleziono jednostek AI o tagu: {attackerTag}");
            return;
        }

        foreach (var attackerGroup in attackers)
        {
            TacticalGroup finalTarget = null;

            // 2. Znajdź cel
            if (string.IsNullOrEmpty(targetName) || targetName.ToLower() == "nearest")
            {
                // Znajdź najbliższego wroga z listy enemyGroups (zdefiniowanej w EnemyGeneral)
                finalTarget = armyManager.FindClosestEnemy(attackerGroup.groupCenter, enemyGroups);
            }
            else
            {
                // Próba znalezienia konkretnego oddziału gracza
                finalTarget = FindEnemyByNameOrTag(targetName);

                // Fallback: jeśli nie znaleziono konkretnego, atakuj najbliższego
                if (finalTarget == null)
                {
                    finalTarget = armyManager.FindClosestEnemy(attackerGroup.groupCenter, enemyGroups);
                }
            }

            // 3. Wykonanie ataku
            if (finalTarget != null && finalTarget.unitMatrices.Count > 0)
            {
                // Ważne: Ustawiamy chaseTarget, aby ArmyManager wiedział, że to pościg/walka
                attackerGroup.chaseTarget = finalTarget;

                // Resetujemy przypisanie do terenu (żeby nie broniły rzeki/wzgórza podczas ataku)
                attackerGroup.assignedFeature = null;

                // Obliczamy kierunek ataku
                Vector3 directionToEnemy = (finalTarget.groupCenter - attackerGroup.groupCenter).normalized;

                // Wywołujemy formację "Line" (najskuteczniejsza do ataku) w miejscu wroga
                // ArmyHierarchyManager zajmie się ruchem i logiką walki (ResolveCombatRoundRobin)
                armyManager.ApplyFormation(attackerGroup, "Line", finalTarget.groupCenter, directionToEnemy);

                Debug.Log($"<color=red>[GENERAL AI]</color> {attackerGroup.groupName} otrzymał rozkaz ataku na: {finalTarget.groupName}");
            }
            else
            {
                // Jeśli nie ma wrogów, idź na środek mapy lub pozycję domyślną wroga
                Vector3 fallbackPos = GetEnemyArmyCenter();
                armyManager.ApplyFormation(attackerGroup, "Line", fallbackPos, (fallbackPos - attackerGroup.groupCenter).normalized);
            }
        }
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
            // =================================================================================
            // 1. NOWA LOGIKA: PRZECHWYCENIE ROZKAZU ATAKU
            // =================================================================================
            // Jeśli w Inspectorze wybrano "ChargeTarget", używamy nowej metody OrderAttackOnPlayer.
            // Pozwala to na inteligentne szukanie wrogów zamiast sztywnego ustawiania się w linii.

            if (cmd.action == AIActionType.ChargeTarget)
            {
                // Jeśli cel nie jest podany lub nazywa się ogólnie "Enemy_Army", uznajemy, że cel to "Najbliższy"
                string smartTargetName = (string.IsNullOrEmpty(cmd.targetName) || cmd.targetName == "Enemy_Army")
                                         ? "Nearest"
                                         : cmd.targetName;

                // Wywołujemy metodę, którą dodałeś wcześniej
                OrderAttackOnPlayer(cmd.unitTag, smartTargetName);

                // Przechodzimy do następnego rozkazu, pomijając starą logikę pozycjonowania dla tego ataku
                continue;
            }
            // =================================================================================


            // =================================================================================
            // 2. STARA LOGIKA: RUCH, OBRONA TERENU, FLANKI
            // =================================================================================

            List<TacticalGroup> selectedUnits = FilterUnits(cmd.unitTag);
            if (selectedUnits.Count == 0) continue;

            // Sortowanie dla zachowania spójności (żeby oddziały nie zamieniały się miejscami losowo)
            selectedUnits.Sort((a, b) => a.groupName.CompareTo(b.groupName));

            Vector3 enemyCenter = GetEnemyArmyCenter();
            Vector3 myArmyCenter = GetArmyCenter(selectedUnits);

            // A. Czyścimy nazwę celu (usuwamy sufiksy _Front, _Top itp., żeby znaleźć obiekt na scenie)
            string cleanName = cmd.targetName.Replace("_Front", "").Replace("_Back", "").Replace("_Top", "");

            // B. Szukamy obiektu terenu (Wzgórze, Las, Rzeka)
            TerrainFeature feature = FindFeature(cleanName);
            if (feature == null) feature = FindFeature("Auto_" + cleanName);

            // C. Sprawdzamy, czy mamy ustawić się wzdłuż terenu (Obrona/Ruch)
            bool alignToFeature = (feature != null && (cmd.action == AIActionType.DefendFeature || cmd.action == AIActionType.MoveTo));

            Vector3 rightDir = Vector3.zero; // W którą stronę rozstawiać kolejne jednostki (w prawo)
            Vector3 mainLookDir = Vector3.zero; // Gdzie mają patrzeć (przód)

            if (alignToFeature)
            {
                // === LOGIKA USTAWIANIA WZDŁUŻ TERENU ===
                Vector3 featureDir = feature.GetWorldDirection();
                rightDir = featureDir;

                // Wektor prostopadły (przód)
                Vector3 perpDir = Vector3.Cross(Vector3.up, rightDir).normalized;

                // Sprawdzamy, po której stronie jest wróg. Nie chcemy stać tyłem do wroga.
                Vector3 toEnemy = (enemyCenter - feature.transform.position).normalized;
                if (Vector3.Dot(perpDir, toEnemy) < 0)
                {
                    perpDir = -perpDir;
                    rightDir = -rightDir; // Odwracamy też linię rozstawienia
                }

                mainLookDir = perpDir;
            }
            else
            {
                // === LOGIKA STANDARDOWA (SZCZERE POLE) ===
                mainLookDir = (enemyCenter - myArmyCenter).normalized;
                if (mainLookDir == Vector3.zero) mainLookDir = Vector3.forward;

                // W prawo względem patrzenia
                rightDir = Vector3.Cross(Vector3.up, mainLookDir).normalized;
            }

            // D. Obliczanie pozycji dla każdego oddziału w linii
            // Spacing zależy od tego, jak szeroko mają stać (np. 65 jednostek)
            float spacing = 65.0f;

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                // Matematyka środkowania linii: (i - środek) * odstęp
                float offset = (i - (selectedUnits.Count - 1) / 2.0f) * spacing;

                // Przesunięcie względem punktu docelowego (w lewo/prawo)
                Vector3 positionOffset = rightDir * offset;

                // Wykonanie pojedynczego rozkazu ruchu
                ProcessSingleCommand(selectedUnits[i], cmd, positionOffset, mainLookDir);
            }
        }
    }

    void ProcessSingleCommand(TacticalGroup group, AICommandData cmd, Vector3 offsetVector, Vector3 forcedLookDir)
    {
        Vector3 basePos = CalculatePosition(cmd.targetName);
        Vector3 finalPos = basePos + offsetVector;

        // Jeśli nie podano wymuszonego kierunku, oblicz go standardowo
        if (forcedLookDir == Vector3.zero) forcedLookDir = (GetEnemyArmyCenter() - group.groupCenter).normalized;

        switch (cmd.action)
        {
            case AIActionType.MoveTo:
                // Używamy forcedLookDir, żeby szły równo
                armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                break;

            case AIActionType.DefendFeature:
                TerrainFeature feature = FindFeature(cmd.targetName);
                if (feature != null)
                {
                    // MoveGroupToLineFeature ma własną logikę kierunku (prostopadle do rzeki/wzgórza)
                    armyManager.MoveGroupToLineFeature(group, feature, "Top");
                }
                else
                {
                    armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                }
                break;

            case AIActionType.AttackMove:
                group.chaseTarget = null;
                // TU BYŁ BŁĄD: Wcześniej liczyliśmy kierunek do wroga dla każdej grupy osobno.
                // Teraz używamy forcedLookDir (wspólnego), więc idą ławą.
                armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                break;

            case AIActionType.ChargeTarget:
                {
                    TacticalGroup specificEnemy = FindEnemyByNameOrTag(cmd.targetName);
                    if (specificEnemy != null)
                    {
                        group.chaseTarget = specificEnemy;
                        // Przy szarży na konkretny cel, celujemy w niego (tu akurat zbieżność jest ok)
                        armyManager.ApplyFormation(group, cmd.formation, specificEnemy.groupCenter);
                    }
                    else
                    {
                        // Szarża ogólna - zachowujemy linię
                        armyManager.ApplyFormation(group, cmd.formation, finalPos, forcedLookDir);
                    }
                }
                break;

            case AIActionType.Flank:
                // Logika flanki pozostaje podobna, ale uwzględniamy offset w pozycji
                Vector3 enemyC = GetEnemyArmyCenter();
                Vector3 flankBaseOffset = Vector3.zero;

                if (cmd.targetName.Contains("West")) flankBaseOffset = new Vector3(-100, 0, 0);
                else if (cmd.targetName.Contains("East")) flankBaseOffset = new Vector3(100, 0, 0);
                else if (cmd.targetName.Contains("North")) flankBaseOffset = new Vector3(0, 0, 100);
                else if (cmd.targetName.Contains("South")) flankBaseOffset = new Vector3(0, 0, -100);

                Vector3 flankPos = enemyC + flankBaseOffset + offsetVector;

                // Flanka zawsze patrzy w stronę środka wroga
                Vector3 flankDir = (enemyC - flankPos).normalized;

                armyManager.ApplyFormation(group, cmd.formation, flankPos, flankDir);
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

        // Sprawdzamy każdy nasz oddział...
        foreach (var myGroup in myGroups)
        {
            if (myGroup.unitMatrices.Count == 0) continue; // Pomiń martwe oddziały

            // ...z każdym oddziałem wroga
            foreach (var enemyGroup in enemyGroups)
            {
                if (enemyGroup.unitMatrices.Count == 0) continue;

                // Liczymy dystans między środkami konkretnych grup
                float d = Vector3.Distance(myGroup.groupCenter, enemyGroup.groupCenter);

                // Odejmujemy promienie grup, żeby liczyć dystans "od krawędzi do krawędzi", a nie "od środka do środka"
                // Dzięki temu trigger zadziała, gdy jednostki faktycznie są blisko siebie
                float actualDistance = d - myGroup.groupRadius - enemyGroup.groupRadius;

                // Zabezpieczenie, żeby nie wyszło ujemne (gdy grupy na siebie nachodzą)
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