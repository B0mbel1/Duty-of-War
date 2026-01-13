using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement; // <-- DODAJ TĘ LINIJKĘ

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

    [Header("End Game Settings")] // --- NOWOŚĆ ---
    public GameObject endGamePanel;
    public TextMeshProUGUI resultText;
    public string menuSceneName = "MainMenu"; // Wpisz tu nazwę swojej sceny z menu
    private bool isBattleOver = false;

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

    // --- NOWOŚĆ: PRZYWRÓCONY LAS ---
    [Header("Forest Settings")]
    [Tooltip("Dokładna nazwa warstwy terenu (Terrain Layer), która jest lasem.")]
    public string forestLayerName = "Forest";
    public float forestCavalryPenalty = 0.7f; // Kawaleria zwalnia mocniej
    public float forestInfantryPenalty = 0.9f; // Piechota radzi sobie lepiej
    private int forestLayerIndex = -1;
    // --------------------------------

    [Header("Slope Physics")]
    public float slopeCheckDistance = 2.0f;
    public float lightSlopeAngle = 10f;
    public float steepSlopeAngle = 30f;

    [Header("Slope Multipliers")]
    public float uphillLightMult = 0.8f;
    public float uphillSteepMult = 0.4f;
    public float downhillLightMult = 1.1f;
    public float downhillSteepMult = 0.9f;

    [Header("Combat Settings")]
    public float attackRangeMelee = 3.5f;
    public float attackRangeArcher = 80f;
    [Tooltip("Ile metrów zasięgu zyskuje łucznik za każdy metr przewagi wysokości.")]
    public float heightRangeBonus = 0.5f; // Np. 0.5 oznacza: będąc 10m wyżej, mam 5m więcej zasięgu.
    [Tooltip("Maksymalna premia do obrażeń za wysokość.")]
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
    private Dictionary<TacticalGroup, Material> originalMaterials = new Dictionary<TacticalGroup, Material>();

    private bool groupsInitialized = false;
    private Terrain terrain;
    private TerrainData terrainData; // Cache danych terenu

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

            // Znajdź indeks warstwy lasu
            FindForestLayerIndex();
        }

        PrepareIndicatorMeshes();
        Invoke(nameof(InitializeGroups), 0.5f);
    }

    void FindForestLayerIndex()
    {
        if (terrainData == null) return;
        for (int i = 0; i < terrainData.terrainLayers.Length; i++)
        {
            if (terrainData.terrainLayers[i].name == forestLayerName)
            {
                forestLayerIndex = i;
                Debug.Log($"[ArmyManager] Las znaleziony na indeksie warstwy: {i}");
                return;
            }
        }
        Debug.LogWarning($"[ArmyManager] Nie znaleziono warstwy lasu o nazwie: {forestLayerName}");
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
        uiElements.Clear(); originalMaterials.Clear(); factionAGroups.Clear(); factionBGroups.Clear();

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

            switch (type)
            {
                case UnitType.Archer: newGroup.arrowMesh = cachedArcherMesh ?? cachedInfantryMesh; newGroup.speedMultiplier = 1.0f; break;
                case UnitType.Cavalry: newGroup.arrowMesh = cachedCavalryMesh ?? cachedInfantryMesh; newGroup.speedMultiplier = cavalrySpeedMultiplier; break;
                default: newGroup.arrowMesh = cachedInfantryMesh; newGroup.speedMultiplier = 1.0f; break;
            }
            newGroup.arrowMaterial = directionArrowMaterial;
            if (!originalMaterials.ContainsKey(newGroup)) originalMaterials.Add(newGroup, mat);

            List<Matrix4x4> slice = sourceList.GetRange(i, count);
            newGroup.unitMatrices = slice;
            foreach (var matrix in slice)
            {
                newGroup.unitHealth.Add(hpMax);
                newGroup.attackCooldowns.Add(Random.Range(0f, 1.0f));
                newGroup.lastPositions.Add((Vector3)matrix.GetColumn(3));
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
        if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
        if (Input.GetKeyDown(KeyCode.B)) KillRandomUnit(false);

        foreach (var group in factionAGroups) { if (group.unitMatrices.Count > 0) { if (group.isMoving) ProcessMovement(group); group.RenderGroup(); group.RenderIndicators(); } }
        foreach (var group in factionBGroups) { if (group.unitMatrices.Count > 0) { if (group.isMoving) ProcessMovement(group); group.RenderGroup(); group.RenderIndicators(); } }

        HandleMouseInput();
        ResolveCombatRoundRobin();
    }

    void ProcessMovement(TacticalGroup group)
    {
        Vector3 flatCenter = new Vector3(group.groupCenter.x, 0, group.groupCenter.z);
        Vector3 flatTarget = new Vector3(group.currentDestination.x, 0, group.currentDestination.z);

        if (Vector3.SqrMagnitude(flatTarget - flatCenter) < stopDistance * stopDistance)
        {
            group.isMoving = false;
            return;
        }

        Vector3 direction = (flatTarget - flatCenter).normalized;
        float baseSpeed = moveSpeed * group.speedMultiplier * Time.deltaTime;

        Quaternion currentRot = group.unitMatrices[0].rotation;
        Quaternion targetRot = Quaternion.LookRotation(direction);
        Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);

        Vector3 scaleVec = group.unitMatrices[0].lossyScale;

        // Cache danych terenu (dla lasu)
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

            float terrainY = currentPos.y;
            if (terrain != null) terrainY = terrain.SampleHeight(currentPos) + terrain.transform.position.y;

            // 1. WODA (Raycast)
            bool inWater = false;
            float waterHeight = -1000f;
            RaycastHit hit;
            if (Physics.Raycast(currentPos + Vector3.up * 50f, Vector3.down, out hit, 100f, waterLayerMask))
            {
                if (hit.point.y > terrainY - 0.1f)
                {
                    inWater = true;
                    waterHeight = hit.point.y;
                }
            }

            // 2. POCHYŁOŚĆ
            float slopeMod = 1.0f;
            if (terrain != null && !inWater)
            {
                Vector3 probePos = currentPos + (direction * slopeCheckDistance);
                float nextY = terrain.SampleHeight(probePos) + terrain.transform.position.y;
                float heightDiff = nextY - terrainY;
                float angle = Mathf.Atan2(heightDiff, slopeCheckDistance) * Mathf.Rad2Deg;

                if (angle > steepSlopeAngle) slopeMod = uphillSteepMult;
                else if (angle > lightSlopeAngle) slopeMod = uphillLightMult;
                else if (angle < -steepSlopeAngle) slopeMod = downhillSteepMult;
                else if (angle < -lightSlopeAngle) slopeMod = downhillLightMult;
            }

            // --- 3. LAS (ALPHAMAP CHECK) ---
            float forestMod = 1.0f;
            if (forestLayerIndex != -1 && !inWater && terrainData != null)
            {
                // Obliczamy współrzędne na splatmapie
                int mapX = Mathf.RoundToInt(((currentPos.x - terrain.transform.position.x) / terrainData.size.x) * alphaWidth);
                int mapZ = Mathf.RoundToInt(((currentPos.z - terrain.transform.position.z) / terrainData.size.z) * alphaHeight);

                // Sprawdzamy czy jednostka jest w granicach
                if (mapX >= 0 && mapX < alphaWidth && mapZ >= 0 && mapZ < alphaHeight)
                {
                    // Pobieramy wartość tekstury lasu w tym punkcie
                    float[,,] splat = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
                    if (splat[0, 0, forestLayerIndex] > 0.5f) // Jeśli tekstura lasu > 50%
                    {
                        forestMod = (group.type == UnitType.Cavalry) ? forestCavalryPenalty : forestInfantryPenalty;
                    }
                }
            }
            // --------------------------------

            float finalMod = slopeMod * forestMod;
            if (inWater) finalMod *= waterSpeedPenalty;

            Vector3 newPos = currentPos + (direction * baseSpeed * finalMod);

            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.z = Mathf.Clamp(newPos.z, minZ, maxZ);

            if (inWater) newPos.y = waterHeight - swimmingDepth;
            else if (terrain != null) newPos.y = terrain.SampleHeight(newPos) + terrain.transform.position.y;
            else newPos.y = terrainY;

            group.unitMatrices[i] = Matrix4x4.TRS(newPos, nextRot, scaleVec);
        }

        group.UpdateBatchData();
        group.RecalculateCenter();
    }

    void ResolveCombatRoundRobin()
    {
        int totalBudget = maxCombatChecksPerFrame;
        int halfBudget = totalBudget / 2;

        // Faza 1: A atakuje B
        spatialGrid.Clear();
        foreach (var groupB in factionBGroups)
        {
            if (groupB.unitMatrices.Count == 0) continue;
            for (int i = 0; i < groupB.unitMatrices.Count; i++)
                AddToGrid(groupB, i, (Vector3)groupB.unitMatrices[i].GetColumn(3));
        }
        ProcessFactionAttack(factionAGroups, factionBGroups, ref nextGroupIdxA, ref nextUnitIdxA, halfBudget);

        // Faza 2: B atakuje A
        spatialGrid.Clear();
        foreach (var groupA in factionAGroups)
        {
            if (groupA.unitMatrices.Count == 0) continue;
            for (int i = 0; i < groupA.unitMatrices.Count; i++)
                AddToGrid(groupA, i, (Vector3)groupA.unitMatrices[i].GetColumn(3));
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
                groupIdx++;
                unitIdx = 0;
                groupsChecked++;
                continue;
            }

            while (unitIdx < attGroup.unitMatrices.Count && budget > 0)
            {
                ProcessSingleAttacker(attGroup, defenders, unitIdx);
                unitIdx++;
                budget--;
            }

            if (unitIdx >= attGroup.unitMatrices.Count)
            {
                groupIdx++;
                unitIdx = 0;
                groupsChecked++;
            }
        }
    }

    void ProcessSingleAttacker(TacticalGroup attackerGroup, List<TacticalGroup> enemyFaction, int idx)
    {
        if (idx >= attackerGroup.attackCooldowns.Count || idx >= attackerGroup.unitMatrices.Count) return;

        float dt = Time.deltaTime;
        if (attackerGroup.attackCooldowns[idx] > 0)
        {
            attackerGroup.attackCooldowns[idx] -= dt;
            return;
        }

        Matrix4x4 myMat = attackerGroup.unitMatrices[idx];
        Vector3 myPos = (Vector3)myMat.GetColumn(3); // Pozycja
        Vector3 myForward = (Vector3)myMat.GetColumn(2);

        bool isArcher = attackerGroup.type == UnitType.Archer;
        float range = isArcher ? attackRangeArcher : attackRangeMelee;
        float rangeSq = range * range;
        float cooldownTime = isArcher ? archerAttackCooldown : attackCooldown;


        UnitRef target = new UnitRef { group = null };

        if (isArcher)
        {
            target = FindTargetGlobal(myPos, range, rangeSq, enemyFaction);
        }
        else
        {
            target = FindTargetInGrid(myPos, rangeSq);
        }

        if (target.group != null)
        {
            if (target.index >= target.group.unitMatrices.Count) return;

            Vector3 dirToTarget = (target.position - myPos).normalized;
            // Dot Product: 1.0 = idealnie przodem, 0.0 = bokiem, -1.0 = idealnie tyłem
            // Wartość 0.2f daje dość szeroki kąt z przodu (ok. 150 stopni pola widzenia)
            float angleDot = Vector3.Dot(myForward, dirToTarget);

            if (angleDot < 0.2f)
            {
                // Jeśli wróg jest za plecami lub mocno z boku -> nie atakuj w tej klatce
                // (Opcjonalnie: można tu dodać logikę obracania się w stronę wroga)
                return;
            }
            // -------------------------------------------------------------

            float dmg = CalculateDamage(attackerGroup, target.group, idx, target.index, myPos, target.position);
            target.group.unitHealth[target.index] -= dmg;
            attackerGroup.attackCooldowns[idx] = cooldownTime * Random.Range(0.8f, 1.2f);

            if (target.group.unitHealth[target.index] <= 0)
            {
                KillUnit(target.group, target.index);
            }
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
            float distToGroup = Vector3.Distance(myPos, group.groupCenter);
            if (flatDistToGroup < baseRange + group.groupRadius + 50f)
            {
                // Sprawdzamy kilka losowych jednostek w oddziale (próbkowanie)
                int attempts = 3;
                for (int k = 0; k < attempts; k++)
                {
                    int randIdx = Random.Range(0, group.unitMatrices.Count);
                    Vector3 enemyPos = (Vector3)group.unitMatrices[randIdx].GetColumn(3);

                    // --- LOGIKA BALISTYKI I WYSOKOŚCI ---

                    // A. Oblicz różnicę wysokości
                    float heightDiff = myPos.y - enemyPos.y; // Dodatnie = strzelam w dół

                    // B. Oblicz zmodyfikowany zasięg
                    // Jeśli jesteś wyżej, zasięg rośnie. Jeśli niżej, maleje.
                    float effectiveRange = baseRange + (heightDiff * heightRangeBonus);
                    if (effectiveRange < 10f) effectiveRange = 10f; // Minimalny zasięg

                    // C. Oblicz dystans płaski (XZ) do konkretnego wroga
                    float dx = myPos.x - enemyPos.x;
                    float dz = myPos.z - enemyPos.z;
                    float distFlatSq = (dx * dx) + (dz * dz);

                    // Sprawdź czy wróg jest w zasięgu (porównujemy kwadraty dla wydajności)
                    if (distFlatSq < effectiveRange * effectiveRange)
                    {
                        // D. Raycast (Line of Sight) - Czy widzimy wroga?
                        // Podnosimy punkt startu i końca o 1.5m (wysokość wzroku/głowy), żeby nie strzelać w podłogę pod nogami
                        Vector3 originHead = myPos + Vector3.up * 1.5f;
                        Vector3 targetHead = enemyPos + Vector3.up * 1.0f;

                        // Sprawdzamy czy teren nie zasłania (używamy terrainLayerMask z Twojego skryptu)
                        if (!Physics.Linecast(originHead, targetHead, terrainLayerMask))
                        {
                            return new UnitRef { group = group, index = randIdx, position = enemyPos };
                        }
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
                int nx = cx + x;
                int nz = cz + z;
                int key = (nx * 73856093) ^ (nz * 19349663);

                if (spatialGrid.TryGetValue(key, out List<UnitRef> list))
                {
                    foreach (var u in list)
                    {
                        if ((u.position - pos).sqrMagnitude < rangeSq) return u;
                    }
                }
            }
        }
        return new UnitRef { group = null };
    }

    float CalculateDamage(TacticalGroup attGroup, TacticalGroup defGroup, int attIdx, int defIdx, Vector3 attPos, Vector3 defPos)
    {
        float finalDamage = 0f;
        switch (attGroup.type)
        {
            case UnitType.Infantry: finalDamage = dmgInfantry; break;
            case UnitType.Archer: finalDamage = Random.Range(dmgArcherMin, dmgArcherMax); break;
            case UnitType.Cavalry: finalDamage = attGroup.isMoving ? dmgCavalryCharge : dmgCavalryStatic; break;
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

        // --- NOWOŚĆ: Sprawdź czy koniec gry ---
        CheckWinCondition(totalA, totalB);
    }

    void SetGroupHighlight(TacticalGroup group, bool highlight)
    {
        if (group == null || group.unitMatrices.Count == 0) return;
        if (highlight && highlightMaterial != null) group.unitMaterial = highlightMaterial;
        else if (originalMaterials.ContainsKey(group)) group.unitMaterial = originalMaterials[group];
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

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0) && selectedGroups.Count > 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 10000f, terrainLayerMask))
            {
                foreach (var group in selectedGroups) if (group.unitMatrices.Count > 0) IssueMoveOrder(group, hit.point);
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

        // Pobieramy, którą stroną jest gracz (zapisane w PlayerPrefs w menu)
        // Domyślnie zakładamy SideA, jeśli brakuje zapisu
        string playerSide = PlayerPrefs.GetString("PlayerSide", "SideA");
        bool playerIsSideA = (playerSide == "SideA");

        if (countA <= 0)
        {
            // Strona A przegrała (wszyscy zginęli)
            if (playerIsSideA) GameOver(false); // Gracz był A -> Porażka
            else GameOver(true);                // Gracz był B -> Zwycięstwo
        }
        else if (countB <= 0)
        {
            // Strona B przegrała
            if (!playerIsSideA) GameOver(false); // Gracz był B -> Porażka
            else GameOver(true);                 // Gracz był A -> Zwycięstwo
        }
    }

    void GameOver(bool isVictory)
    {
        isBattleOver = true;
        Time.timeScale = 0f; // Zatrzymaj grę (fizykę i ruch)

        // --- NOWOŚĆ: Wyłączamy możliwość pauzowania ---
        PauseMenuController pauseCtrl = FindAnyObjectByType<PauseMenuController>();
        if (pauseCtrl != null)
        {
            pauseCtrl.enabled = false; // Wyłącza Update() w skrypcie pauzy -> ESC przestaje działać
        }
        // ----------------------------------------------

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);

            if (resultText != null)
            {
                if (isVictory)
                {
                    resultText.text = "WIELKIE ZWYCIĘSTWO!";
                    resultText.color = Color.green;
                }
                else
                {
                    resultText.text = "HANIEBNA PORAŻKA...";
                    resultText.color = Color.red;
                }
            }
        }
    }
    public void ReturnToMenu()
    {
        Time.timeScale = 1f; // Odblokuj czas przed zmianą sceny!
        SceneManager.LoadScene(menuSceneName);
    }
}