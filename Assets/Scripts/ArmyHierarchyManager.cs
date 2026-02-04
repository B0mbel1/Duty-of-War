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
    public Material directionArrowMaterial;
    public Mesh indicatorMeshInfantry;
    public Mesh indicatorMeshArcher;
    public Mesh indicatorMeshCavalry;

    private Mesh cachedInfantryMesh;
    private Mesh cachedArcherMesh;
    private Mesh cachedCavalryMesh;

    [Header("Hierarchy & Speed")]
    public HierarchyRank groupSize = HierarchyRank.Company;
    public float moveSpeed = 10f;
    public float rotationSpeed = 8f;
    public float stopDistance = 2.0f;
    public float cavalrySpeedMultiplier = 1.5f;

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

    // --- DANE ---
    public List<TacticalGroup> factionAGroups = new List<TacticalGroup>();
    public List<TacticalGroup> factionBGroups = new List<TacticalGroup>();

    private List<ArmyGroupUI> uiElements = new List<ArmyGroupUI>();
    private List<TacticalGroup> selectedGroups = new List<TacticalGroup>();
    private TacticalGroup lastSelectedGroup = null;

    private bool groupsInitialized = false;
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
        // Sprawdzamy, czy kursor jest w polu tekstowym (Input Field)
        bool isTyping = false;
        if (EventSystem.current != null)
        {
            GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
            if (selectedObj != null && selectedObj.GetComponent<TMP_InputField>() != null)
            {
                isTyping = true;
            }
        }

        // --- SKRÓTY KLAWISZOWE (Działają tylko gdy NIE piszemy) ---
        if (!isTyping)
        {
            if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
            if (Input.GetKeyDown(KeyCode.B)) KillRandomUnit(false);
            if (Input.GetKeyDown(KeyCode.Space)) ToggleTacticalPause();

            // Tutaj jest poprawka dla klawisza 'M':
            if (Input.GetKeyDown(KeyCode.M))
            {
                // Jeśli panel jest otwarty -> ZAMKNIJ GO
                if (namingPanel != null && namingPanel.activeSelf)
                {
                    namingPanel.SetActive(false);
                }
                // Jeśli zamknięty i mamy zaznaczone > 1 grupy -> OTWÓRZ GO
                else if (selectedGroups.Count > 1)
                {
                    OpenNamingPanel();
                }
            }
        }

        // --- LOGIKA GRY (Ruch i Walka działają zawsze) ---

        // USUNIĘTO OBSŁUGĘ MYSZKI DLA ROZKAZÓW (HandleMouseInput) - zgodnie z poprzednią prośbą

        bool isGameFrozen = isDeploymentPhase || isTacticalPause;

        foreach (var group in factionAGroups)
        {
            if (group.unitMatrices.Count > 0)
            {
                if (!isGameFrozen && group.isMoving) ProcessMovement(group);
                group.RenderGroup();
                group.RenderIndicators();
            }
        }

        foreach (var group in factionBGroups)
        {
            if (group.unitMatrices.Count > 0)
            {
                if (!isGameFrozen && group.isMoving) ProcessMovement(group);
                group.RenderGroup();
                group.RenderIndicators();
            }
        }

        if (!isGameFrozen) ResolveCombatRoundRobin();
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

        float distToTargetSq = Vector3.SqrMagnitude(flatTarget - flatCenter);

        if (!group.isReforming && distToTargetSq < 0.5f)
        {
            group.isMoving = false;
            return;
        }
        float globalBaseSpeed = moveSpeed * Time.deltaTime;
        int unitsReachedTarget = 0;

        Vector3 groupDirection = (flatTarget - flatCenter).normalized;
        Quaternion targetRot = Quaternion.LookRotation(groupDirection != Vector3.zero ? groupDirection : Vector3.forward);

        if (group.isReforming && group.unitMatrices.Count > 0)
            targetRot = group.unitMatrices[0].rotation;

        Quaternion currentRot = group.unitMatrices.Count > 0 ? group.unitMatrices[0].rotation : Quaternion.identity;
        Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);


        int alphaWidth = 0, alphaHeight = 0;
        if (terrainData != null) { alphaWidth = terrainData.alphamapWidth; alphaHeight = terrainData.alphamapHeight; }

        for (int i = 0; i < group.unitMatrices.Count; i++)
        {
            Matrix4x4 mat = group.unitMatrices[i];
            Vector3 currentPos = mat.GetColumn(3);
            Vector3 scaleVec = mat.lossyScale;
            UnitArchetype arch = group.GetUnitArchetype(i);

            Vector3 moveDirection;
            float distToSlot = 0f;

            if (group.isReforming)
            {
                // Jeśli formujemy, idziemy do SWOJEGO celu
                if (i < group.formationTargets.Count)
                {
                    Vector3 targetPos = group.formationTargets[i];
                    Vector3 diff = targetPos - currentPos;
                    // Ignorujemy oś Y dla kierunku ruchu
                    Vector3 flatDiff = new Vector3(diff.x, 0, diff.z);
                    distToSlot = flatDiff.magnitude;

                    if (distToSlot < 0.2f)
                    {
                        moveDirection = Vector3.zero; // Jesteś na miejscu
                        unitsReachedTarget++;
                    }
                    else
                    {
                        moveDirection = flatDiff.normalized;
                    }
                }
                else
                {
                    moveDirection = Vector3.zero; // Fallback
                    unitsReachedTarget++;
                }
            }
            else
            {
                // Zwykły ruch grupy
                moveDirection = groupDirection;
            }
            if (moveDirection == Vector3.zero) continue;


            float mySpeedMultiplier = (arch != null) ? arch.speedMultiplier : 1.0f;
            float myBaseSpeed = globalBaseSpeed * mySpeedMultiplier;

            // --- Reszta fizyki terenu bez zmian ---
            float terrainY = currentPos.y;
            if (terrain != null) terrainY = terrain.SampleHeight(currentPos) + terrain.transform.position.y;
            bool inWater = false;
            float waterHeight = -1000f;

            RaycastHit hit;
            if (Physics.Raycast(currentPos + Vector3.up * 50f, Vector3.down, out hit, 100f, waterLayerMask))
            {
                if (hit.point.y > terrainY - 0.1f) { inWater = true; waterHeight = hit.point.y; }
            }

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

            float forestMod = 1.0f;
            if (forestLayerIndex != -1 && !inWater && terrainData != null)
            {
                int mapX = Mathf.RoundToInt(((currentPos.x - terrain.transform.position.x) / terrainData.size.x) * alphaWidth);
                int mapZ = Mathf.RoundToInt(((currentPos.z - terrain.transform.position.z) / terrainData.size.z) * alphaHeight);

                if (mapX >= 0 && mapX < alphaWidth && mapZ >= 0 && mapZ < alphaHeight)
                {
                    float[,,] splat = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
                    if (splat[0, 0, forestLayerIndex] > 0.5f)
                    {
                        if (arch != null)
                            forestMod = (arch.type == UnitType.Cavalry) ? forestCavalryPenalty : forestInfantryPenalty;
                    }
                }
            }

            float finalMod = slopeMod * forestMod;
            if (inWater) finalMod *= waterSpeedPenalty;

            Vector3 newPos = currentPos + (moveDirection * myBaseSpeed * finalMod);

            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

            if (inWater) newPos.y = waterHeight - swimmingDepth;
            else if (terrain != null) newPos.y = terrain.SampleHeight(newPos) + terrain.transform.position.y + 0.5f;
            else newPos.y = terrainY;

            Quaternion unitRot = nextRot;
            if (group.isReforming && moveDirection.sqrMagnitude > 0.1f)
                unitRot = Quaternion.Lerp(mat.rotation, Quaternion.LookRotation(moveDirection), 10f * Time.deltaTime);
            else
            {
                if (group.targetFacingDirection != Vector3.zero)
                {
                    Quaternion idealRot = Quaternion.LookRotation(group.targetFacingDirection);
                    unitRot = Quaternion.Slerp(mat.rotation, idealRot, 5f * Time.deltaTime);
                }
            }

            group.unitMatrices[i] = Matrix4x4.TRS(newPos, unitRot, scaleVec);
        }
        if (group.isReforming && unitsReachedTarget >= group.unitMatrices.Count)
        {
            group.isReforming = false;
            group.isMoving = false;
            // Wyrównaj idealnie rotację na koniec
            ApplyFinalFormationRotation(group);
        }
        group.UpdateBatchData();
        group.RecalculateCenter();
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
    public void MoveGroupToLineFeature(TacticalGroup group, TerrainFeature feature)
    {
        if (group.unitMatrices.Count == 0) return;

        // 1. Przypisujemy grupę do terenu (ważne dla kolejkowania!)
        group.assignedFeature = feature;

        Vector3 center = feature.transform.position;
        Vector3 lineDir = feature.lineOrientation.normalized;
        float lineLen = feature.lineLength;

        // Obliczamy normalną (prostopadłą) do linii
        Vector3 riverNormal = Vector3.Cross(Vector3.up, lineDir).normalized;

        // --- KIERUNEK PATRZENIA (FaceDir) ---
        // Domyślnie prostopadle do linii
        Vector3 faceDir = riverNormal;

        // Jeśli to rzeka, patrzymy W STRONĘ WODY
        if (feature.isRiver)
        {
            // Sprawdzamy po której stronie rzeki jesteśmy
            Vector3 toGroup = group.groupCenter - center;
            float side = Vector3.Dot(toGroup, riverNormal);

            // Jeśli side > 0, woda jest "za nami" względem normalnej, więc odwracamy normalną żeby patrzeć na wodę
            // Albo prościej: Patrzymy od naszej pozycji w stronę środka rzeki
            Vector3 toWater = (feature.transform.position - center).normalized; // Wektor na rzekę

            // Poprawka: Czasami środek grupy jest daleko, więc używamy wektora prostopadłego
            // Ustalmy faceDir tak, by celował w rzekę, ale był idealnie prostopadły do linii
            if (Vector3.Dot(toWater, riverNormal) > 0) faceDir = riverNormal;
            else faceDir = -riverNormal;

            // Ustawiamy "bazę" na brzegu (z uwzględnieniem offsetu)
            Vector3 offsetVector = -faceDir; // Cofamy się od wody w głąb lądu
            center = feature.transform.position + (offsetVector * feature.riverBankOffset);
        }

        // --- NOWOŚĆ: OBLICZANIE "GŁĘBOKOŚCI" (Rank Depth) ---
        // Sprawdzamy, który to jest szereg oddziałów
        int myRankIndex = 0;
        List<TacticalGroup> myFaction = (factionAGroups.Contains(group)) ? factionAGroups : factionBGroups;

        foreach (var other in myFaction)
        {
            if (other == group) break; // Liczymy tylko tych PRZED nami na liście (żeby uniknąć sytuacji, że oba oddziały ustępują sobie nawzajem)

            // Jeśli inny oddział też jest przypisany do tej samej rzeki/lasu
            if (other.assignedFeature == feature)
            {
                myRankIndex++;
            }
        }

        // Obliczamy ile miejsca zajmuje jeden oddział (głębokość)
        // Zakładamy średnio 10-15 metrów na oddział + odstęp
        float groupDepthSpacing = 12.0f; // Możesz zwiększyć, jeśli oddziały są duże

        // Cofamy centrum o (index * głębokość) w stronę PRZECIWNĄ do patrzenia
        // Czyli stajemy "za plecami" pierwszego oddziału
        center -= faceDir * (myRankIndex * groupDepthSpacing);

        // Debug info
        if (myRankIndex > 0) Debug.Log($"Grupa {group.groupName} staje w {myRankIndex}. linii obrony (za innymi).");

        // ----------------------------------------------------

        // Zapisujemy kierunek patrzenia
        group.targetFacingDirection = faceDir;

        // Reszta logiki rozstawiania (Szeregi wewnątrz oddziału)
        int unitCount = group.unitMatrices.Count;
        float spacing = 1.5f;
        int maxUnitsPerRow = Mathf.FloorToInt(lineLen / spacing);
        if (maxUnitsPerRow < 1) maxUnitsPerRow = 1;
        float currentLineWidth = Mathf.Min(unitCount, maxUnitsPerRow) * spacing;
        Vector3 lineStart = center - (lineDir * currentLineWidth * 0.5f);

        // Rozszerzamy listę celów jeśli trzeba
        while (group.formationTargets.Count < unitCount) group.formationTargets.Add(Vector3.zero);

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / maxUnitsPerRow;
            int col = i % maxUnitsPerRow;

            // faceDir * row * spacing * -1 -> kolejne rzędy żołnierzy stają za plecami pierwszego
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
        if (idx >= attackerGroup.attackCooldowns.Count || idx >= attackerGroup.unitMatrices.Count) return;

        float dt = Time.deltaTime;
        if (attackerGroup.attackCooldowns[idx] > 0) { attackerGroup.attackCooldowns[idx] -= dt; return; }

        Matrix4x4 myMat = attackerGroup.unitMatrices[idx];
        Vector3 myPos = (Vector3)myMat.GetColumn(3);
        Vector3 myForward = (Vector3)myMat.GetColumn(2);

        UnitArchetype attackerStats = attackerGroup.GetUnitArchetype(idx);
        if (attackerStats == null) return;

        bool isArcher = attackerStats.type == UnitType.Archer;
        float range = isArcher ? attackRangeArcher : attackRangeMelee;
        float rangeSq = range * range;
        float cooldownTime = isArcher ? archerAttackCooldown : attackCooldown;

        UnitRef target = new UnitRef { group = null };
        if (isArcher) target = FindTargetGlobal(myPos, range, rangeSq, enemyFaction);
        else target = FindTargetInGrid(myPos, rangeSq);

        if (target.group != null)
        {
            if (target.index >= target.group.unitMatrices.Count) return;

            Vector3 dirToTarget = (target.position - myPos).normalized;
            float angleDot = Vector3.Dot(myForward, dirToTarget);
            if (angleDot < 0.2f) return;

            Matrix4x4 targetMat = target.group.unitMatrices[target.index];
            Vector3 targetForward = (Vector3)targetMat.GetColumn(2);
            float orientationDot = Vector3.Dot(myForward, targetForward);
            bool isBackstab = (orientationDot > 0.5f);

            float dmg = 0f;
            if (isBackstab) dmg = 10000f;
            else dmg = CalculateDamage(attackerGroup, target.group, idx, target.index, myPos, target.position);

            target.group.unitHealth[target.index] -= dmg;
            attackerGroup.attackCooldowns[idx] = cooldownTime * Random.Range(0.8f, 1.2f);

            if (target.group.unitHealth[target.index] <= 0) KillUnit(target.group, target.index);
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
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                int key = ((cx + x) * 73856093) ^ ((cz + z) * 19349663);
                if (spatialGrid.TryGetValue(key, out List<UnitRef> list))
                {
                    foreach (var u in list)
                        if ((u.position - pos).sqrMagnitude < rangeSq) return u;
                }
            }
        }
        return new UnitRef { group = null };
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

    void SetGroupHighlight(TacticalGroup group, bool highlight)
    {
        if (group == null || group.unitMatrices.Count == 0) return;

        foreach (var arch in group.archetypes)
        {
            if (highlight && highlightMaterial != null)
                arch.unitMaterial = highlightMaterial;
            else
                arch.unitMaterial = arch.originalMaterial;
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
        Gizmos.color = Color.red;
        Vector3 center = new Vector3((minX + maxX) / 2, 50, (minZ + maxZ) / 2);
        Vector3 size = new Vector3(maxX - minX, 100, maxZ - minZ);
        Gizmos.DrawWireCube(center, size);

        if (factionAGroups.Count > 0 && factionAGroups[0].unitMatrices.Count > 0)
        {
            Matrix4x4 mat = factionAGroups[0].unitMatrices[0];
            Vector3 pos = (Vector3)mat.GetColumn(3);
            Vector3 dir = (factionAGroups[0].currentDestination - factionAGroups[0].groupCenter).normalized;
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pos, pos + dir * slopeCheckDistance);
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
        Debug.Log("Faza rozstawiania zakończona. Bitwa rozpoczęta!");
    }

    void ToggleTacticalPause()
    {
        if (isDeploymentPhase || isBattleOver) return;
        isTacticalPause = !isTacticalPause;
        if (tacticalPauseIndicator != null) tacticalPauseIndicator.SetActive(isTacticalPause);
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
    public void ApplyFormation(TacticalGroup group, string formationType)
    {
        if (group.unitMatrices.Count == 0) return;

        while (group.formationTargets.Count < group.unitMatrices.Count)
            group.formationTargets.Add(group.groupCenter);
        while (group.formationTargets.Count > group.unitMatrices.Count)
            group.formationTargets.RemoveAt(group.formationTargets.Count - 1);

        int count = group.unitMatrices.Count;
        float spacing = 1.5f;
        int cols = 0;

        switch (formationType.ToLower())
        {
            case "line": cols = count; spacing = 1.2f; break;
            case "double_line": cols = Mathf.CeilToInt(count / 2.0f); spacing = 1.3f; break;
            case "box":
            case "turtle": cols = Mathf.CeilToInt(Mathf.Sqrt(count)); spacing = 1.0f; break;
            case "loose": cols = Mathf.CeilToInt(Mathf.Sqrt(count)); spacing = 3.0f; break;
            default: cols = Mathf.CeilToInt(Mathf.Sqrt(count)); break;
        }

        Vector3 center = group.groupCenter;

        Quaternion rotation = group.unitMatrices[0].rotation;
        Vector3 right = rotation * Vector3.right;
        Vector3 back = rotation * Vector3.back;

        float totalWidth = (cols - 1) * spacing;
        Vector3 startPos = center - (right * totalWidth * 0.5f);

        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            Vector3 offset = (right * col * spacing) + (back * row * spacing);
            Vector3 finalPos = startPos + offset;

            // Dopasowanie do terenu (dla celu)
            if (terrain != null)
                finalPos.y = terrain.SampleHeight(finalPos) + terrain.transform.position.y;
            else
                finalPos.y = group.unitMatrices[i].GetColumn(3).y; // fallback

            group.formationTargets[i] = finalPos;
        }

        group.isReforming = true;
        group.isMoving = true;
        group.currentDestination = group.groupCenter;
        Debug.Log($"Grupa {group.groupName} rozpoczyna formowanie: {formationType}");
        // group.UpdateBatchData();
        // group.RecalculateCenter();
        // group.currentDestination = group.groupCenter;
        // group.isMoving = false;
    }

    public List<TacticalGroup> GetSelectedGroups()
    {
        // Zwracamy kopię listy, żeby nie uszkodzić oryginału
        return new List<TacticalGroup>(selectedGroups);
    }
}