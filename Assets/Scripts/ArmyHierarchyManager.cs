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

    [Header("Water & Physics Settings")]
    [Tooltip("Ile % prêdkoœci zostaje w wodzie (0.5 = 50% wolniej)")]
    [Range(0.1f, 1.0f)]
    public float waterSpeedPenalty = 0.5f;

    [Tooltip("Tolerancja brzegu. Wy¿sza wartoœæ (np. 0.4) sprawia, ¿e jednostki zwalniaj¹ ju¿ w p³ytkiej wodzie.")]
    [Range(0.01f, 1.0f)]
    public float shoreTolerance = 0.4f;

    [Tooltip("Warstwy terenu do klikania myszk¹ (Ruch)")]
    public LayerMask detectionLayer = 1; // Domyœlnie 'Default'

    [Header("Map Boundaries")]
    public float minX = 0f;
    public float maxX = 2000f;
    public float minZ = 0f;
    public float maxZ = 2000f;

    [Header("Advanced Environment")]
    public string waterTag = "Water";
    public float swimmingDepth = 0.5f;

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

    // Zmienne wodne (obliczane raz)
    private float waterSurfaceY = -1000f;
    private bool waterFound = false;

    void Start()
    {
        terrain = Terrain.activeTerrain;

        if (terrain != null && maxX == 2000f && maxZ == 2000f)
        {
            maxX = terrain.terrainData.size.x;
            maxZ = terrain.terrainData.size.z;
        }

        PrepareIndicatorMeshes();
        DetectWaterLevel(); // Wykrywamy poziom wody na starcie
        Invoke(nameof(InitializeGroups), 0.5f);
    }

    void DetectWaterLevel()
    {
        // Szukamy obiektu wody, aby ustaliæ jej globalny poziom Y
        GameObject waterObj = GameObject.FindGameObjectWithTag(waterTag);
        if (waterObj != null)
        {
            // Zak³adamy, ¿e woda jest p³aska. Pobieramy górn¹ krawêdŸ jej BoxCollidera lub Mesha
            // (Position Y + po³owa skali Y)
            waterSurfaceY = waterObj.transform.position.y + (waterObj.transform.localScale.y * 0.5f);
            waterFound = true;
            Debug.Log($"[ArmyManager] Wykryto wodê na poziomie Y: {waterSurfaceY}");
        }
        else
        {
            Debug.LogWarning("[ArmyManager] Nie znaleziono obiektu z tagiem 'Water'. P³ywanie wy³¹czone.");
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

    // --- ZAKTUALIZOWANA METODA: INDYWIDUALNA DETEKCJA WODY ---
    void ProcessMovement(TacticalGroup group)
    {
        if (group.unitMatrices.Count == 0)
        {
            group.isMoving = false;
            return;
        }

        // --- KONFIGURACJA ---
        float currentOffset = 0f;
        if (group.groupName.Contains("Infantry")) currentOffset = offsetInfantry;
        else if (group.groupName.Contains("Archer")) currentOffset = offsetArcher;
        else if (group.groupName.Contains("Cavalry")) currentOffset = offsetCavalry;

        Vector3 flatCenter = new Vector3(group.groupCenter.x, 0, group.groupCenter.z);
        Vector3 flatTarget = new Vector3(group.currentDestination.x, 0, group.currentDestination.z);

        if (Vector3.Distance(flatCenter, flatTarget) < stopDistance)
        {
            group.isMoving = false;
            return;
        }

        Vector3 direction = (flatTarget - flatCenter).normalized;

        // --- RUCH ---
        float baseSpeed = moveSpeed * group.speedMultiplier * Time.deltaTime;
        Vector3 scaleVec = group.unitMatrices[0].lossyScale;

        // PÊTLA PO WSZYSTKICH JEDNOSTKACH
        // Ka¿dy ¿o³nierz sam sprawdza teren pod sob¹.
        for (int i = 0; i < group.unitMatrices.Count; i++)
        {
            Matrix4x4 mat = group.unitMatrices[i];
            Vector3 currentPos = mat.GetColumn(3);

            // 1. Sprawdzamy wysokoœæ terenu pod nogami tej jednostki
            float myGroundHeight = currentPos.y;
            if (terrain != null)
                myGroundHeight = terrain.SampleHeight(currentPos) + terrain.transform.position.y;

            // 2. Decyzja INDYWIDUALNA: Czy jestem w wodzie?
            // Porównujemy wysokoœæ gruntu z poziomem wody.
            // ShoreTolerance (np. 0.4) pozwala wykryæ p³ytk¹ wodê (gdy grunt jest lekko poni¿ej poziomu kolan).
            bool amIInWater = false;

            if (waterFound)
            {
                if (myGroundHeight < waterSurfaceY + shoreTolerance)
                {
                    amIInWater = true;
                }
            }

            // 3. Aplikujemy prêdkoœæ TYLKO dla tego ¿o³nierza
            float myModifier = amIInWater ? waterSpeedPenalty : 1.0f;
            Vector3 myMoveVector = direction * baseSpeed * myModifier;

            Vector3 newPos = currentPos + myMoveVector;

            // Clamp do granic mapy
            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

            // 4. Ustalanie wysokoœci (Wizualne zanurzenie)
            float finalY;
            float nextTerrainY = myGroundHeight;
            if (terrain != null) nextTerrainY = terrain.SampleHeight(newPos) + terrain.transform.position.y;

            if (amIInWater)
            {
                // W WODZIE -> Zanurzenie wzglêdem tafli wody
                finalY = waterSurfaceY - swimmingDepth;
            }
            else
            {
                // NA L¥DZIE -> Po prostu na terenie
                finalY = nextTerrainY + currentOffset;
            }

            // P³ynne dopasowanie wysokoœci (Lerp)
            newPos.y = Mathf.Lerp(currentPos.y, finalY, Time.deltaTime * 10f);

            // Rotacja
            Quaternion currentRot = mat.rotation;
            Quaternion targetRot = Quaternion.LookRotation(direction);
            Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);

            // Zapisz macierz
            group.unitMatrices[i] = Matrix4x4.TRS(newPos, nextRot, scaleVec);
        }

        group.RebuildBatches();
        group.RecalculateCenter();
    }

    // --- Reszta metod (SelectGroup, Gizmos) bez zmian logicznych ---

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

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0) && selectedGroups.Count > 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, detectionLayer))
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