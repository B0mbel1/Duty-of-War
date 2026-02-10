using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class ArmyHierarchyManager : MonoBehaviour
{
    [Header("References (Auto-detected)")]
    public UnitsSpawnCustom customSpawner;
    public UnitsSpawn historySpawner;

    [Header("UI Integration")]
    public TextMeshProUGUI counterTextA;
    public TextMeshProUGUI counterTextB;
    public GameObject groupUIPrefab;
    public Transform uiContainer;

    [Header("Merging UI")]
    public GameObject namingPanel;
    public TMP_InputField nameInput;
    public UnityEngine.UI.Button confirmNameButton;
    public TextMeshProUGUI warningText;

    [Header("End Game Settings")]
    public GameObject endGamePanel;
    public TextMeshProUGUI resultText;
    public string menuSceneName = "MainMenu";
    private bool isBattleOver = false;

    [Header("Battle Phase Settings")]
    public bool isDeploymentPhase = true;
    public GameObject startBattleButton;

    [Header("Tactical Pause Settings")]
    public GameObject tacticalPauseIndicator;
    private bool isTacticalPause = false;

    [Header("Visuals")]
    public Material highlightMaterial;
    public Material enemyHighlightMaterial;

    public Material directionArrowMaterial;
    public Mesh indicatorMeshInfantry;
    public Mesh indicatorMeshArcher;
    public Mesh indicatorMeshCavalry;

    private Mesh cachedInfantryMesh;
    private Mesh cachedArcherMesh;
    private Mesh cachedCavalryMesh;

    [Header("Hierarchy & Speed")]
    public HierarchyRank groupSize = HierarchyRank.Company;
    public float moveSpeed;
    public float rotationSpeed;
    public float stopDistance;
    public float cavalrySpeedMultiplier;

    [Header("Water Settings")]
    public LayerMask waterLayerMask;
    public float waterSpeedPenalty = 0.5f;
    public float swimmingDepth = 0.5f;

    [Header("Forest Settings")]
    public string forestLayerName = "Forest";
    public float forestCavalryPenalty = 0.7f;
    public float forestInfantryPenalty = 0.9f;
    private int forestLayerIndex = -1;

    [Header("Slope Physics")]
    public float slopeCheckDistance = 2.0f;
    public float lightSlopeAngle = 10f;
    public float steepSlopeAngle = 30f;
    public float uphillLightMult = 0.8f;
    public float uphillSteepMult = 0.4f;
    public float downhillLightMult = 1.1f;
    public float downhillSteepMult = 0.9f;

    [Header("Combat Settings")]
    public float attackRangeMelee = 3.5f;
    public float attackRangeArcher = 80f;
    public float heightRangeBonus = 0.5f;
    public float maxHeightDamageBonus = 1.5f;
    public float attackCooldown = 1.0f;
    public float archerAttackCooldown = 2.0f;

    [Header("Map Boundaries")]
    public float minX = 0f; public float maxX = 2000f;
    public float minZ = 0f; public float maxZ = 2000f;

    [Header("Performance")]
    public int maxCombatChecksPerFrame = 2000;

    [Header("Base Stats")]
    public float hpMax = 2.0f;
    public float dmgInfantry = 1.0f;
    public float dmgArcherMin = 0.5f;
    public float dmgArcherMax = 1.0f;
    public float dmgCavalryCharge = 2.0f;
    public float dmgCavalryStatic = 0.8f;

    [Header("Cursor Settings")]
    public GameObject cursorMarkerPrefab; // Przypisz tu prefab (np. cylinder lub flagę)
    private GameObject activeCursorMarker;
    [HideInInspector] public Vector3 lastCursorPosition;
    [HideInInspector] public bool hasCursorPosition = false;
    public LayerMask groundLayerMask = ~0;
    // --- DANE ---
    public List<TacticalGroup> factionAGroups = new List<TacticalGroup>();
    public List<TacticalGroup> factionBGroups = new List<TacticalGroup>();

    private List<ArmyGroupUI> uiElements = new List<ArmyGroupUI>();
    private List<TacticalGroup> selectedGroups = new List<TacticalGroup>();
    private TacticalGroup lastSelectedGroup = null;

    public bool groupsInitialized = false;
    private Terrain terrain;
    private TerrainData terrainData;

    // --- SPATIAL GRID ---
    private Dictionary<int, List<UnitRef>> spatialGrid = new Dictionary<int, List<UnitRef>>();
    private float cellSize = 10.0f;

    // --- FAIR SCHEDULER ---
    private int nextGroupIdxA = 0;
    private int nextUnitIdxA = 0;
    private int nextGroupIdxB = 0;
    private int nextUnitIdxB = 0;


    public TacticalGroup selectedEnemyGroup = null;

    struct UnitRef
    {
        public TacticalGroup group;
        public int index;
        public Vector3 position;
    }

    public LayerMask terrainLayerMask = 1;

    void Start()
    {
        terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            terrainData = terrain.terrainData;
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrainData.size;
            minX = tPos.x; minZ = tPos.z;
            maxX = tPos.x + tSize.x; maxZ = tPos.z + tSize.z;
            FindForestLayerIndex();
        }

        PrepareIndicatorMeshes();
        Invoke(nameof(InitializeGroups), 0.5f);


        if (namingPanel != null) namingPanel.SetActive(false);
        if (confirmNameButton != null) confirmNameButton.onClick.AddListener(OnMergeConfirmed);
    }

    TacticalGroup FindGroupUnderCursor(Ray ray, List<TacticalGroup> potentialGroups)
    {
        TacticalGroup bestCandidate = null;
        float closestDistanceToCamera = float.MaxValue;

        foreach (var group in potentialGroups)
        {
            if (group.unitMatrices.Count == 0) continue;

            // --- FAZA 1: SZYBKI TEST (BROAD PHASE) ---
            // Najpierw sprawdzamy, czy w ogóle celujemy w okolice grupy.
            // Jeśli promień mija całą grupę o 5 metrów, nie ma sensu sprawdzać każdego żołnierza.

            Vector3 toCenter = group.groupCenter - ray.origin;
            float projection = Vector3.Dot(toCenter, ray.direction);
            if (projection < 0) continue; // Grupa za kamerą

            Vector3 pointOnRay = ray.origin + (ray.direction * projection);
            float distFromRayToCenter = Vector3.Distance(pointOnRay, group.groupCenter);

            // Jeśli kursor jest bardzo daleko od środka grupy -> pomiń
            if (distFromRayToCenter > group.groupRadius + 5.0f) continue;


            // --- FAZA 2: PRECYZYJNY TEST (NARROW PHASE) ---
            // Sprawdzamy kolizję z każdym pojedynczym żołnierzem w tej grupie.

            // Ustalamy wielkość hitboxa zależnie od typu jednostki
            float unitHitRadius = 1.2f; // Domyślnie (piechota)
            if (group.archetypes.Count > 0)
            {
                UnitType type = group.archetypes[0].type;
                if (type == UnitType.Cavalry) unitHitRadius = 2.5f; // Konie są większe i łatwiej w nie trafić
                else if (type == UnitType.Archer) unitHitRadius = 1.2f;
            }

            for (int i = 0; i < group.unitMatrices.Count; i++)
            {
                // Pobieramy pozycję konkretnego żołnierza
                Vector3 unitPos = (Vector3)group.unitMatrices[i].GetColumn(3);

                // Matematyka: Dystans punktu (żołnierza) od prostej (promienia myszki)
                Vector3 toUnit = unitPos - ray.origin;
                float t = Vector3.Dot(toUnit, ray.direction);

                // Jeśli żołnierz jest za kamerą, pomiń
                if (t < 0) continue;

                Vector3 closestPointOnRay = ray.origin + (ray.direction * t);
                float distToRay = Vector3.Distance(closestPointOnRay, unitPos);

                // CZY TRAFILIŚMY W TEGO ŻOŁNIERZA?
                if (distToRay <= unitHitRadius)
                {
                    // Tak! Teraz sprawdzamy, czy ten żołnierz jest bliżej kamery niż poprzedni kandydaci.
                    // 't' to odległość od kamery wzdłuż promienia.
                    if (t < closestDistanceToCamera)
                    {
                        closestDistanceToCamera = t;
                        bestCandidate = group;
                    }
                }
            }
        }
        return bestCandidate;
    }

    void FindForestLayerIndex()
    {
        if (terrainData == null) return;
        for (int i = 0; i < terrainData.terrainLayers.Length; i++)
        {
            if (terrainData.terrainLayers[i].name == forestLayerName)
            {
                forestLayerIndex = i;
                return;
            }
        }
    }
    public void SetCursorPosition(Vector3 pos)
    {
        lastCursorPosition = pos;
        hasCursorPosition = true;

        if (activeCursorMarker == null && cursorMarkerPrefab != null)
        {
            activeCursorMarker = Instantiate(cursorMarkerPrefab);
        }

        if (activeCursorMarker != null)
        {
            activeCursorMarker.transform.position = pos;
            activeCursorMarker.SetActive(true);
        }

        Debug.Log($"Ustawiono kursor w: {pos}");
    }
    void PrepareIndicatorMeshes()
    {
        cachedInfantryMesh = CreateOffsetMesh(indicatorMeshInfantry, new Vector3(0, 0.1f, 0.5f), new Vector3(90, 0, 0), new Vector3(0.2f, 1.0f, 0.2f));
        cachedArcherMesh = CreateOffsetMesh(indicatorMeshArcher, new Vector3(0, 0.1f, 0.5f), new Vector3(-90, 90, 0), new Vector3(1.0f, 0.02f, 1.0f));
        cachedCavalryMesh = CreateOffsetMesh(indicatorMeshCavalry, new Vector3(0, 0.1f, 0.5f), new Vector3(90, 0, 0), new Vector3(0.2f, 1.0f, 0.2f));
    }

    Mesh CreateOffsetMesh(Mesh sourceMesh, Vector3 positionOffset, Vector3 rotationOffset, Vector3 scale)
    {
        if (sourceMesh == null) return null;
        Mesh newMesh = new Mesh();
        Vector3[] originalVertices = sourceMesh.vertices;
        Vector3[] newVertices = new Vector3[originalVertices.Length];
        Matrix4x4 trs = Matrix4x4.TRS(positionOffset, Quaternion.Euler(rotationOffset), scale);
        for (int i = 0; i < originalVertices.Length; i++) newVertices[i] = trs.MultiplyPoint3x4(originalVertices[i]);
        newMesh.vertices = newVertices;
        newMesh.triangles = sourceMesh.triangles;
        newMesh.uv = sourceMesh.uv;
        newMesh.RecalculateNormals();
        return newMesh;
    }

    void InitializeGroups()
    {
        if (customSpawner == null) customSpawner = GetComponent<UnitsSpawnCustom>();
        if (historySpawner == null) historySpawner = GetComponent<UnitsSpawn>();
        bool useCustom = (customSpawner != null && customSpawner.enabled);
        if (!useCustom && (historySpawner == null || !historySpawner.enabled)) return;

        if (useCustom) { if (counterTextA == null) counterTextA = customSpawner.factionAText; if (counterTextB == null) counterTextB = customSpawner.factionBText; }
        else { if (counterTextA == null) counterTextA = historySpawner.factionAText; if (counterTextB == null) counterTextB = historySpawner.factionBText; }

        if (uiContainer != null) { for (int i = uiContainer.childCount - 1; i >= 0; i--) Destroy(uiContainer.GetChild(i).gameObject); }
        uiElements.Clear(); factionAGroups.Clear(); factionBGroups.Clear();

        bool isPlayerA = useCustom ? true : (PlayerPrefs.GetString("PlayerSide", "SideA") == "SideA");
        bool isPlayerB = !isPlayerA;

        List<Matrix4x4> a_Inf, a_Arc, a_Cav, b_Inf, b_Arc, b_Cav;
        Mesh mInf, mArc, mCav;
        Material matA, matB;

        if (useCustom)
        {
            a_Inf = customSpawner.listA_Inf; a_Arc = customSpawner.listA_Arc; a_Cav = customSpawner.listA_Cav;
            b_Inf = customSpawner.listB_Inf; b_Arc = customSpawner.listB_Arc; b_Cav = customSpawner.listB_Cav;
            mInf = customSpawner.infantryMesh; mArc = customSpawner.archerMesh; mCav = customSpawner.cavalryMesh;
            matA = customSpawner.factionAMaterial; matB = customSpawner.factionBMaterial;
        }
        else
        {
            a_Inf = historySpawner.listA_Inf; a_Arc = historySpawner.listA_Arc; a_Cav = historySpawner.listA_Cav;
            b_Inf = historySpawner.listB_Inf; b_Arc = historySpawner.listB_Arc; b_Cav = historySpawner.listB_Cav;
            mInf = historySpawner.infantryMesh; mArc = historySpawner.archerMesh; mCav = historySpawner.cavalryMesh;
            matA = historySpawner.factionAMaterial; matB = historySpawner.factionBMaterial;
        }

        CreateGroupsFromList(a_Inf, mInf, matA, "A_Infantry", factionAGroups, isPlayerA, UnitType.Infantry);
        CreateGroupsFromList(a_Arc, mArc, matA, "A_Archers", factionAGroups, isPlayerA, UnitType.Archer);
        CreateGroupsFromList(a_Cav, mCav, matA, "A_Cavalry", factionAGroups, isPlayerA, UnitType.Cavalry);

        CreateGroupsFromList(b_Inf, mInf, matB, "B_Infantry", factionBGroups, isPlayerB, UnitType.Infantry);
        CreateGroupsFromList(b_Arc, mArc, matB, "B_Archers", factionBGroups, isPlayerB, UnitType.Archer);
        CreateGroupsFromList(b_Cav, mCav, matB, "B_Cavalry", factionBGroups, isPlayerB, UnitType.Cavalry);

        if (useCustom) customSpawner.RebuildAllBatches(); else historySpawner.RebuildAllBatches();
        groupsInitialized = true;
        UpdateGlobalCounters();

        if (customSpawner != null) customSpawner.enabled = false;
        if (historySpawner != null) historySpawner.enabled = false;
    }

    void CreateGroupsFromList(List<Matrix4x4> sourceList, Mesh mesh, Material mat, string baseName, List<TacticalGroup> targetCollection, bool createUI, UnitType type)
    {
        int sizePerGroup = (int)groupSize;
        int totalUnits = sourceList.Count;
        int groupIndex = 1;

        for (int i = 0; i < totalUnits; i += sizePerGroup)
        {
            int count = Mathf.Min(sizePerGroup, totalUnits - i);
            TacticalGroup newGroup = new TacticalGroup($"{baseName}_{groupIndex}", mesh, mat, groupSize, type);
            var arch = newGroup.archetypes[0];

            switch (type)
            {
                case UnitType.Archer:
                    arch.arrowMesh = cachedArcherMesh ?? cachedInfantryMesh;
                    arch.speedMultiplier = 1.0f;
                    break;
                case UnitType.Cavalry:
                    arch.arrowMesh = cachedCavalryMesh ?? cachedInfantryMesh;
                    arch.speedMultiplier = cavalrySpeedMultiplier;
                    break;
                default:
                    arch.arrowMesh = cachedInfantryMesh;
                    arch.speedMultiplier = 1.0f;
                    break;
            }
            arch.arrowMaterial = directionArrowMaterial;

            newGroup.RecalculateStats();

            List<Matrix4x4> slice = sourceList.GetRange(i, count);
            foreach (var matrix in slice)
            {
                newGroup.AddUnit(matrix, hpMax, 0);
                newGroup.attackCooldowns[newGroup.attackCooldowns.Count - 1] = Random.Range(0f, 1.0f);
            }

            if (newGroup.unitMatrices.Count > 0) newGroup.RebuildBatchesFull();

            targetCollection.Add(newGroup);

            if (createUI && uiContainer != null)
            {
                GameObject newUI = Instantiate(groupUIPrefab, uiContainer);
                newUI.GetComponent<ArmyGroupUI>().Setup(newGroup, this);
                uiElements.Add(newUI.GetComponent<ArmyGroupUI>());
            }
            groupIndex++;
        }
    }

    void Update()
    {
        if (!groupsInitialized) return;

        // --- ZABEZPIECZENIE PRZED PISANIEM ---
        bool isTyping = false;
        if (EventSystem.current != null)
        {
            GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
            if (selectedObj != null && selectedObj.GetComponent<TMP_InputField>() != null)
                isTyping = true;
        }

        // --- SKRÓTY KLAWISZOWE ---
        if (!isTyping)
        {
            if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
            if (Input.GetKeyDown(KeyCode.B)) KillRandomUnit(false);
            if (Input.GetKeyDown(KeyCode.Space)) ToggleTacticalPause();
            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                if (namingPanel != null && namingPanel.activeSelf) namingPanel.SetActive(false);
                else if (selectedGroups.Count > 1) OpenNamingPanel();
            }
            // SKRÓT ENTER DO ROZPOCZĘCIA BITWY (Opcjonalnie)
            if (Input.GetKeyDown(KeyCode.Return) && isDeploymentPhase) StartBattle();
        }

        foreach (var g in factionAGroups) UpdateChaseLogic(g);
        foreach (var g in factionBGroups) UpdateChaseLogic(g);

        // --- LOGIKA FAZ GRY (TOTAL WAR STYLE) ---

        // 1. Czy Gracz może ruszać jednostki? 
        // TAK, jeśli nie ma PAUZY TAKTYCZNEJ (w Deployment Phase też może!)
        bool canMovePlayer = !isTacticalPause;

        // 2. Czy Wróg może ruszać jednostki?
        // TYLKO jeśli bitwa się zaczęła i nie ma pauzy
        bool canMoveEnemy = !isTacticalPause;
        // 3. Czy walka jest aktywna?
        // TYLKO jeśli bitwa się zaczęła i nie ma pauzy
        bool isCombatActive = !isDeploymentPhase && !isTacticalPause;


        // --- AKTUALIZACJA JEDNOSTEK GRACZA (Faction A) ---
        foreach (var group in factionAGroups)
        {
            if (group.unitMatrices.Count > 0)
            {
                // Jeśli nie ma pauzy (isTacticalPause), przetwarzamy ruch.
                // (W deployment phase wejdzie do ProcessMovement i od razu zrobi teleport).
                if (!isTacticalPause && group.isMoving)
                {
                    ProcessMovement(group);
                }

                group.RenderGroup();
                group.RenderIndicators();
            }
        }

        // --- AKTUALIZACJA JEDNOSTEK WROGA (Faction B) ---
        foreach (var group in factionBGroups)
        {
            if (group.unitMatrices.Count > 0)
            {
                if (canMoveEnemy && group.isMoving) ProcessMovement(group);
                group.RenderGroup();
                group.RenderIndicators();
            }
        }

        // --- OBSŁUGA KLIKNIĘCIA MYSZKĄ (Bez zmian) ---
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool isPlayerA = (PlayerPrefs.GetString("PlayerSide", "SideA") == "SideA");
            List<TacticalGroup> enemyGroups = isPlayerA ? factionBGroups : factionAGroups;
            TacticalGroup hitEnemy = FindGroupUnderCursor(ray, enemyGroups);

            if (hitEnemy != null)
            {
                if (selectedEnemyGroup != null && selectedEnemyGroup != hitEnemy) SetGroupHighlight(selectedEnemyGroup, false);
                selectedEnemyGroup = hitEnemy;
                SetGroupHighlight(selectedEnemyGroup, true, enemyHighlightMaterial);
                return;
            }

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, groundLayerMask))
            {
                if (selectedGroups.Count > 0) SetCursorPosition(hit.point);
                else
                {
                    HideCursorMarker();
                    if (selectedEnemyGroup != null) { SetGroupHighlight(selectedEnemyGroup, false); selectedEnemyGroup = null; }
                }
            }
        }

        // --- WALKA (Tylko gdy bitwa trwa) ---
        if (isCombatActive) ResolveCombatRoundRobin();
    }
    public void HideCursorMarker()
    {
        if (activeCursorMarker != null)
        {
            activeCursorMarker.SetActive(false);
        }
        // WAŻNE: Nie ustawiamy hasCursorPosition = false tutaj.
        // AI potrzebuje jeszcze chwili (sekunda/dwie), żeby odebrać dane.
    }

    public TacticalGroup GetGroupByName(string name)
    {
        // Szukamy w obu frakcjach
        foreach (var g in factionAGroups)
            if (g.groupName == name || g.customName == name) return g;

        foreach (var g in factionBGroups)
            if (g.groupName == name || g.customName == name) return g;

        return null;
    }
    void OpenNamingPanel()
    {
        if (namingPanel != null)
        {
            namingPanel.SetActive(true);
            nameInput.text = "";
            if (warningText != null) warningText.text = "";
            nameInput.Select();
            nameInput.ActivateInputField();
        }
    }

    public void OnMergeConfirmed()
    {
        string newName = nameInput.text;

        if (string.IsNullOrEmpty(newName))
        {
            if (warningText != null) warningText.text = "Nazwa nie może być pusta!";
            return;
        }

        if (IsNameTaken(newName))
        {
            if (warningText != null)
            {
                warningText.text = "Nazwa już istnieje!";
                nameInput.Select();
            }
            return;
        }

        if (warningText != null) warningText.text = "";
        MergeSelectedGroups(newName);

        if (namingPanel != null) namingPanel.SetActive(false);
    }

    void MergeSelectedGroups(string customName)
    {
        if (selectedGroups.Count < 2) return;

        bool belongsToFactionA = factionAGroups.Contains(selectedGroups[0]);
        bool belongsToFactionB = factionBGroups.Contains(selectedGroups[0]);

        if (!belongsToFactionA && !belongsToFactionB) return;

        var firstGroupArch = selectedGroups[0].archetypes[0];

        TacticalGroup superGroup = new TacticalGroup(
            "Merged_" + customName,
            firstGroupArch.unitMesh,
            firstGroupArch.unitMaterial,
            HierarchyRank.Brigade,
            firstGroupArch.type
        );

        superGroup.archetypes.Clear();
        superGroup.customName = customName;
        superGroup.isMergedGroup = true;

        Dictionary<string, int> shapeToArchetypeMap = new Dictionary<string, int>();

        foreach (var oldGroup in selectedGroups)
        {
            for (int k = 0; k < oldGroup.archetypes.Count; k++)
            {
                var oldArch = oldGroup.archetypes[k];
                string uniqueKey = oldArch.unitMesh.name + "_" + oldArch.type.ToString();

                int targetArchetypeIndex = -1;

                if (!shapeToArchetypeMap.TryGetValue(uniqueKey, out targetArchetypeIndex))
                {
                    UnitArchetype newArch = new UnitArchetype();
                    newArch.type = oldArch.type;
                    newArch.unitMesh = oldArch.unitMesh;
                    newArch.unitMaterial = oldArch.unitMaterial;
                    newArch.originalMaterial = oldArch.originalMaterial;
                    newArch.arrowMesh = oldArch.arrowMesh;
                    newArch.arrowMaterial = oldArch.arrowMaterial;
                    newArch.speedMultiplier = oldArch.speedMultiplier;

                    superGroup.archetypes.Add(newArch);
                    targetArchetypeIndex = superGroup.archetypes.Count - 1;
                    shapeToArchetypeMap.Add(uniqueKey, targetArchetypeIndex);
                }

                for (int i = 0; i < oldGroup.unitMatrices.Count; i++)
                {
                    if (oldGroup.unitArchetypeIndices[i] == k)
                    {
                        superGroup.AddUnit(oldGroup.unitMatrices[i], oldGroup.unitHealth[i], targetArchetypeIndex);
                        superGroup.attackCooldowns[superGroup.attackCooldowns.Count - 1] = oldGroup.attackCooldowns[i];
                    }
                }
            }

            oldGroup.unitMatrices.Clear();
            var uiToRemove = uiElements.Find(x => x.GetGroup() == oldGroup);
            if (uiToRemove != null)
            {
                uiElements.Remove(uiToRemove);
                Destroy(uiToRemove.gameObject);
            }

            if (belongsToFactionA)
            {
                if (factionAGroups.Contains(oldGroup)) factionAGroups.Remove(oldGroup);
            }
            else
            {
                if (factionBGroups.Contains(oldGroup)) factionBGroups.Remove(oldGroup);
            }
        }

        superGroup.RebuildBatchesFull();
        superGroup.RecalculateStats();

        if (belongsToFactionA) factionAGroups.Add(superGroup);
        else factionBGroups.Add(superGroup);

        GameObject newUIObj = Instantiate(groupUIPrefab, uiContainer);
        ArmyGroupUI newUI = newUIObj.GetComponent<ArmyGroupUI>();
        newUI.Setup(superGroup, this);
        uiElements.Add(newUI);

        selectedGroups.Clear();
        UpdateUISelectionVisuals();
    }

    void ProcessMovement(TacticalGroup group)
    {
        Vector3 flatCenter = new Vector3(group.groupCenter.x, 0, group.groupCenter.z);
        Vector3 flatTarget = new Vector3(group.currentDestination.x, 0, group.currentDestination.z);

        // --- 1. Czy gonimy wroga? ---
        bool isChasing = (group.chaseTarget != null && group.chaseTarget.unitMatrices.Count > 0);
        float distToTargetSq = Vector3.SqrMagnitude(flatTarget - flatCenter);

        // Warunek zatrzymania: jeśli nie gonimy, nie formujemy się i jesteśmy u celu
        if (!isChasing && !group.isReforming && distToTargetSq < 0.5f)
        {
            group.isMoving = false;
            return;
        }

        // --- 2. Kierunek grupy ---
        Vector3 groupDirection = (flatTarget - flatCenter).normalized;
        if (groupDirection == Vector3.zero) groupDirection = group.targetFacingDirection;
        if (groupDirection == Vector3.zero) groupDirection = Vector3.forward;

        // Obliczanie rotacji dla całej grupy (bazowe)
        Quaternion targetRot = Quaternion.LookRotation(groupDirection);
        if (group.isReforming && group.unitMatrices.Count > 0)
            targetRot = group.unitMatrices[0].rotation;

        Quaternion currentRot = group.unitMatrices.Count > 0 ? group.unitMatrices[0].rotation : Quaternion.identity;
        Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);

        float globalBaseSpeed = moveSpeed * Time.deltaTime;
        int unitsReachedTarget = 0;

        // Cache danych terenu dla lasu
        int alphaWidth = 0, alphaHeight = 0;
        if (terrainData != null)
        {
            alphaWidth = terrainData.alphamapWidth;
            alphaHeight = terrainData.alphamapHeight;
        }

        // --- Parametry SEPARACJI ---
        float separationRadius = 1.1f;
        float separationStrength = isChasing ? 0.8f : 2.0f; // Słabsza separacja w ataku
        float maxSeparationForce = 0.5f;

        // --- PĘTLA PO JEDNOSTKACH ---
        for (int i = 0; i < group.unitMatrices.Count; i++)
        {
            Matrix4x4 mat = group.unitMatrices[i];
            Vector3 currentPos = mat.GetColumn(3);
            Vector3 scaleVec = mat.lossyScale;
            UnitArchetype arch = group.GetUnitArchetype(i);

            // Promień kolizji (dla kawalerii większy)
            float myRadius = separationRadius;
            if (arch != null && arch.type == UnitType.Cavalry) myRadius *= 1.8f;

            // Bazowa prędkość jednostki
            float currentMoveSpeed = globalBaseSpeed;
            if (arch != null) currentMoveSpeed *= arch.speedMultiplier;

            Vector3 moveDirection = Vector3.zero;

            // Zmienne dla systemu indywidualnej walki
            Vector3 individualTargetPos = Vector3.zero;
            bool hasIndividualTarget = false;
            bool isInAttackRange = false; // Czy stoję i walczę?

            // --- A. NOWA LOGIKA: MAGNETYZM WALKI (Melee Magnetism) ---
            float engagementRange = 20.0f; // Zasięg "zrywania się" do wroga
            float stopAtEnemyDist = (arch.type == UnitType.Archer) ? attackRangeArcher * 0.9f : attackRangeMelee * 0.85f;

            if (isChasing && group.chaseTarget != null)
            {
                float distToEnemyGroup = Vector3.Distance(currentPos, group.chaseTarget.groupCenter);

                // Jeśli jesteśmy blisko wrogiej grupy, szukamy konkretnego celu
                if (distToEnemyGroup < engagementRange + group.chaseTarget.groupRadius)
                {
                    Vector3 closestEnemyPos = Vector3.zero;
                    float closestDistSq = float.MaxValue;
                    bool foundEnemy = false;

                    // Szukanie najbliższego wroga
                    foreach (var enemyMat in group.chaseTarget.unitMatrices)
                    {
                        Vector3 ePos = (Vector3)enemyMat.GetColumn(3);
                        float dSq = (ePos - currentPos).sqrMagnitude;
                        if (dSq < closestDistSq)
                        {
                            closestDistSq = dSq;
                            closestEnemyPos = ePos;
                            foundEnemy = true;
                        }
                    }

                    if (foundEnemy)
                    {
                        hasIndividualTarget = true;
                        individualTargetPos = closestEnemyPos;

                        float distToEnemy = Mathf.Sqrt(closestDistSq);

                        if (distToEnemy <= stopAtEnemyDist)
                        {
                            // JESTEM W ZASIĘGU ATAKU -> STOP
                            isInAttackRange = true;
                            moveDirection = Vector3.zero;
                            currentMoveSpeed = 0f;
                        }
                        else
                        {
                            // POZA ZASIĘGIEM -> IDŹ DO KONKRETNEGO WROGA
                            moveDirection = (closestEnemyPos - currentPos).normalized;
                        }
                    }
                }
            }

            // --- B. STARA LOGIKA: RUCH FORMACJI ---
            // Wykonujemy tylko, jeśli NIE mamy indywidualnego celu
            if (!hasIndividualTarget)
            {
                if (group.isReforming)
                {
                    if (i < group.formationTargets.Count)
                    {
                        Vector3 targetPos = group.formationTargets[i];
                        Vector3 diff = targetPos - currentPos;
                        Vector3 flatDiff = new Vector3(diff.x, 0, diff.z);
                        if (flatDiff.magnitude < 0.2f) { moveDirection = Vector3.zero; unitsReachedTarget++; }
                        else moveDirection = flatDiff.normalized;
                    }
                    else unitsReachedTarget++;
                }
                else
                {
                    moveDirection = groupDirection;
                }
            }

            // --- C. SEPARACJA (Unikanie tłoku) ---
            // Wyłączamy separację TYLKO gdy stoimy i walczymy (isInAttackRange), żeby nie drgać
            if (!isInAttackRange && moveDirection != Vector3.zero)
            {
                Vector3 separationForce = Vector3.zero;
                int neighborsCount = 0;
                int checkRange = 4;
                int startCheck = Mathf.Max(0, i - checkRange);
                int endCheck = Mathf.Min(group.unitMatrices.Count, i + checkRange + 1);

                for (int n = startCheck; n < endCheck; n++)
                {
                    if (n == i) continue;
                    Vector3 neighborPos = (Vector3)group.unitMatrices[n].GetColumn(3);
                    Vector3 toNeighbor = currentPos - neighborPos;
                    toNeighbor.y = 0;

                    float distSq = toNeighbor.sqrMagnitude;
                    float safeDistSq = myRadius * myRadius;

                    if (distSq < safeDistSq && distSq > 0.001f)
                    {
                        float pushStrength = (safeDistSq - distSq) / safeDistSq;
                        separationForce += toNeighbor.normalized * pushStrength;
                        neighborsCount++;
                    }
                }

                if (neighborsCount > 0)
                {
                    separationForce /= neighborsCount;
                    separationForce *= separationStrength;
                    separationForce = Vector3.ClampMagnitude(separationForce, maxSeparationForce);

                    // Jeśli atakujemy, separacja jest słabsza (pozwala na ścisk)
                    if (hasIndividualTarget) separationForce *= 0.4f;

                    moveDirection += separationForce;
                    moveDirection.Normalize();
                }
            }

            // --- D. APLIKOWANIE RUCHU Z FIZYKĄ TERENU I LASU ---
            Vector3 newPos = currentPos;

            // Ruszamy się tylko, jeśli nie walczymy w miejscu (isInAttackRange == false)
            if (!isInAttackRange && moveDirection != Vector3.zero && currentMoveSpeed > 0.001f)
            {
                // 1. Woda
                float terrainY = currentPos.y;
                if (terrain != null) terrainY = terrain.SampleHeight(currentPos) + terrain.transform.position.y;

                bool inWater = false;
                float waterHeight = -1000f;
                RaycastHit hit;
                if (Physics.Raycast(currentPos + Vector3.up * 50f, Vector3.down, out hit, 100f, waterLayerMask))
                {
                    if (hit.point.y > terrainY - 0.1f) { inWater = true; waterHeight = hit.point.y; }
                }

                // 2. Nachylenie (Slope)
                float slopeMod = 1.0f;
                if (terrain != null && !inWater)
                {
                    Vector3 probePos = currentPos + (moveDirection * slopeCheckDistance);
                    float nextY = terrain.SampleHeight(probePos) + terrain.transform.position.y;
                    float angle = Mathf.Atan2(nextY - terrainY, slopeCheckDistance) * Mathf.Rad2Deg;

                    if (angle > steepSlopeAngle) slopeMod = uphillSteepMult;
                    else if (angle > lightSlopeAngle) slopeMod = uphillLightMult;
                    else if (angle < -steepSlopeAngle) slopeMod = downhillSteepMult;
                    else if (angle < -lightSlopeAngle) slopeMod = downhillLightMult;
                }

                // 3. Las (Forest)
                float forestMod = 1.0f;
                if (forestLayerIndex != -1 && !inWater && terrainData != null)
                {
                    int mapX = Mathf.RoundToInt(((currentPos.x - terrain.transform.position.x) / terrainData.size.x) * alphaWidth);
                    int mapZ = Mathf.RoundToInt(((currentPos.z - terrain.transform.position.z) / terrainData.size.z) * alphaHeight);

                    if (mapX >= 0 && mapX < alphaWidth && mapZ >= 0 && mapZ < alphaHeight)
                    {
                        // Sprawdzamy, czy w tym punkcie jest tekstura lasu
                        if (terrainData.GetAlphamaps(mapX, mapZ, 1, 1)[0, 0, forestLayerIndex] > 0.5f)
                        {
                            if (arch != null)
                                forestMod = (arch.type == UnitType.Cavalry) ? forestCavalryPenalty : forestInfantryPenalty;
                        }
                    }
                }

                // 4. Finalna prędkość
                float finalMod = slopeMod * forestMod;
                if (inWater) finalMod *= waterSpeedPenalty;

                newPos = currentPos + (moveDirection * currentMoveSpeed * finalMod);

                // Clamp mapy
                newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
                newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

                // Ustalenie wysokości Y
                if (inWater) newPos.y = waterHeight - swimmingDepth;
                else if (terrain != null) newPos.y = terrain.SampleHeight(newPos) + terrain.transform.position.y + 0.5f;
                else newPos.y = terrainY;
            }

            // --- E. ROTACJA (Najważniejsze dla anty-jitter) ---
            Quaternion targetUnitRot;

            if (isInAttackRange)
            {
                // Jeśli walczę, patrzę SZTYWNO na wroga
                Vector3 dirToTarget = (individualTargetPos - currentPos).normalized;
                if (dirToTarget != Vector3.zero)
                    targetUnitRot = Quaternion.LookRotation(dirToTarget);
                else
                    targetUnitRot = mat.rotation;
            }
            else if (hasIndividualTarget)
            {
                // Jeśli biegnę na wroga, patrzę w kierunku biegu
                targetUnitRot = Quaternion.LookRotation(moveDirection);
            }
            else if (group.isReforming && moveDirection.sqrMagnitude > 0.1f)
            {
                // Jeśli formuję szyk
                targetUnitRot = Quaternion.LookRotation(moveDirection);
            }
            else
            {
                // Zwykły marsz / stanie
                targetUnitRot = nextRot;
            }

            // Jeśli walczymy (isInAttackRange), obrót jest szybszy (20f), żeby nie było opóźnienia
            float rotSpeed = isInAttackRange ? 20f : 10f;
            Quaternion finalRot = Quaternion.Slerp(mat.rotation, targetUnitRot, rotSpeed * Time.deltaTime);

            // Zapis macierzy
            group.unitMatrices[i] = Matrix4x4.TRS(newPos, finalRot, scaleVec);
        }

        // --- F. FINALIZACJA ---
        if (group.isReforming && unitsReachedTarget >= group.unitMatrices.Count)
        {
            group.isReforming = false;
            group.isMoving = false;
            ApplyFinalFormationRotation(group);
        }

        group.UpdateBatchData();
        group.RecalculateCenter();
    }

    Vector3 GetSmartLookTarget(TacticalGroup group)
    {
        // 1. Jeśli mamy zaznaczonego wroga czerwonym kółkiem -> patrzymy na niego
        if (selectedEnemyGroup != null && selectedEnemyGroup.unitMatrices.Count > 0)
        {
            return selectedEnemyGroup.groupCenter;
        }

        // 2. Jeśli grupa ma swój cel pościgu -> patrzymy na niego
        if (group.chaseTarget != null && group.chaseTarget.unitMatrices.Count > 0)
        {
            return group.chaseTarget.groupCenter;
        }

        // 3. Domyślnie: Patrzymy na środek armii przeciwnej (Globalna świadomość)
        List<TacticalGroup> enemies = (factionAGroups.Contains(group)) ? factionBGroups : factionAGroups;

        // Znajdź najbliższego wroga, żeby nie patrzeć na drugi koniec mapy
        TacticalGroup closest = FindClosestEnemy(group.groupCenter, enemies);
        if (closest != null) return closest.groupCenter;

        // Fallback: Środek mapy po stronie wroga
        float enemyZ = (factionAGroups.Contains(group)) ? maxZ : minZ;
        return new Vector3((minX + maxX) / 2, 0, enemyZ);
    }
    void ApplyFinalFormationRotation(TacticalGroup group)
    {
        if (group.unitMatrices.Count == 0) return;
        Quaternion finalRot = (group.targetFacingDirection != Vector3.zero)
            ? Quaternion.LookRotation(group.targetFacingDirection)
            : group.unitMatrices[0].rotation;
        Vector3 scale = group.unitMatrices[0].lossyScale;

        for (int i = 0; i < group.unitMatrices.Count; i++)
        {
            Vector3 pos = group.unitMatrices[i].GetColumn(3);
            group.unitMatrices[i] = Matrix4x4.TRS(pos, finalRot, scale);
        }
        group.UpdateBatchData();
    }

    // --- NOWA FUNKCJA: Ustawianie na linii terenu ---
    // Znajdź tę funkcję i podmień jej zawartość (sekcję isHill)
    public void MoveGroupToLineFeature(TacticalGroup group, TerrainFeature feature, string mode = "Top")
    {
        string pSide = PlayerPrefs.GetString("PlayerSide", "SideA");
        bool isPlayerGroup = (pSide == "SideA" && factionAGroups.Contains(group)) ||
                             (pSide == "SideB" && factionBGroups.Contains(group));
        // if (isDeploymentPhase && !isPlayerGroup)
        // {
        //     group.pendingOrder = new PendingOrderData
        //     {
        //         type = PendingOrderType.FeatureMove,
        //         feature = feature,
        //         chaseTarget = null
        //     };
        //     group.isMoving = false;
        //     return;
        // }

        if (group.unitMatrices.Count == 0) return;

        group.assignedFeature = feature;

        Vector3 center = feature.transform.position;
        // Uwzględniamy rotację obiektu
        Vector3 lineDir = feature.transform.TransformDirection(feature.lineOrientation).normalized;
        Vector3 featureNormal = Vector3.Cross(Vector3.up, lineDir).normalized;

        Vector3 faceDir = featureNormal;

        // --- ZMODYFIKOWANA LOGIKA RELATYWNA (PERSPEKTYWA GRACZA) ---
        if (feature.isHill)
        {
            // 1. Sprawdzamy, kim gra gracz (pobieramy to z ustawień zapisanych w LLMCommander/MainMenu)
            string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
            bool amIRomans = (playerSide == "SideA"); // Zakładamy: SideA (Rzym) startuje na dole (MinZ), SideB (Goci) na górze (MaxZ)

            // 2. Definiujemy wektor "w stronę mojej bazy"
            // Jeśli jestem Rzymianinem (dół mapy), moja baza jest w kierunku -Z (Back).
            // Jeśli jestem Gotem (góra mapy), moja baza jest w kierunku +Z (Forward).
            Vector3 directionToMyBase = amIRomans ? Vector3.back : Vector3.forward;

            // 3. Sprawdzamy, która strona wzgórza jest bliżej mojej bazy
            // Iloczyn skalarny powie nam, czy featureNormal celuje w stronę bazy, czy przeciwną
            float dot = Vector3.Dot(directionToMyBase, featureNormal);

            // 'frontNormal' to wektor prostopadły do wzgórza, celujący w stronę GRACZA (bezpieczną)
            Vector3 frontNormal = (dot > 0) ? featureNormal : -featureNormal;

            // 'backNormal' to wektor celujący w stronę WROGA (za wzgórzem)
            Vector3 backNormal = -frontNormal;

            switch (mode)
            {
                case "Front": // "Przed wzgórzem" (Moja strona)
                    // Ustawiamy się po naszej stronie
                    center += frontNormal * feature.widthOffset;

                    // Patrzymy w stronę wzgórza/wroga (czyli w kierunku backNormal)
                    faceDir = backNormal;
                    break;

                case "Back": // "Za wzgórzem" (Strona wroga)
                    // Przechodzimy na drugą stronę
                    center += backNormal * feature.widthOffset;

                    // Patrzymy dalej w głąb terytorium wroga (lub asekurujemy tyły)
                    faceDir = backNormal;
                    break;

                case "Top": // "Na szczycie"
                default:
                    // Stoimy na środku, ale patrzymy w stronę nadchodzącego wroga
                    faceDir = frontNormal;
                    break;
            }
        }
        else if (feature.isRiver)
        {
            // Dla rzeki logika pozostaje bez zmian (brzeg bliższy jednostce)
            Vector3 toWater = (feature.transform.position - center).normalized;
            if (Vector3.Dot(toWater, featureNormal) > 0) faceDir = featureNormal;
            else faceDir = -featureNormal;
            center = feature.transform.position + (-faceDir * feature.widthOffset);
        }

        // --- RESZTA FUNKCJI BEZ ZMIAN ---
        // (Obliczanie szeregów, spacingu i pozycji jednostek)

        int myRankIndex = 0;
        List<TacticalGroup> myFaction = (factionAGroups.Contains(group)) ? factionAGroups : factionBGroups;
        foreach (var other in myFaction)
        {
            if (other == group) break;
            if (other.assignedFeature == feature) myRankIndex++;
        }

        // Cofamy kolejne oddziały, żeby nie właziły na siebie
        center -= faceDir * (myRankIndex * 12.0f);

        group.targetFacingDirection = faceDir;

        int unitCount = group.unitMatrices.Count;
        float spacing = 1.5f;
        int maxUnitsPerRow = Mathf.FloorToInt(feature.lineLength / spacing);
        if (maxUnitsPerRow < 1) maxUnitsPerRow = 1;

        float currentLineWidth = Mathf.Min(unitCount, maxUnitsPerRow) * spacing;
        Vector3 lineStart = center - (lineDir * currentLineWidth * 0.5f);

        while (group.formationTargets.Count < unitCount) group.formationTargets.Add(Vector3.zero);

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / maxUnitsPerRow;
            int col = i % maxUnitsPerRow;

            Vector3 pos = lineStart
                        + (lineDir * col * spacing)
                        - (faceDir * row * spacing);

            if (terrain != null)
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            else
                pos.y = group.groupCenter.y;

            group.formationTargets[i] = pos;
        }

        group.isReforming = true;
        group.isMoving = true;
        group.currentDestination = center;
    }
    void ResolveCombatRoundRobin()
    {
        int totalBudget = maxCombatChecksPerFrame;
        int halfBudget = totalBudget / 2;

        spatialGrid.Clear();
        foreach (var groupB in factionBGroups)
        {
            if (groupB.unitMatrices.Count == 0) continue;
            for (int i = 0; i < groupB.unitMatrices.Count; i++) AddToGrid(groupB, i, (Vector3)groupB.unitMatrices[i].GetColumn(3));
        }
        ProcessFactionAttack(factionAGroups, factionBGroups, ref nextGroupIdxA, ref nextUnitIdxA, halfBudget);

        spatialGrid.Clear();
        foreach (var groupA in factionAGroups)
        {
            if (groupA.unitMatrices.Count == 0) continue;
            for (int i = 0; i < groupA.unitMatrices.Count; i++) AddToGrid(groupA, i, (Vector3)groupA.unitMatrices[i].GetColumn(3));
        }
        ProcessFactionAttack(factionBGroups, factionAGroups, ref nextGroupIdxB, ref nextUnitIdxB, halfBudget);
    }

    void ProcessFactionAttack(List<TacticalGroup> attackers, List<TacticalGroup> defenders, ref int groupIdx, ref int unitIdx, int budget)
    {
        int groupsChecked = 0;
        while (budget > 0 && groupsChecked < attackers.Count)
        {
            if (groupIdx >= attackers.Count) { groupIdx = 0; unitIdx = 0; }
            var attGroup = attackers[groupIdx];

            if (attGroup.unitMatrices.Count == 0)
            {
                groupIdx++; unitIdx = 0; groupsChecked++;
                continue;
            }

            while (unitIdx < attGroup.unitMatrices.Count && budget > 0)
            {
                ProcessSingleAttacker(attGroup, defenders, unitIdx);
                unitIdx++; budget--;
            }

            if (unitIdx >= attGroup.unitMatrices.Count)
            {
                groupIdx++; unitIdx = 0; groupsChecked++;
            }
        }
    }

    void ProcessSingleAttacker(TacticalGroup attackerGroup, List<TacticalGroup> enemyFaction, int idx)
    {
        // 1. Walidacja indeksów
        if (idx >= attackerGroup.attackCooldowns.Count || idx >= attackerGroup.unitMatrices.Count) return;

        // Pobranie danych atakującego
        Matrix4x4 myMat = attackerGroup.unitMatrices[idx];
        Vector3 myPos = (Vector3)myMat.GetColumn(3);
        Vector3 myForward = (Vector3)myMat.GetColumn(2);

        UnitArchetype attackerStats = attackerGroup.GetUnitArchetype(idx);
        if (attackerStats == null) return;

        bool isArcher = attackerStats.type == UnitType.Archer;
        float range = isArcher ? attackRangeArcher : attackRangeMelee;
        float rangeSq = range * range;
        float cooldownTime = isArcher ? archerAttackCooldown : attackCooldown;

        // --- SZUKANIE CELU (PRIORYTETYZACJA) ---
        UnitRef target = new UnitRef { group = null };

        // KROK A: Czy mam rozkaz ataku na konkretny oddział?
        bool hasSpecificOrder = (attackerGroup.chaseTarget != null && attackerGroup.chaseTarget.unitMatrices.Count > 0);

        if (hasSpecificOrder)
        {
            // Sprawdzamy, czy ktoś z CELU GŁÓWNEGO jest w zasięgu
            target = FindClosestUnitInGroup(myPos, rangeSq, attackerGroup.chaseTarget);

            // Jeśli cel główny jest daleko (target.group == null), to:
            // Opcja 1 (Lojalność): Nie atakujemy nikogo innego, biegniemy dalej (return).
            // Opcja 2 (Samoobrona): Jeśli cel główny daleko, ale ktoś inny mnie bije, to go biję.

            // Wybieramy Opcję 1 dla "Poczucia kontroli" - jak klikasz atak na łuczników, 
            // to Twoi ludzie mają olać piechotę i biec do łuczników, nawet jak obrywają.
            if (target.group == null)
            {
                // Redukcja cooldownu w biegu i wyjście (jednostka będzie się ruszać w ProcessMovement)
                if (attackerGroup.attackCooldowns[idx] > 0) attackerGroup.attackCooldowns[idx] -= Time.deltaTime;
                return;
            }
        }
        else
        {
            // KROK B: Nie mam konkretnego celu (Attack Move / Idle) -> Biję najbliższego
            if (isArcher) target = FindTargetGlobal(myPos, range, rangeSq, enemyFaction);
            else target = FindTargetInGrid(myPos, rangeSq);
        }

        // --- KONIEC ZMIAN W SZUKANIU ---

        // Dalsza część funkcji (Zadawanie obrażeń) - bez zmian, wklejam dla kontekstu
        if (target.group != null)
        {
            if (target.index >= target.group.unitMatrices.Count) return;

            Vector3 dirToTarget = (target.position - myPos).normalized;
            float angleDot = Vector3.Dot(myForward, dirToTarget);

            float requiredAngle = isArcher ? 0.5f : -0.5f;
            if (angleDot < requiredAngle) return;

            attackerGroup.lastCombatTimes[idx] = Time.time;

            if (attackerGroup.attackCooldowns[idx] > 0)
            {
                attackerGroup.attackCooldowns[idx] -= Time.deltaTime;
                return;
            }

            if (target.index < target.group.lastCombatTimes.Count)
                target.group.lastCombatTimes[target.index] = Time.time;

            // (Reszta logiki DMG, Backstab, KillUnit - taka sama jak w poprzednim pliku)
            Matrix4x4 targetMat = target.group.unitMatrices[target.index];
            Vector3 targetForward = (Vector3)targetMat.GetColumn(2);
            float orientationDot = Vector3.Dot(myForward, targetForward);
            bool isBackstab = (orientationDot > 0.5f);

            float dmg = 0f;
            if (isBackstab) dmg = 10000f;
            else dmg = CalculateDamage(attackerGroup, target.group, idx, target.index, myPos, target.position);

            target.group.unitHealth[target.index] -= dmg;
            attackerGroup.attackCooldowns[idx] = cooldownTime * Random.Range(0.8f, 1.2f);

            if (target.group.unitHealth[target.index] <= 0)
            {
                KillUnit(target.group, target.index);
                attackerGroup.lastCombatTimes[idx] = -100f;
            }
        }
        else
        {
            if (attackerGroup.attackCooldowns[idx] > 0)
                attackerGroup.attackCooldowns[idx] -= Time.deltaTime;
        }
    }

    public TacticalGroup FindClosestEnemy(Vector3 searchPos, List<TacticalGroup> enemyList)
    {
        TacticalGroup best = null;
        float closestDist = float.MaxValue;

        foreach (var enemy in enemyList)
        {
            if (enemy.unitMatrices.Count == 0) continue; // Martwych pomijamy

            float d = Vector3.Distance(searchPos, enemy.groupCenter);
            if (d < closestDist)
            {
                closestDist = d;
                best = enemy;
            }
        }
        return best;
    }
    // Nowa funkcja do szukania celu TYLKO w konkretnym oddziale (dla Focus Fire)
    UnitRef FindClosestUnitInGroup(Vector3 myPos, float rangeSq, TacticalGroup targetGroup)
    {
        int bestIdx = -1;
        float bestDistSq = rangeSq; // Musi być w zasięgu

        for (int i = 0; i < targetGroup.unitMatrices.Count; i++)
        {
            Vector3 enemyPos = (Vector3)targetGroup.unitMatrices[i].GetColumn(3);
            float dSq = (enemyPos - myPos).sqrMagnitude;

            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestIdx = i;
            }
        }

        if (bestIdx != -1)
        {
            Vector3 foundPos = (Vector3)targetGroup.unitMatrices[bestIdx].GetColumn(3);
            return new UnitRef { group = targetGroup, index = bestIdx, position = foundPos };
        }

        return new UnitRef { group = null };
    }

    // 2. Dodaj metodę aktualizującą pościg:
    void UpdateChaseLogic(TacticalGroup group)
    {
        if (isDeploymentPhase) return;

        // Scenariusz A: Mamy cel, ale on już nie żyje (oddział wybity do nogi)
        if (group.chaseTarget != null && group.chaseTarget.unitMatrices.Count == 0)
        {
            // Zamiast się zatrzymywać (group.chaseTarget = null), szukamy nowego celu!
            List<TacticalGroup> enemies = (factionAGroups.Contains(group)) ? factionBGroups : factionAGroups;

            // Znajdź najbliższego żywego wroga
            TacticalGroup newTarget = FindClosestEnemy(group.groupCenter, enemies);

            if (newTarget != null)
            {
                group.chaseTarget = newTarget;
                // Debug.Log($"Grupa {group.groupName} zniszczyła cel i zmienia cel na: {newTarget.groupName}");
            }
            else
            {
                // Dopiero gdy nie ma już NIKOGO na mapie, zatrzymujemy się
                group.chaseTarget = null;
                group.isMoving = false;
            }
        }

        // Scenariusz B: Mamy żywy cel -> gonimy go
        if (group.chaseTarget != null && group.chaseTarget.unitMatrices.Count > 0)
        {
            group.currentDestination = group.chaseTarget.groupCenter;
            group.isMoving = true;
            group.isReforming = false;
        }
    }

    UnitRef FindTargetGlobal(Vector3 myPos, float baseRange, float unusedRangeSq, List<TacticalGroup> enemies)
    {
        int startIdx = Random.Range(0, enemies.Count);
        for (int i = 0; i < enemies.Count; i++)
        {
            int idx = (startIdx + i) % enemies.Count;
            var group = enemies[idx];
            if (group.unitMatrices.Count == 0) continue;
            Vector3 centerDir = group.groupCenter - myPos;
            float flatDistToGroup = new Vector2(centerDir.x, centerDir.z).magnitude;
            if (flatDistToGroup < baseRange + group.groupRadius + 50f)
            {
                int attempts = 3;
                for (int k = 0; k < attempts; k++)
                {
                    int randIdx = Random.Range(0, group.unitMatrices.Count);
                    Vector3 enemyPos = (Vector3)group.unitMatrices[randIdx].GetColumn(3);
                    float heightDiff = myPos.y - enemyPos.y;
                    float effectiveRange = baseRange + (heightDiff * heightRangeBonus);
                    if (effectiveRange < 10f) effectiveRange = 10f;

                    float dx = myPos.x - enemyPos.x;
                    float dz = myPos.z - enemyPos.z;
                    float distFlatSq = (dx * dx) + (dz * dz);

                    if (distFlatSq < effectiveRange * effectiveRange)
                    {
                        Vector3 originHead = myPos + Vector3.up * 1.5f;
                        Vector3 targetHead = enemyPos + Vector3.up * 1.0f;
                        if (!Physics.Linecast(originHead, targetHead, terrainLayerMask))
                            return new UnitRef { group = group, index = randIdx, position = enemyPos };
                    }
                }
            }
        }
        return new UnitRef { group = null };
    }

    void AddToGrid(TacticalGroup group, int index, Vector3 pos)
    {
        int key = GetGridKey(pos);
        if (!spatialGrid.ContainsKey(key)) spatialGrid[key] = new List<UnitRef>();
        spatialGrid[key].Add(new UnitRef { group = group, index = index, position = pos });
    }

    int GetGridKey(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / cellSize);
        int z = Mathf.FloorToInt(pos.z / cellSize);
        return (x * 73856093) ^ (z * 19349663);
    }

    UnitRef FindTargetInGrid(Vector3 pos, float rangeSq)
    {
        int cx = Mathf.FloorToInt(pos.x / cellSize);
        int cz = Mathf.FloorToInt(pos.z / cellSize);

        UnitRef bestTarget = new UnitRef { group = null };
        float closestDistSq = rangeSq; // Szukamy tylko w zasięgu

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                int key = ((cx + x) * 73856093) ^ ((cz + z) * 19349663);
                if (spatialGrid.TryGetValue(key, out List<UnitRef> list))
                {
                    foreach (var u in list)
                    {
                        float dSq = (u.position - pos).sqrMagnitude;
                        // Jeśli ten wróg jest bliżej niż poprzedni znaleziony ORAZ jest w zasięgu
                        if (dSq < closestDistSq)
                        {
                            closestDistSq = dSq;
                            bestTarget = u;
                        }
                    }
                }
            }
        }
        return bestTarget;
    }

    float CalculateDamage(TacticalGroup attGroup, TacticalGroup defGroup, int attIdx, int defIdx, Vector3 attPos, Vector3 defPos)
    {
        UnitArchetype attackerStats = attGroup.GetUnitArchetype(attIdx);
        if (attackerStats == null) return 1.0f;

        float finalDamage = 0f;
        switch (attackerStats.type)
        {
            case UnitType.Infantry: finalDamage = dmgInfantry; break;
            case UnitType.Archer: finalDamage = Random.Range(dmgArcherMin, dmgArcherMax); break;
            case UnitType.Cavalry:
                finalDamage = attGroup.isMoving ? dmgCavalryCharge : dmgCavalryStatic;
                break;
        }

        if (attPos.y > defPos.y + 1.0f) finalDamage *= 1.25f;
        else if (attPos.y < defPos.y - 1.0f) finalDamage *= 0.75f;
        return finalDamage;
    }

    void KillUnit(TacticalGroup group, int index)
    {
        group.RemoveUnitAt(index);
        group.RebuildBatchesFull();

        if (group.unitMatrices.Count == 0)
        {
            for (int i = uiElements.Count - 1; i >= 0; i--)
            {
                if (uiElements[i].GetGroup() == group)
                {
                    Destroy(uiElements[i].gameObject);
                    uiElements.RemoveAt(i); break;
                }
            }
            if (selectedGroups.Contains(group)) selectedGroups.Remove(group);
            if (lastSelectedGroup == group) lastSelectedGroup = null;
        }
        else
        {
            foreach (var ui in uiElements) if (ui.GetGroup() == group) { ui.UpdateUnitCount(); break; }
        }
        UpdateGlobalCounters();
    }

    void KillRandomUnit(bool isFactionA)
    {
        var targetFaction = isFactionA ? factionAGroups : factionBGroups;
        foreach (var g in targetFaction) if (g.unitMatrices.Count > 0) { KillUnit(g, g.unitMatrices.Count - 1); return; }
    }

    void UpdateGlobalCounters()
    {
        int totalA = 0; foreach (var g in factionAGroups) totalA += g.unitMatrices.Count;
        int totalB = 0; foreach (var g in factionBGroups) totalB += g.unitMatrices.Count;

        if (counterTextA != null) counterTextA.text = totalA.ToString();
        if (counterTextB != null) counterTextB.text = totalB.ToString();
        CheckWinCondition(totalA, totalB);
    }

    void SetGroupHighlight(TacticalGroup group, bool highlight, Material customMaterial = null)
    {
        if (group == null || group.unitMatrices.Count == 0) return;

        Material targetMat = (customMaterial != null) ? customMaterial : highlightMaterial;

        foreach (var arch in group.archetypes)
        {
            if (highlight && targetMat != null)
            {
                arch.unitMaterial = targetMat;
            }
            else
            {
                arch.unitMaterial = arch.originalMaterial;
            }
        }
    }

    public void SelectGroup(TacticalGroup group)
    {
        if (group.unitMatrices.Count == 0) return;
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shift && lastSelectedGroup != null && lastSelectedGroup != group) HandleShiftSelection(group);
        else if (ctrl)
        {
            if (selectedGroups.Contains(group)) { SetGroupHighlight(group, false); selectedGroups.Remove(group); }
            else { selectedGroups.Add(group); SetGroupHighlight(group, true); lastSelectedGroup = group; }
        }
        else
        {
            foreach (var g in selectedGroups) SetGroupHighlight(g, false);
            selectedGroups.Clear(); selectedGroups.Add(group); SetGroupHighlight(group, true); lastSelectedGroup = group;
        }
        UpdateUISelectionVisuals();

        if (selectedGroups.Count == 0)
        {
            HideCursorMarker();
        }
    }

    void HandleShiftSelection(TacticalGroup currentGroup)
    {
        int indexStart = -1, indexEnd = -1;
        for (int i = 0; i < uiElements.Count; i++)
        {
            if (uiElements[i].GetGroup() == lastSelectedGroup) indexStart = i;
            if (uiElements[i].GetGroup() == currentGroup) indexEnd = i;
        }
        if (indexStart != -1 && indexEnd != -1)
        {
            int min = Mathf.Min(indexStart, indexEnd); int max = Mathf.Max(indexStart, indexEnd);
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            {
                foreach (var g in selectedGroups) SetGroupHighlight(g, false);
                selectedGroups.Clear();
            }
            for (int i = min; i <= max; i++)
            {
                TacticalGroup g = uiElements[i].GetGroup();
                if (!selectedGroups.Contains(g)) { selectedGroups.Add(g); SetGroupHighlight(g, true); }
            }
        }
    }

    void UpdateUISelectionVisuals() { foreach (var ui in uiElements) ui.SetSelectionState(selectedGroups.Contains(ui.GetGroup())); }

    void OnDrawGizmos()
    {
        // Rysowanie obszarów detekcji wrogów (Czerwone kule)
        Gizmos.color = new Color(1, 0, 0, 0.3f); // Półprzezroczysty czerwony
        if (factionBGroups != null)
        {
            foreach (var group in factionBGroups)
            {
                if (group.unitMatrices.Count > 0)
                {
                    // Rysujemy sferę w miejscu, gdzie kod "myśli", że jest środek grupy
                    Gizmos.DrawSphere(group.groupCenter, group.groupRadius);

                    // Rysujemy drut dla dokładności
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(group.groupCenter, group.groupRadius);
                    Gizmos.color = new Color(1, 0, 0, 0.3f);
                }
            }
        }

        // Debug Raycasta - zobacz gdzie trafiasz myszką
        if (hasCursorPosition)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(lastCursorPosition, 1.0f);
            Gizmos.DrawLine(Camera.main.transform.position, lastCursorPosition);
        }
    }

    void CheckWinCondition(int countA, int countB)
    {
        if (isBattleOver) return;
        string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
        bool playerIsSideA = (playerSide == "SideA");

        if (countA <= 0)
        {
            if (playerIsSideA) GameOver(false);
            else GameOver(true);
        }
        else if (countB <= 0)
        {
            if (!playerIsSideA) GameOver(false);
            else GameOver(true);
        }
    }

    void GameOver(bool isVictory)
    {
        isBattleOver = true;
        Time.timeScale = 0f;

        // PauseMenuController pauseCtrl = FindAnyObjectByType<PauseMenuController>();
        // if (pauseCtrl != null) pauseCtrl.enabled = false;

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
            if (resultText != null)
            {
                if (isVictory) { resultText.text = "WIELKIE ZWYCIĘSTWO!"; resultText.color = Color.green; }
                else { resultText.text = "HANIEBNA PORAŻKA..."; resultText.color = Color.red; }
            }
        }
    }
    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneName);
    }

    public void StartBattle()
    {
        isDeploymentPhase = false;
        if (startBattleButton != null) startBattleButton.SetActive(false);
        Debug.Log("Faza rozstawiania zakończona. Wykonywanie zaplanowanych rozkazów...");

        // Wykonaj rozkazy dla obu frakcji
        ExecutePendingOrdersForFaction(factionAGroups);
        ExecutePendingOrdersForFaction(factionBGroups);
    }

    void ToggleTacticalPause()
    {
        if (isDeploymentPhase || isBattleOver) return;
        isTacticalPause = !isTacticalPause;
        if (tacticalPauseIndicator != null) tacticalPauseIndicator.SetActive(isTacticalPause);
    }

    void ExecutePendingOrdersForFaction(List<TacticalGroup> faction)
    {
        foreach (var group in faction)
        {
            if (group.pendingOrder.type == PendingOrderType.None) continue;

            var order = group.pendingOrder;

            // Czyścimy pending order przed wykonaniem (żeby nie zapętlić)
            group.pendingOrder.type = PendingOrderType.None;

            if (order.type == PendingOrderType.MoveOrForm)
            {
                // Przywracamy cel ataku (jeśli był)
                group.chaseTarget = order.chaseTarget;

                // Wywołujemy ApplyFormation (teraz isDeploymentPhase jest false, więc wykona się fizyka)
                ApplyFormation(group, order.formationName, order.destination, order.lookDirection);
            }
            else if (order.type == PendingOrderType.FeatureMove)
            {
                if (order.feature != null)
                {
                    MoveGroupToLineFeature(group, order.feature);
                }
            }
        }
    }

    bool IsNameTaken(string nameToCheck)
    {
        foreach (var group in factionAGroups)
        {
            if (group.customName == nameToCheck) return true;
            if (group.groupName == nameToCheck) return true;
        }
        foreach (var group in factionBGroups)
        {
            if (group.customName == nameToCheck) return true;
            if (group.groupName == nameToCheck) return true;
        }
        return false;
    }

    // --- NOWOŚĆ: SYSTEM FORMACJI ---
    public void ApplyFormation(TacticalGroup group, string formationType, Vector3? centerOverride = null, Vector3? lookDirectionOverride = null)
    {
        string pSide = PlayerPrefs.GetString("PlayerSide", "SideA");
        bool isPlayerGroup = (pSide == "SideA" && factionAGroups.Contains(group)) ||
                             (pSide == "SideB" && factionBGroups.Contains(group));
        // 1. Ustalmy cel (Gdzie ma stać środek formacji)
        Vector3 center = centerOverride.HasValue ? centerOverride.Value : group.groupCenter;

        // 2. --- NOWA LOGIKA ROTACJI (Zawsze frontem do wroga) ---
        Quaternion rotation;

        // A. Jeśli komenda wymusza kierunek (np. Flank Attack), użyj go
        if (lookDirectionOverride.HasValue && lookDirectionOverride.Value != Vector3.zero)
        {
            rotation = Quaternion.LookRotation(lookDirectionOverride.Value);
        }
        // B. W przeciwnym razie: Oblicz, gdzie jest wróg i patrz na niego!
        else
        {
            Vector3 enemyPos = GetSmartLookTarget(group);
            Vector3 dirToEnemy = (enemyPos - center).normalized;
            if (dirToEnemy == Vector3.zero) dirToEnemy = Vector3.forward;
            rotation = Quaternion.LookRotation(dirToEnemy);
        }

        Vector3 finalLookDir = rotation * Vector3.forward; // To jest wektor "Do Wroga"

        // --- OBSŁUGA DEPLOYMENT PHASE ---
        // if (isDeploymentPhase && !isPlayerGroup)
        // {
        //     group.pendingOrder = new PendingOrderData
        //     {
        //         type = PendingOrderType.MoveOrForm,
        //         formationName = formationType,
        //         destination = center,
        //         lookDirection = finalLookDir,
        //         chaseTarget = group.chaseTarget
        //     };
        //     group.chaseTarget = null;
        //     group.isMoving = false;
        //     group.isReforming = false;
        //     // Debug.Log($"[PLANOWANIE AI] Formacja {formationType} zakolejkowana.");
        //     return;
        // }

        if (group.unitMatrices.Count == 0) return;
        group.assignedFeature = null;

        int count = group.unitMatrices.Count;
        group.formationTargets.Clear();
        while (group.formationTargets.Count < count) group.formationTargets.Add(Vector3.zero);

        // Wektory pomocnicze (względem wroga)
        Vector3 forward = finalLookDir; // W stronę wroga
        Vector3 right = rotation * Vector3.right; // W prawo od patrzenia
        Vector3 back = -forward; // W tył od patrzenia

        string typeLower = formationType.ToLower();

        // --- MATEMATYKA FORMACJI ---

        // 1. KLIN (WEDGE) - Ostry koniec do przeciwnika
        if (typeLower.Contains("wedge") || typeLower.Contains("klin"))
        {
            float spacing = 2.0f; // Luźniej dla koni

            // Obliczamy ile jest rzędów, żeby wiedzieć, jak daleko cofnąć "dupę" klina, 
            // a czubek wystawić do przodu.
            // Wzór na sumę ciągu arytmetycznego (1+2+3...) przybliża liczbę rzędów: rows ≈ Sqrt(2*count)
            int estimatedRows = Mathf.CeilToInt(Mathf.Sqrt(2 * count));
            float formationDepth = estimatedRows * spacing;

            // Czubek (Tip) ustawiamy "do przodu" względem środka (center)
            // Dzięki temu 'center' wypada w środku masy oddziału, a czubek jest najbliżej wroga.
            Vector3 tipPosition = center + (forward * (formationDepth * 0.33f));

            int currentRow = 0;
            int unitsInCurrentRow = 0;
            int unitsAllowedInRow = 1; // 1, 2, 3, 4...

            for (int i = 0; i < count; i++)
            {
                // Szerokość aktualnego rzędu
                float rowWidth = (unitsAllowedInRow - 1) * spacing;

                // Pozycja startowa rzędu (lewa strona)
                // "Cofamy się" (back) o numer rzędu
                Vector3 rowStart = tipPosition + (back * currentRow * spacing) - (right * rowWidth * 0.5f);

                Vector3 pos = rowStart + (right * unitsInCurrentRow * spacing);
                SetFormationPoint(group, i, pos);

                unitsInCurrentRow++;
                if (unitsInCurrentRow >= unitsAllowedInRow)
                {
                    currentRow++;
                    unitsInCurrentRow = 0;
                    unitsAllowedInRow++; // Kolejny rząd szerszy
                }
            }
        }
        // 2. CZWOROBOK (SQUARE)
        else if (typeLower.Contains("square") || typeLower.Contains("czworobok"))
        {
            float spacing = 1.5f;
            int sideCount = Mathf.CeilToInt(count / 4.0f);
            float sideLength = sideCount * spacing;

            // Róg lewy-przód (względem wroga)
            Vector3 frontLeftCorner = center + (forward * sideLength * 0.5f) - (right * sideLength * 0.5f);

            for (int i = 0; i < count; i++)
            {
                int sideIndex = (i / sideCount) % 4;
                int unitInSide = i % sideCount;
                Vector3 pos = Vector3.zero;

                switch (sideIndex)
                {
                    case 0: // Front (Frontem do wroga) - wypełniamy w prawo
                        pos = frontLeftCorner + (right * unitInSide * spacing);
                        break;
                    case 1: // Prawy bok - wypełniamy w tył
                        pos = frontLeftCorner + (right * sideLength) + (back * unitInSide * spacing);
                        break;
                    case 2: // Tył - wypełniamy w lewo
                        pos = frontLeftCorner + (right * sideLength) + (back * sideLength) - (right * unitInSide * spacing);
                        break;
                    case 3: // Lewy bok - wypełniamy w przód
                        pos = frontLeftCorner + (back * sideLength) - (back * unitInSide * spacing);
                        break;
                }
                SetFormationPoint(group, i, pos);
            }
        }
        // 3. KOLUMNA
        else if (typeLower.Contains("column") || typeLower.Contains("kolumna"))
        {
            int cols = 5;
            float spacing = 1.5f;
            float totalWidth = (cols - 1) * spacing;

            // Startujemy z przodu, żeby czoło kolumny było bliżej wroga
            int rows = Mathf.CeilToInt((float)count / cols);
            Vector3 frontLineCenter = center + (forward * (rows * spacing * 0.5f));
            Vector3 startPos = frontLineCenter - (right * totalWidth * 0.5f);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                // Budujemy w tył (back)
                Vector3 pos = startPos + (right * col * spacing) + (back * row * spacing);
                SetFormationPoint(group, i, pos);
            }
        }
        // 4. LUŹNA
        else if (typeLower.Contains("loose") || typeLower.Contains("luźna"))
        {
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count) * 2);
            float spacing = 3.5f;
            float totalWidth = (cols - 1) * spacing;
            Vector3 startPos = center - (right * totalWidth * 0.5f);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float jitterX = Random.Range(-0.5f, 0.5f);
                float jitterZ = Random.Range(-0.5f, 0.5f);
                Vector3 pos = startPos + (right * (col + jitterX) * spacing) + (back * (row + jitterZ) * spacing);
                SetFormationPoint(group, i, pos);
            }
        }
        // 5. STANDARD (LINIA)
        else
        {
            // Domyślna "Bezkształtna masa" to 3 szeregi, żeby nie zajmowali całego ekranu
            int targetRows = 3;

            // --- ZMIANA: DEFINICJE SZEREGÓW ---

            if (typeLower.Contains("turtle") || typeLower.Contains("żółw"))
            {
                targetRows = Mathf.CeilToInt(Mathf.Sqrt(count)); // Kwadrat
            }
            else if (typeLower.Contains("double") || typeLower.Contains("dwuszereg"))
            {
                targetRows = 2; // SZTYWNY DWUSZEREG
            }
            // Jeśli nazwa to "Line", "Linia", "Single" lub "Szereg" -> ROBIMY DŁUGI WĄŻ (1 rząd)
            else if (typeLower.Contains("single") || typeLower.Contains("szereg") || typeLower.Contains("line") || typeLower.Contains("linia"))
            {
                targetRows = 1; // SZTYWNY JEDEN SZEREG
            }

            // Obliczamy szerokość (cols) na podstawie sztywnej liczby rzędów
            int cols = Mathf.CeilToInt((float)count / targetRows);

            float spacing = 1.5f;
            if (typeLower.Contains("turtle")) spacing = 1.1f;
            // Dla 1 i 2 szeregów dajemy ciut więcej luzu (1.6f), żeby ładnie wyglądali
            else if (targetRows <= 2) spacing = 1.6f;

            float totalWidth = (cols - 1) * spacing;

            // Startujemy po lewej stronie (patrząc na wroga)
            Vector3 startPos = center - (right * totalWidth * 0.5f);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                // Budujemy w prawo i w tył
                Vector3 pos = startPos + (right * col * spacing) + (back * row * spacing);
                SetFormationPoint(group, i, pos);
            }
        }

        // Finalizacja
        group.targetFacingDirection = finalLookDir; // To sprawia, że po dojściu na miejsce obrócą się do wroga
        group.isReforming = true;
        group.isMoving = true;
        group.currentDestination = center;
    }

    void SetFormationPoint(TacticalGroup group, int index, Vector3 flatPos)
    {
        if (terrain != null)
            flatPos.y = terrain.SampleHeight(flatPos) + terrain.transform.position.y;
        else
            flatPos.y = 0;

        group.formationTargets[index] = flatPos;
    }

    public List<TacticalGroup> GetSelectedGroups()
    {
        // Zwracamy kopię listy, żeby nie uszkodzić oryginału
        return new List<TacticalGroup>(selectedGroups);
    }
}