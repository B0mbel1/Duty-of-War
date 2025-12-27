using UnityEngine;
using System.Collections.Generic;
using TMPro; // <-- Potrzebne do obs³ugi tekstu

public class ArmyHierarchyManager : MonoBehaviour
{
    [Header("References (Auto-detected)")]
    public UnitsSpawnCustom customSpawner;
    public UnitsSpawn historySpawner;

    [Header("UI Integration (Global Counters)")]
    public TextMeshProUGUI counterTextA; // Licznik Frakcji A (np. 9000)
    public TextMeshProUGUI counterTextB; // Licznik Frakcji B (np. 9000)

    [Header("UI Integration (Buttons)")]
    public GameObject groupUIPrefab;
    public Transform uiContainer;

    [Header("Hierarchy Settings")]
    public HierarchyRank groupSize = HierarchyRank.Company;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 8f;
    public float stopDistance = 2.0f;
    public LayerMask groundLayer = 1;

    [Header("Map Boundaries (Granice Mapy)")]
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
    private TacticalGroup selectedGroup = null;
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

        DetectWater();
        Invoke(nameof(InitializeGroups), 0.5f);
    }

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

        // --- PRZEJMOWANIE TEKSTÓW ZE SPAWNERA ---
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
        // ----------------------------------------

        if (uiContainer != null)
        {
            for (int i = uiContainer.childCount - 1; i >= 0; i--)
                Destroy(uiContainer.GetChild(i).gameObject);
        }
        uiElements.Clear();

        bool isPlayerA = true;
        bool isPlayerB = false;

        if (useCustom)
        {
            isPlayerA = true;
            isPlayerB = false;
        }
        else
        {
            string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
            isPlayerA = (playerSide == "SideA");
            isPlayerB = !isPlayerA;
        }

        // Pobranie danych...
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

        // Tworzenie grup
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

        // DEBUG: Zabijanie
        if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
        if (Input.GetKeyDown(KeyCode.B)) KillRandomUnit(false);

        foreach (var group in factionAGroups)
        {
            if (group.isMoving) ProcessMovement(group);
            group.RenderGroup();
        }

        foreach (var group in factionBGroups)
        {
            if (group.isMoving) ProcessMovement(group);
            group.RenderGroup();
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

        // Usuwanie jednostki
        randomGroup.unitMatrices.RemoveAt(lastIndex);

        randomGroup.RebuildBatches();

        // Aktualizacja przycisku tej konkretnej grupy
        foreach (var ui in uiElements)
        {
            if (ui.GetGroup() == randomGroup)
            {
                ui.UpdateUnitCount();
                break;
            }
        }

        // Aktualizacja G£ÓWNYCH liczników
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

    public void SelectGroup(TacticalGroup group)
    {
        selectedGroup = group;
        foreach (var ui in uiElements)
        {
            if (ui.GetGroup() == selectedGroup) ui.SetSelectionState(true);
            else ui.SetSelectionState(false);
        }
    }

    // --- ZMODYFIKOWANA FUNKCJA ---
    void HandleMouseInput()
    {
        // Zmiana: 0 oznacza lewy przycisk myszy (by³o 1 - prawy)
        if (Input.GetMouseButtonDown(0))
        {
            if (selectedGroup == null) return;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, groundLayer))
            {
                IssueMoveOrder(selectedGroup, hit.point);
            }
        }
    }
    // -----------------------------

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
        Vector3 moveVector = direction * moveSpeed * Time.deltaTime;

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