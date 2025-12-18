using UnityEngine;
using System.Collections.Generic;

public class ArmyHierarchyManager : MonoBehaviour
{
    [Header("References (Auto-detected)")]
    public UnitsSpawnCustom customSpawner; // Dla Custom Battle
    public UnitsSpawn historySpawner;      // Dla Historical Battle

    [Header("UI Integration")]
    public GameObject groupUIPrefab;    // Prefab przycisku
    public Transform uiContainer;       // Panel UnitPanel

    [Header("Hierarchy Settings")]
    public HierarchyRank groupSize = HierarchyRank.Company; // 150

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 8f;
    public float stopDistance = 2.0f;
    public LayerMask groundLayer = 1;

    // Listy danych
    public List<TacticalGroup> factionAGroups = new List<TacticalGroup>();
    public List<TacticalGroup> factionBGroups = new List<TacticalGroup>();

    private List<ArmyGroupUI> uiElements = new List<ArmyGroupUI>();

    // STAN GRY
    private TacticalGroup selectedGroup = null;
    private bool groupsInitialized = false;
    private Terrain terrain;

    void Start()
    {
        terrain = Terrain.activeTerrain;
        Invoke(nameof(InitializeGroups), 0.5f);
    }

    void InitializeGroups()
    {
        // 1. AUTO-DETEKCJA SPAWNERA
        if (customSpawner == null) customSpawner = GetComponent<UnitsSpawnCustom>();
        if (historySpawner == null) historySpawner = GetComponent<UnitsSpawn>();

        bool useCustom = (customSpawner != null && customSpawner.enabled);
        bool useHistory = (historySpawner != null && historySpawner.enabled);

        if (!useCustom && !useHistory)
        {
            Debug.LogError("ArmyHierarchyManager: Nie znaleziono ¿adnego aktywnego skryptu UnitsSpawn!");
            return;
        }

        // 2. CZYSZCZENIE PANELU UI
        if (uiContainer != null)
        {
            for (int i = uiContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(uiContainer.GetChild(i).gameObject);
            }
        }

        // 3. OKREŒLENIE STRONY GRACZA (MODYFIKACJA TUTAJ)
        bool isPlayerA = true; // Domyœlna wartoœæ
        bool isPlayerB = false;

        if (useCustom)
        {
            // --- TRYB CUSTOM: ZAWSZE JESTEŒMY FRAKCJ¥ A (NIEBIESCY) ---
            isPlayerA = true;
            isPlayerB = false;
            Debug.Log("Tryb Custom: Wymuszono Frakcjê A (Gracz = Niebiescy).");
        }
        else
        {
            // --- TRYB HISTORYCZNY: DECYDUJE WYBÓR W MENU (PlayerPrefs) ---
            string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
            isPlayerA = (playerSide == "SideA");
            isPlayerB = !isPlayerA;
            Debug.Log($"Tryb Historyczny: Wybrano {playerSide} z PlayerPrefs.");
        }

        // 4. POBRANIE DANYCH
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

        // 5. TWORZENIE GRUP Z WARUNKIEM

        // --- FRAKCJA A (Niebiescy) ---
        // Generuj UI tylko jeœli isPlayerA == true
        CreateGroupsFromList(a_Inf, mInf, matA, "A_Infantry", factionAGroups, isPlayerA);
        CreateGroupsFromList(a_Arc, mArc, matA, "A_Archers", factionAGroups, isPlayerA);
        CreateGroupsFromList(a_Cav, mCav, matA, "A_Cavalry", factionAGroups, isPlayerA);

        // --- FRAKCJA B (Czerwoni) ---
        // Generuj UI tylko jeœli isPlayerB == true
        CreateGroupsFromList(b_Inf, mInf, matB, "B_Infantry", factionBGroups, isPlayerB);
        CreateGroupsFromList(b_Arc, mArc, matB, "B_Archers", factionBGroups, isPlayerB);
        CreateGroupsFromList(b_Cav, mCav, matB, "B_Cavalry", factionBGroups, isPlayerB);

        // 6. PRZEJÊCIE RENDEROWANIA
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

        foreach (var group in factionAGroups) group.RenderGroup();
        foreach (var group in factionBGroups) group.RenderGroup();

        HandleMouseInput();
    }

    public void SelectGroup(TacticalGroup group)
    {
        selectedGroup = group;
        foreach (var ui in uiElements) ui.SetSelectionState(false);
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButton(1))
        {
            if (selectedGroup == null) return;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, groundLayer))
            {
                MoveSelectedGroup(selectedGroup, hit.point);
            }
        }
    }

    public void MoveSelectedGroup(TacticalGroup group, Vector3 destination)
    {
        if (group == null || group.unitMatrices.Count == 0) return;

        Vector3 flatCenter = new Vector3(group.groupCenter.x, 0, group.groupCenter.z);
        Vector3 flatTarget = new Vector3(destination.x, 0, destination.z);

        if (Vector3.Distance(flatCenter, flatTarget) < stopDistance) return;

        Vector3 direction = (flatTarget - flatCenter).normalized;
        Vector3 moveVector = direction * moveSpeed * Time.deltaTime;

        float unitDiameter = group.unitMatrices[0].lossyScale.x;
        Vector3 scaleVec = new Vector3(unitDiameter, unitDiameter, unitDiameter);

        for (int i = 0; i < group.unitMatrices.Count; i++)
        {
            Matrix4x4 mat = group.unitMatrices[i];
            Vector3 currentPos = mat.GetColumn(3);
            Vector3 newPos = currentPos + moveVector;

            float groundHeight = 0f;
            if (terrain != null)
                groundHeight = terrain.SampleHeight(newPos) + terrain.transform.position.y;
            else
                groundHeight = newPos.y;

            newPos.y = Mathf.Lerp(currentPos.y, groundHeight, Time.deltaTime * 10f);

            Quaternion currentRot = mat.rotation;
            Quaternion targetRot = Quaternion.LookRotation(direction);
            Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);

            group.unitMatrices[i] = Matrix4x4.TRS(newPos, nextRot, scaleVec);
        }

        group.RebuildBatches();
        group.RecalculateCenter();
    }
}