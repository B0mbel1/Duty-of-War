using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class ArmyHierarchyManager : MonoBehaviour
{
    [Header("References (Auto-detected)")]
    public UnitsSpawnCustom customSpawner;
    public UnitsSpawn historySpawner;

    [Header("UI Integration (Global Counters)")]
    public TextMeshProUGUI counterTextA;
    public TextMeshProUGUI counterTextB;

    [Header("UI Integration (Buttons)")]
    public GameObject groupUIPrefab;
    public Transform uiContainer;

    [Header("Visuals (Selection)")]
    public Material highlightMaterial;

    [Header("Visuals (Direction Indicators)")]
    public Material directionArrowMaterial;

    public Mesh indicatorMeshInfantry;
    public Mesh indicatorMeshArcher;
    public Mesh indicatorMeshCavalry;

    private Mesh cachedInfantryMesh;
    private Mesh cachedArcherMesh;
    private Mesh cachedCavalryMesh;

    [Header("Hierarchy Settings")]
    public HierarchyRank groupSize = HierarchyRank.Company;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 8f;
    public float stopDistance = 2.0f;
    public float cavalrySpeedMultiplier = 1.5f;

    [Header("Water Settings (FIXED)")]
    [Tooltip("Ile % prędkości zostaje w wodzie (0.5 = 50% wolniej)")]
    [Range(0.1f, 1.0f)]
    public float waterSpeedPenalty = 0.5f;

    [Tooltip("Wybierz warstwę, na której jest obiekt wody. Woda MUSI mieć Collider.")]
    public LayerMask waterLayerMask;

    [Tooltip("Głębokość zanurzenia wizualnego.")]
    public float swimmingDepth = 0.5f;

    [Header("Forest Settings (INDIVIDUAL DETECTION)")]
    [Tooltip("Dokładna nazwa warstwy tekstury terenu (w Terrain Layers), która jest lasem.")]
    public string forestLayerName = "Forest";

    [Tooltip("Mnożnik prędkości dla kawalerii w lesie (0.7 = 30% wolniej).")]
    public float forestCavalryPenalty = 0.7f;

    [Tooltip("Mnożnik prędkości dla piechoty w lesie (1.0 = brak kary, 0.9 = lekko wolniej).")]
    public float forestInfantryPenalty = 0.9f;

    // Cache indeksu warstwy lasu (żeby nie szukać po stringu co klatkę)
    private int forestLayerIndex = -1;

    [Header("Slope Physics Settings (Wzgórza)")]
    public float slopeCheckDistance = 1.0f;
    public float lightSlopeAngle = 10f;
    public float steepSlopeAngle = 30f;

    [Header("Slope Speed Multipliers")]
    public float uphillLightMult = 0.5f;
    public float downhillLightMult = 1.2f;
    public float uphillSteepMult = 0.2f;
    public float downhillSteepMult = 0.7f;

    [Header("Map Boundaries")]
    public float minX = 0f;
    public float maxX = 2000f;
    public float minZ = 0f;
    public float maxZ = 2000f;

    [Header("Unit Height Offsets")]
    public float offsetInfantry = 0.0f;
    public float offsetArcher = 0.0f;
    public float offsetCavalry = 0.2f;

    // Listy danych
    public List<TacticalGroup> factionAGroups = new List<TacticalGroup>();
    public List<TacticalGroup> factionBGroups = new List<TacticalGroup>();

    private List<ArmyGroupUI> uiElements = new List<ArmyGroupUI>();
    private List<TacticalGroup> selectedGroups = new List<TacticalGroup>();
    private TacticalGroup lastSelectedGroup = null;
    private Dictionary<TacticalGroup, Material> originalMaterials = new Dictionary<TacticalGroup, Material>();

    private bool groupsInitialized = false;
    private Terrain terrain;
    private TerrainData terrainData; // Cache danych terenu dla optymalizacji

    void Start()
    {
        terrain = Terrain.activeTerrain;

        if (terrain != null)
        {
            terrainData = terrain.terrainData;
            if (maxX == 2000f && maxZ == 2000f)
            {
                maxX = terrainData.size.x;
                maxZ = terrainData.size.z;
            }

            // --- DETEKCJA INDEXU LASU ---
            // Robimy to raz na starcie, żeby wiedzieć który kanał splatmapy to las
            FindForestLayerIndex();
        }

        PrepareIndicatorMeshes();
        // DetectWaterLevel(); <- USUNIĘTE: Stara metoda psuła logikę. Teraz używamy Physics Check.
        Invoke(nameof(InitializeGroups), 0.5f);
    }

    void FindForestLayerIndex()
    {
        if (terrainData == null) return;
        if (string.IsNullOrEmpty(forestLayerName)) return;

        for (int i = 0; i < terrainData.terrainLayers.Length; i++)
        {
            if (terrainData.terrainLayers[i].name == forestLayerName)
            {
                forestLayerIndex = i;
                Debug.Log($"[ArmyManager] Znaleziono warstwę lasu '{forestLayerName}' na indeksie: {forestLayerIndex}");
                return;
            }
        }
        Debug.LogWarning($"[ArmyManager] Nie znaleziono warstwy terenu o nazwie '{forestLayerName}'. Mechanika lasu nieaktywna.");
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
        newMesh.name = sourceMesh.name + "_Modified";
        Vector3[] originalVertices = sourceMesh.vertices;
        Vector3[] newVertices = new Vector3[originalVertices.Length];
        Quaternion rotation = Quaternion.Euler(rotationOffset);
        Matrix4x4 transformMatrix = Matrix4x4.TRS(positionOffset, rotation, scale);
        for (int i = 0; i < originalVertices.Length; i++) newVertices[i] = transformMatrix.MultiplyPoint3x4(originalVertices[i]);
        newMesh.vertices = newVertices;
        newMesh.triangles = sourceMesh.triangles;
        newMesh.uv = sourceMesh.uv;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        return newMesh;
    }

    void InitializeGroups()
    {
        if (customSpawner == null) customSpawner = GetComponent<UnitsSpawnCustom>();
        if (historySpawner == null) historySpawner = GetComponent<UnitsSpawn>();

        bool useCustom = (customSpawner != null && customSpawner.enabled);
        bool useHistory = (historySpawner != null && historySpawner.enabled);

        if (!useCustom && !useHistory) return;

        if (useCustom)
        {
            if (counterTextA == null) counterTextA = customSpawner.factionAText;
            if (counterTextB == null) counterTextB = customSpawner.factionBText;
        }
        else
        {
            if (counterTextA == null) counterTextA = historySpawner.factionAText;
            if (counterTextB == null) counterTextB = historySpawner.factionBText;
        }

        if (uiContainer != null) { for (int i = uiContainer.childCount - 1; i >= 0; i--) Destroy(uiContainer.GetChild(i).gameObject); }
        uiElements.Clear();
        originalMaterials.Clear();

        bool isPlayerA = true;
        bool isPlayerB = false;

        if (!useCustom)
        {
            isPlayerA = (PlayerPrefs.GetString("PlayerSide", "SideA") == "SideA");
            isPlayerB = !isPlayerA;
        }

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

        CreateGroupsFromList(a_Inf, mInf, matA, "A_Infantry", factionAGroups, isPlayerA);
        CreateGroupsFromList(a_Arc, mArc, matA, "A_Archers", factionAGroups, isPlayerA);
        CreateGroupsFromList(a_Cav, mCav, matA, "A_Cavalry", factionAGroups, isPlayerA);
        CreateGroupsFromList(b_Inf, mInf, matB, "B_Infantry", factionBGroups, isPlayerB);
        CreateGroupsFromList(b_Arc, mArc, matB, "B_Archers", factionBGroups, isPlayerB);
        CreateGroupsFromList(b_Cav, mCav, matB, "B_Cavalry", factionBGroups, isPlayerB);

        if (useCustom) customSpawner.RebuildAllBatches(); else historySpawner.RebuildAllBatches();
        groupsInitialized = true;
        UpdateGlobalCounters();
    }

    void CreateGroupsFromList(List<Matrix4x4> sourceList, Mesh mesh, Material mat, string baseName, List<TacticalGroup> targetCollection, bool createUI)
    {
        int sizePerGroup = (int)groupSize;
        int totalUnits = sourceList.Count;
        int groupIndex = 1;
        for (int i = 0; i < totalUnits; i += sizePerGroup)
        {
            int count = Mathf.Min(sizePerGroup, totalUnits - i);
            TacticalGroup newGroup = new TacticalGroup($"{baseName}_{groupIndex}", mesh, mat, groupSize);

            if (baseName.Contains("Archer") || baseName.Contains("Arc")) { newGroup.arrowMesh = cachedArcherMesh != null ? cachedArcherMesh : cachedInfantryMesh; newGroup.speedMultiplier = 1.0f; }
            else if (baseName.Contains("Cavalry") || baseName.Contains("Cav")) { newGroup.arrowMesh = cachedCavalryMesh != null ? cachedCavalryMesh : cachedInfantryMesh; newGroup.speedMultiplier = cavalrySpeedMultiplier; }
            else { newGroup.arrowMesh = cachedInfantryMesh; newGroup.speedMultiplier = 1.0f; }
            newGroup.arrowMaterial = directionArrowMaterial;

            if (!originalMaterials.ContainsKey(newGroup)) originalMaterials.Add(newGroup, mat);

            List<Matrix4x4> slice = sourceList.GetRange(i, count);
            newGroup.unitMatrices = slice;
            newGroup.RebuildBatches();
            newGroup.RecalculateCenter();
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
        if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
        if (Input.GetKeyDown(KeyCode.B)) KillRandomUnit(false);

        foreach (var group in factionAGroups) { if (group.isMoving) ProcessMovement(group); group.RenderGroup(); group.RenderIndicators(); }
        foreach (var group in factionBGroups) { if (group.isMoving) ProcessMovement(group); group.RenderGroup(); group.RenderIndicators(); }
        HandleMouseInput();
    }

    // --- ZMODYFIKOWANA FIZYKA: INDYWIDUALNA DETEKCJA WODY + LASU ---
    void ProcessMovement(TacticalGroup group)
    {
        if (group.unitMatrices.Count == 0)
        {
            group.isMoving = false;
            return;
        }

        bool isCavalry = group.groupName.Contains("Cavalry") || group.groupName.Contains("Cav");

        float currentOffset = 0f;
        if (group.groupName.Contains("Infantry")) currentOffset = offsetInfantry;
        else if (group.groupName.Contains("Archer")) currentOffset = offsetArcher;
        else if (isCavalry) currentOffset = offsetCavalry;

        Vector3 flatCenter = new Vector3(group.groupCenter.x, 0, group.groupCenter.z);
        Vector3 flatTarget = new Vector3(group.currentDestination.x, 0, group.currentDestination.z);

        if (Vector3.Distance(flatCenter, flatTarget) < stopDistance)
        {
            group.isMoving = false;
            return;
        }

        Vector3 direction = (flatTarget - flatCenter).normalized;
        float groupBaseSpeed = moveSpeed * group.speedMultiplier * Time.deltaTime;
        Vector3 scaleVec = group.unitMatrices[0].lossyScale;

        // Cache danych terenu
        int alphaWidth = 0, alphaHeight = 0;
        if (terrainData != null)
        {
            alphaWidth = terrainData.alphamapWidth;
            alphaHeight = terrainData.alphamapHeight;
        }

        for (int i = 0; i < group.unitMatrices.Count; i++)
        {
            Matrix4x4 mat = group.unitMatrices[i];
            Vector3 currentPos = mat.GetColumn(3);

            // 1. Gdzie jest ziemia?
            float myGroundHeight = currentPos.y;
            if (terrain != null)
                myGroundHeight = terrain.SampleHeight(currentPos) + terrain.transform.position.y;

            // --- FIZYKA WODY (METODA RAYCAST "Z NIEBA") ---
            bool amIInWater = false;
            float waterSurfaceHeight = -1000f;

            // Strzelamy promieniem z góry (np. Y+100) pionowo w dół, szukając TYLKO warstwy wody
            RaycastHit waterHit;
            Vector3 skyPos = new Vector3(currentPos.x, myGroundHeight + 50f, currentPos.z);

            // Raycast ograniczony tylko do maski wody (waterLayerMask)
            if (Physics.Raycast(skyPos, Vector3.down, out waterHit, 100f, waterLayerMask))
            {
                waterSurfaceHeight = waterHit.point.y;

                // Logika: Jeśli poziom wody jest wyższy niż poziom gruntu (+ mały margines), to jesteśmy w wodzie.
                // To eliminuje problem "wody pod ziemią" (gdyby woda była na całym levelu pod spodem).
                if (waterSurfaceHeight > myGroundHeight + 0.1f)
                {
                    amIInWater = true;
                }
            }

            // --- FIZYKA LASU ---
            bool amIInForest = false;
            if (terrain != null && forestLayerIndex != -1 && !amIInWater)
            {
                int mapX = Mathf.RoundToInt(((currentPos.x - terrain.transform.position.x) / terrainData.size.x) * alphaWidth);
                int mapZ = Mathf.RoundToInt(((currentPos.z - terrain.transform.position.z) / terrainData.size.z) * alphaHeight);

                if (mapX >= 0 && mapX < alphaWidth && mapZ >= 0 && mapZ < alphaHeight)
                {
                    float[,,] splatMap = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
                    if (splatMap[0, 0, forestLayerIndex] > 0.5f) amIInForest = true;
                }
            }

            // 3. Modyfikatory prędkości
            float finalSpeedModifier = 1.0f;

            if (amIInWater)
            {
                finalSpeedModifier = waterSpeedPenalty;
            }
            else
            {
                if (amIInForest)
                {
                    finalSpeedModifier *= isCavalry ? forestCavalryPenalty : forestInfantryPenalty;
                }

                if (terrain != null)
                {
                    Vector3 probePos = currentPos + (direction * slopeCheckDistance);
                    float nextHeightProbe = terrain.SampleHeight(probePos) + terrain.transform.position.y;
                    float angle = Mathf.Atan2(nextHeightProbe - myGroundHeight, slopeCheckDistance) * Mathf.Rad2Deg;

                    if (angle > steepSlopeAngle) finalSpeedModifier *= uphillSteepMult;
                    else if (angle > lightSlopeAngle) finalSpeedModifier *= uphillLightMult;
                    else if (angle < -steepSlopeAngle) finalSpeedModifier *= downhillSteepMult;
                    else if (angle < -lightSlopeAngle) finalSpeedModifier *= downhillLightMult;
                }
            }

            // 4. Ruch
            Vector3 myMoveVector = direction * groupBaseSpeed * finalSpeedModifier;
            Vector3 newPos = currentPos + myMoveVector;

            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

            // 5. Ustalanie wysokości Y (Wizualne)
            float finalY;
            float nextTerrainY = myGroundHeight;
            if (terrain != null) nextTerrainY = terrain.SampleHeight(newPos) + terrain.transform.position.y;

            if (amIInWater)
            {
                // Jeśli wchodzimy głębiej, używamy wykrytej powierzchni wody
                finalY = waterSurfaceHeight - swimmingDepth;
            }
            else
            {
                finalY = nextTerrainY + currentOffset;
            }

            newPos.y = Mathf.Lerp(currentPos.y, finalY, Time.deltaTime * 10f);

            Quaternion currentRot = mat.rotation;
            Quaternion targetRot = Quaternion.LookRotation(direction);
            Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);

            group.unitMatrices[i] = Matrix4x4.TRS(newPos, nextRot, scaleVec);
        }

        group.RebuildBatches();
        group.RecalculateCenter();
    }
    // --- Reszta metod bez zmian ---
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || factionAGroups.Count == 0) return;

        // Rysuje linię nad pierwszą jednostką pierwszej grupy, żebyś widział detekcję
        var group = factionAGroups[0];
        if (group.unitMatrices.Count > 0)
        {
            Vector3 pos = (Vector3)group.unitMatrices[0].GetColumn(3);
            Vector3 skyPos = pos + Vector3.up * 50f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(skyPos, pos);
            Gizmos.DrawSphere(skyPos, 1.0f);
        }
    }
    void KillRandomUnit(bool isFactionA)
    {
        List<TacticalGroup> targetFaction = isFactionA ? factionAGroups : factionBGroups;
        var aliveGroups = new List<TacticalGroup>();
        foreach (var g in targetFaction) if (g.unitMatrices.Count > 0) aliveGroups.Add(g);
        if (aliveGroups.Count == 0) return;
        TacticalGroup randomGroup = aliveGroups[Random.Range(0, aliveGroups.Count)];
        randomGroup.unitMatrices.RemoveAt(randomGroup.unitMatrices.Count - 1);
        randomGroup.RebuildBatches();
        foreach (var ui in uiElements) if (ui.GetGroup() == randomGroup) { ui.UpdateUnitCount(); break; }
        UpdateGlobalCounters();
    }

    void UpdateGlobalCounters()
    {
        int totalA = 0; foreach (var g in factionAGroups) totalA += g.unitMatrices.Count;
        int totalB = 0; foreach (var g in factionBGroups) totalB += g.unitMatrices.Count;
        if (counterTextA != null) counterTextA.text = totalA.ToString();
        if (counterTextB != null) counterTextB.text = totalB.ToString();
    }

    void SetGroupHighlight(TacticalGroup group, bool highlight)
    {
        if (group == null) return;
        if (highlight && highlightMaterial != null) group.unitMaterial = highlightMaterial;
        else if (originalMaterials.ContainsKey(group)) group.unitMaterial = originalMaterials[group];
    }

    public void SelectGroup(TacticalGroup group)
    {
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
            int min = Mathf.Min(indexStart, indexEnd), max = Mathf.Max(indexStart, indexEnd);
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

    [Header("Selection Raycast")]
    public LayerMask terrainLayerMask = 1; // Domyślnie Default

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0) && selectedGroups.Count > 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, terrainLayerMask))
            {
                foreach (var group in selectedGroups) IssueMoveOrder(group, hit.point);
            }
        }
    }

    void IssueMoveOrder(TacticalGroup group, Vector3 destination)
    {
        float clampedX = Mathf.Clamp(destination.x, minX, maxX);
        float clampedZ = Mathf.Clamp(destination.z, minZ, maxZ);
        group.currentDestination = new Vector3(clampedX, destination.y, clampedZ);
        group.isMoving = true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 center = new Vector3((minX + maxX) / 2, 50, (minZ + maxZ) / 2);
        Vector3 size = new Vector3(maxX - minX, 100, maxZ - minZ);
        Gizmos.DrawWireCube(center, size);
    }
}