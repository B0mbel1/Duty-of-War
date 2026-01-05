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
    public Material highlightMaterial; // <-- Materia³ podœwietlenia zaznaczenia (np. jasny zielony)

    [Header("Visuals (Direction Indicators)")]
    public Material directionArrowMaterial; // <-- Wspólny materia³ wskaŸników (np. bia³y transparentny)

    // Trzy ró¿ne meshe bazowe (przypisz tu zwyk³e Cylindry lub inne kszta³ty w Unity)
    public Mesh indicatorMeshInfantry;
    public Mesh indicatorMeshArcher;
    public Mesh indicatorMeshCavalry;

    // Zmienne przechowuj¹ce przeliczone (przesuniête) meshe
    private Mesh cachedInfantryMesh;
    private Mesh cachedArcherMesh;
    private Mesh cachedCavalryMesh;

    [Header("Hierarchy Settings")]
    public HierarchyRank groupSize = HierarchyRank.Company;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 8f;
    public float stopDistance = 2.0f;
    public LayerMask groundLayer = 1;
    public float cavalrySpeedMultiplier = 1.5f;

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

    // Listy zaznaczenia
    private List<TacticalGroup> selectedGroups = new List<TacticalGroup>();
    private TacticalGroup lastSelectedGroup = null;

    // Pamiêæ oryginalnych materia³ów
    private Dictionary<TacticalGroup, Material> originalMaterials = new Dictionary<TacticalGroup, Material>();

    private bool groupsInitialized = false;
    private Terrain terrain;

    // Zmienne do wody
    private Collider waterCollider;
    private float waterSurfaceY;
    private bool waterFound = false;

    void Start()
    {
        terrain = Terrain.activeTerrain;

        if (terrain != null && maxX == 2000f && maxZ == 2000f)
        {
            maxX = terrain.terrainData.size.x;
            maxZ = terrain.terrainData.size.z;
        }

        // 1. Przygotowanie zmodyfikowanych meshy dla wskaŸników
        PrepareIndicatorMeshes();

        // 2. Wykrycie wody
        DetectWater();

        // 3. Inicjalizacja grup z opóŸnieniem (¿eby spawner zd¹¿y³ zadzia³aæ)
        Invoke(nameof(InitializeGroups), 0.5f);
    }

    // --- NOWA METODA PRZYGOTOWUJ¥CA MESHE ---
    void PrepareIndicatorMeshes()
    {
        // 1. PIECHOTA: Le¿¹cy cylinder (strza³ka/linia)
        // Offset Z: 1.5m przed jednostk¹, Y: 0.1m nad ziemi¹
        cachedInfantryMesh = CreateOffsetMesh(
            indicatorMeshInfantry,
            new Vector3(0, 0.1f, 0.5f),
            new Vector3(90, 0, 0),         // Le¿y na p³asko
            new Vector3(0.2f, 1.0f, 0.2f)  // Cienki i d³ugi
        );

        // 2. £UCZNICY: P³aski dysk (ko³o) - symbolizuje zasiêg/obszar
        // Offset Z: 1.5m przed jednostk¹
        cachedArcherMesh = CreateOffsetMesh(
            indicatorMeshArcher,
            new Vector3(0, 0.1f, 0.5f),
            new Vector3(-90, 90, 0),           // Stoi pionowo (dla cylindra to standard)
            new Vector3(1.0f, 0.02f, 1.0f)  // Bardzo niski (plasterek) i szeroki
        );

        // 3. KAWALERIA: D³u¿sza i grubsza linia
        // Offset Z: 2.0m (koñ jest d³u¿szy)
        cachedCavalryMesh = CreateOffsetMesh(
            indicatorMeshCavalry,
            new Vector3(0, 0.1f, 0.5f),
            new Vector3(90, 0, 0),
            new Vector3(0.2f, 1.0f, 0.2f)
        );
    }

    // Funkcja pomocnicza modyfikuj¹ca wierzcho³ki Mesha
    Mesh CreateOffsetMesh(Mesh sourceMesh, Vector3 positionOffset, Vector3 rotationOffset, Vector3 scale)
    {
        if (sourceMesh == null) return null;

        Mesh newMesh = new Mesh();
        newMesh.name = sourceMesh.name + "_Modified";

        Vector3[] originalVertices = sourceMesh.vertices;
        Vector3[] newVertices = new Vector3[originalVertices.Length];

        Quaternion rotation = Quaternion.Euler(rotationOffset);
        Matrix4x4 transformMatrix = Matrix4x4.TRS(positionOffset, rotation, scale);

        for (int i = 0; i < originalVertices.Length; i++)
        {
            newVertices[i] = transformMatrix.MultiplyPoint3x4(originalVertices[i]);
        }

        newMesh.vertices = newVertices;
        newMesh.triangles = sourceMesh.triangles;
        newMesh.uv = sourceMesh.uv;

        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }
    // ----------------------------------------

    void DetectWater()
    {
        GameObject waterObj = GameObject.FindGameObjectWithTag(waterTag);
        if (waterObj != null)
        {
            waterCollider = waterObj.GetComponent<Collider>();
            if (waterCollider != null)
            {
                waterSurfaceY = waterObj.transform.position.y + (waterObj.transform.localScale.y * 0.5f);
                waterFound = true;
            }
        }
    }

    void InitializeGroups()
    {
        if (customSpawner == null) customSpawner = GetComponent<UnitsSpawnCustom>();
        if (historySpawner == null) historySpawner = GetComponent<UnitsSpawn>();

        bool useCustom = (customSpawner != null && customSpawner.enabled);
        bool useHistory = (historySpawner != null && historySpawner.enabled);

        if (!useCustom && !useHistory)
        {
            Debug.LogError("ArmyHierarchyManager: Nie znaleziono aktywnego Spawnera!");
            return;
        }

        if (useCustom)
        {
            if (counterTextA == null) counterTextA = customSpawner.factionAText;
            if (counterTextB == null) counterTextB = customSpawner.factionBText;
        }
        else if (useHistory)
        {
            if (counterTextA == null) counterTextA = historySpawner.factionAText;
            if (counterTextB == null) counterTextB = historySpawner.factionBText;
        }

        if (uiContainer != null)
        {
            for (int i = uiContainer.childCount - 1; i >= 0; i--)
                Destroy(uiContainer.GetChild(i).gameObject);
        }

        uiElements.Clear();
        originalMaterials.Clear();

        bool isPlayerA = true;
        bool isPlayerB = false;

        if (!useCustom)
        {
            string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
            isPlayerA = (playerSide == "SideA");
            isPlayerB = !isPlayerA;
        }

        List<Matrix4x4> a_Inf, a_Arc, a_Cav;
        List<Matrix4x4> b_Inf, b_Arc, b_Cav;
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

        // Tworzenie grup z odpowiednimi nazwami, które potem pos³u¿¹ do doboru wskaŸnika
        CreateGroupsFromList(a_Inf, mInf, matA, "A_Infantry", factionAGroups, isPlayerA);
        CreateGroupsFromList(a_Arc, mArc, matA, "A_Archers", factionAGroups, isPlayerA);
        CreateGroupsFromList(a_Cav, mCav, matA, "A_Cavalry", factionAGroups, isPlayerA);

        CreateGroupsFromList(b_Inf, mInf, matB, "B_Infantry", factionBGroups, isPlayerB);
        CreateGroupsFromList(b_Arc, mArc, matB, "B_Archers", factionBGroups, isPlayerB);
        CreateGroupsFromList(b_Cav, mCav, matB, "B_Cavalry", factionBGroups, isPlayerB);

        // Czyszczenie list w spawnerze
        if (useCustom)
        {
            customSpawner.listA_Inf.Clear(); customSpawner.listA_Arc.Clear(); customSpawner.listA_Cav.Clear();
            customSpawner.listB_Inf.Clear(); customSpawner.listB_Arc.Clear(); customSpawner.listB_Cav.Clear();
            customSpawner.RebuildAllBatches();
        }
        else
        {
            historySpawner.listA_Inf.Clear(); historySpawner.listA_Arc.Clear(); historySpawner.listA_Cav.Clear();
            historySpawner.listB_Inf.Clear(); historySpawner.listB_Arc.Clear(); historySpawner.listB_Cav.Clear();
            historySpawner.RebuildAllBatches();
        }

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

            // ---------------------------------------------------------
            // DOBÓR WSKANIKA (Zale¿nie od nazwy jednostki)
            // ---------------------------------------------------------
            if (baseName.Contains("Archer") || baseName.Contains("Arc"))
            {
                // £ucznicy -> Ko³o/Dysk
                newGroup.arrowMesh = cachedArcherMesh != null ? cachedArcherMesh : cachedInfantryMesh;
                newGroup.speedMultiplier = 1.0f; // Standardowa prêdkoœæ
            }
            else if (baseName.Contains("Cavalry") || baseName.Contains("Cav"))
            {
                // Kawaleria -> D³u¿sza linia
                newGroup.arrowMesh = cachedCavalryMesh != null ? cachedCavalryMesh : cachedInfantryMesh;
                newGroup.speedMultiplier = cavalrySpeedMultiplier; // SZYBCIEJ (np. 1.5x)
            }
            else
            {
                // Piechota (Domyœlna) -> Linia
                newGroup.arrowMesh = cachedInfantryMesh;
                newGroup.speedMultiplier = 1.0f; // Standardowa prêdkoœæ
            }

            newGroup.arrowMaterial = directionArrowMaterial;
            // ---------------------------------------------------------

            // Zapamiêtanie materia³u do selekcji
            if (!originalMaterials.ContainsKey(newGroup))
            {
                originalMaterials.Add(newGroup, mat);
            }

            List<Matrix4x4> slice = sourceList.GetRange(i, count);
            newGroup.unitMatrices = slice;
            newGroup.RebuildBatches();
            newGroup.RecalculateCenter();
            targetCollection.Add(newGroup);

            if (createUI && uiContainer != null && groupUIPrefab != null)
            {
                GameObject newUI = Instantiate(groupUIPrefab, uiContainer);
                ArmyGroupUI uiScript = newUI.GetComponent<ArmyGroupUI>();
                if (uiScript != null)
                {
                    uiScript.Setup(newGroup, this);
                    uiElements.Add(uiScript);
                }
            }
            groupIndex++;
        }
    }

    void Update()
    {
        if (!groupsInitialized) return;

        if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
        if (Input.GetKeyDown(KeyCode.B)) KillRandomUnit(false);

        // --- FRAKCJA A ---
        foreach (var group in factionAGroups)
        {
            if (group.isMoving) ProcessMovement(group);

            group.RenderGroup();      // Renderuj ¿o³nierzy
            group.RenderIndicators(); // Renderuj wskaŸniki (wszystkich!)
        }

        // --- FRAKCJA B ---
        foreach (var group in factionBGroups)
        {
            if (group.isMoving) ProcessMovement(group);

            group.RenderGroup();      // Renderuj ¿o³nierzy
            group.RenderIndicators(); // Renderuj wskaŸniki (wszystkich!)
        }

        HandleMouseInput();
    }

    void KillRandomUnit(bool isFactionA)
    {
        List<TacticalGroup> targetFaction = isFactionA ? factionAGroups : factionBGroups;
        var aliveGroups = new List<TacticalGroup>();
        foreach (var g in targetFaction)
        {
            if (g.unitMatrices.Count > 0) aliveGroups.Add(g);
        }

        if (aliveGroups.Count == 0) return;

        TacticalGroup randomGroup = aliveGroups[Random.Range(0, aliveGroups.Count)];
        int lastIndex = randomGroup.unitMatrices.Count - 1;
        randomGroup.unitMatrices.RemoveAt(lastIndex);
        randomGroup.RebuildBatches();

        foreach (var ui in uiElements)
        {
            if (ui.GetGroup() == randomGroup)
            {
                ui.UpdateUnitCount();
                break;
            }
        }
        UpdateGlobalCounters();
    }

    void UpdateGlobalCounters()
    {
        int totalA = 0;
        foreach (var g in factionAGroups) totalA += g.unitMatrices.Count;
        int totalB = 0;
        foreach (var g in factionBGroups) totalB += g.unitMatrices.Count;

        if (counterTextA != null) counterTextA.text = totalA.ToString();
        if (counterTextB != null) counterTextB.text = totalB.ToString();
    }

    void SetGroupHighlight(TacticalGroup group, bool highlight)
    {
        if (group == null) return;

        if (highlight && highlightMaterial != null)
        {
            group.unitMaterial = highlightMaterial;
        }
        else
        {
            if (originalMaterials.ContainsKey(group))
            {
                group.unitMaterial = originalMaterials[group];
            }
        }
    }

    public void SelectGroup(TacticalGroup group)
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (shiftHeld && lastSelectedGroup != null && lastSelectedGroup != group)
        {
            HandleShiftSelection(group);
        }
        else if (ctrlHeld)
        {
            if (selectedGroups.Contains(group))
            {
                SetGroupHighlight(group, false);
                selectedGroups.Remove(group);
            }
            else
            {
                selectedGroups.Add(group);
                SetGroupHighlight(group, true);
                lastSelectedGroup = group;
            }
        }
        else
        {
            if (selectedGroups.Count == 1 && selectedGroups.Contains(group))
            {
                SetGroupHighlight(group, false);
                selectedGroups.Clear();
                lastSelectedGroup = null;
            }
            else
            {
                foreach (var g in selectedGroups)
                {
                    SetGroupHighlight(g, false);
                }
                selectedGroups.Clear();

                selectedGroups.Add(group);
                SetGroupHighlight(group, true);
                lastSelectedGroup = group;
            }
        }

        UpdateUISelectionVisuals();
    }

    void HandleShiftSelection(TacticalGroup currentGroup)
    {
        int indexStart = -1;
        int indexEnd = -1;

        for (int i = 0; i < uiElements.Count; i++)
        {
            if (uiElements[i].GetGroup() == lastSelectedGroup) indexStart = i;
            if (uiElements[i].GetGroup() == currentGroup) indexEnd = i;
        }

        if (indexStart != -1 && indexEnd != -1)
        {
            int min = Mathf.Min(indexStart, indexEnd);
            int max = Mathf.Max(indexStart, indexEnd);

            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            {
                foreach (var g in selectedGroups)
                {
                    SetGroupHighlight(g, false);
                }
                selectedGroups.Clear();
            }

            for (int i = min; i <= max; i++)
            {
                TacticalGroup g = uiElements[i].GetGroup();
                if (!selectedGroups.Contains(g))
                {
                    selectedGroups.Add(g);
                    SetGroupHighlight(g, true);
                }
            }
        }
    }

    void UpdateUISelectionVisuals()
    {
        foreach (var ui in uiElements)
        {
            TacticalGroup g = ui.GetGroup();
            if (selectedGroups.Contains(g))
            {
                ui.SetSelectionState(true);
            }
            else
            {
                ui.SetSelectionState(false);
            }
        }
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (selectedGroups.Count == 0) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, groundLayer))
            {
                foreach (var group in selectedGroups)
                {
                    IssueMoveOrder(group, hit.point);
                }
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

    bool IsPointInsideWaterBounds(Vector3 point)
    {
        if (waterCollider == null) return false;
        Bounds b = waterCollider.bounds;
        return (point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z);
    }

    void ProcessMovement(TacticalGroup group)
    {
        if (group.unitMatrices.Count == 0)
        {
            group.isMoving = false;
            return;
        }

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
        float actualSpeed = moveSpeed * group.speedMultiplier;
        Vector3 moveVector = direction * actualSpeed * Time.deltaTime;

        float unitDiameter = group.unitMatrices[0].lossyScale.x;
        Vector3 scaleVec = new Vector3(unitDiameter, unitDiameter, unitDiameter);

        for (int i = 0; i < group.unitMatrices.Count; i++)
        {
            Matrix4x4 mat = group.unitMatrices[i];
            Vector3 currentPos = mat.GetColumn(3);
            Vector3 newPos = currentPos + moveVector;

            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

            float groundHeight = 0f;
            if (terrain != null)
                groundHeight = terrain.SampleHeight(newPos) + terrain.transform.position.y;
            else
                groundHeight = newPos.y;

            float finalY = groundHeight;
            bool inWater = false;
            if (waterFound && IsPointInsideWaterBounds(newPos))
            {
                if (groundHeight < waterSurfaceY)
                {
                    inWater = true;
                    finalY = waterSurfaceY - swimmingDepth;
                }
            }

            if (!inWater) finalY = groundHeight + currentOffset;

            newPos.y = Mathf.Lerp(currentPos.y, finalY, Time.deltaTime * 10f);

            Quaternion currentRot = mat.rotation;
            Quaternion targetRot = Quaternion.LookRotation(direction);
            Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);

            group.unitMatrices[i] = Matrix4x4.TRS(newPos, nextRot, scaleVec);
        }

        group.RebuildBatches();
        group.RecalculateCenter();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 center = new Vector3((minX + maxX) / 2, 50, (minZ + maxZ) / 2);
        Vector3 size = new Vector3(maxX - minX, 100, maxZ - minZ);
        Gizmos.DrawWireCube(center, size);
    }
}