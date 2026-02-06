using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class UnitsSpawnCustom : MonoBehaviour
{
    [System.Serializable]
    public class ArmyComposition
    {
        public string name = "Army Settings";
        public int infantryCount = 0;
        public int archerCount = 0;
        public int cavalryCount = 0;
    }

    public enum SpawnShape
    {
        Rectangle,
        Ellipse
    }

    [Header("UI & Counters")]
    public TextMeshProUGUI factionAText;
    public TextMeshProUGUI factionBText;

    [Header("Army Composition")]
    public ArmyComposition factionA_Army;
    public ArmyComposition factionB_Army;

    [Header("Visuals - Materials")]
    public Material factionAMaterial;
    public Material factionBMaterial;

    [Header("Visuals - Shapes")]
    public Mesh infantryMesh;
    public Mesh archerMesh;
    public Mesh cavalryMesh;

    [Header("Spawn Areas - Main")]
    public GameObject spawnAreaA;
    public GameObject spawnAreaB;

    [Header("Spawn Areas - Secondary (Optional)")]
    public GameObject spawnAreaA_Secondary;
    public GameObject spawnAreaB_Secondary;

    [Header("Grid Settings")]
    public float unitDiameter = 1.0f;
    public float unitSpacing = 1.5f;
    [Range(0f, 1f)]
    public float randomness = 0.8f;

    [Header("Water Settings")]
    public string waterTag = "Water";
    public float swimmingDepth = 0.5f;

    // --- USTAWIENIA ROTACJI I WYMIARÓW ---
    [Header("Spawn Orientation & Logic")]
    public SpawnShape factionAShape = SpawnShape.Rectangle;
    public SpawnShape factionBShape = SpawnShape.Rectangle;
    
    [Tooltip("PRIORYTET 1: Użyj rotacji Cube'a.")]
    public bool useSpawnBoxRotation = true;

    [Tooltip("PRIORYTET 2: Główny wróg równolegle do gracza (Fix dla Jarmuk).")]
    public bool forceEnemyAlignmentToPlayer = true;

    [Space(10)]
    [Tooltip("Zaznacz na Orlim Gnieździe (Wąwozy). Odznacz na płaskim.")]
    public bool useSmartWidthProjection = true;

    [Tooltip("RATUNEK: Jeśli jednostki są sklejone, wpisz tu szerokość w metrach (np. 500, 800). Jeśli 0 - liczy automat.")]
    public float manualFormationWidth = 0f;

    [Header("Camera Start Settings")]
    public Camera battleCamera;
    public float cameraHeight = 60f;
    public float cameraDistanceBack = 40f;
    public float cameraAngle = 55f;

    // Listy macierzy
    public List<Matrix4x4> listA_Inf = new List<Matrix4x4>();
    public List<Matrix4x4> listA_Arc = new List<Matrix4x4>();
    public List<Matrix4x4> listA_Cav = new List<Matrix4x4>();

    public List<Matrix4x4> listB_Inf = new List<Matrix4x4>();
    public List<Matrix4x4> listB_Arc = new List<Matrix4x4>();
    public List<Matrix4x4> listB_Cav = new List<Matrix4x4>();

    private List<Matrix4x4[]> batchesA_Inf = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesA_Arc = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesA_Cav = new List<Matrix4x4[]>();

    private List<Matrix4x4[]> batchesB_Inf = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesB_Arc = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesB_Cav = new List<Matrix4x4[]>();

    private Terrain terrain;
    private const int batchSize = 1023;
    enum UnitType { Infantry, Archer, Cavalry }

    private float waterSurfaceY;
    private bool waterFound = false;

    void Start()
    {
        if (factionAMaterial) factionAMaterial.enableInstancing = true;
        if (factionBMaterial) factionBMaterial.enableInstancing = true;

        terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        LoadCustomSettings();
        DetectWater();

        // 1. Spawnujemy Frakcję A
        HandleFactionSpawning(factionA_Army, spawnAreaA, spawnAreaA_Secondary, true, factionAShape, null);
        
        // 2. Spawnujemy Frakcję B
        HandleFactionSpawning(factionB_Army, spawnAreaB, spawnAreaB_Secondary, false, factionBShape, spawnAreaA);

        RebuildAllBatches();
        UpdateUICounters();
        SetupCameraPosition();
    }

    void HandleFactionSpawning(ArmyComposition fullArmy, GameObject mainArea, GameObject secArea, bool isFactionA, SpawnShape shape, GameObject alignmentReference)
    {
        if (mainArea == null) return;

        mainArea.GetComponent<Renderer>().enabled = false;
        if (secArea != null) secArea.GetComponent<Renderer>().enabled = false;

        Vector3 defaultTarget = Vector3.zero;
        if (isFactionA && spawnAreaB != null) defaultTarget = spawnAreaB.transform.position;
        if (!isFactionA && spawnAreaA != null) defaultTarget = spawnAreaA.transform.position;

        if (secArea != null)
        {
            ArmyComposition army1 = new ArmyComposition();
            army1.name = fullArmy.name + "_Part1";
            army1.infantryCount = fullArmy.infantryCount / 2;
            army1.archerCount = fullArmy.archerCount / 2;
            army1.cavalryCount = fullArmy.cavalryCount / 2;

            ArmyComposition army2 = new ArmyComposition();
            army2.name = fullArmy.name + "_Part2";
            army2.infantryCount = fullArmy.infantryCount - army1.infantryCount;
            army2.archerCount = fullArmy.archerCount - army1.archerCount;
            army2.cavalryCount = fullArmy.cavalryCount - army1.cavalryCount;

            ProcessFactionSpawns(army1, mainArea, isFactionA, shape, defaultTarget, alignmentReference);
            ProcessFactionSpawns(army2, secArea, isFactionA, shape, defaultTarget, alignmentReference);
        }
        else
        {
            ProcessFactionSpawns(fullArmy, mainArea, isFactionA, shape, defaultTarget, alignmentReference);
        }
    }

    void ProcessFactionSpawns(ArmyComposition army, GameObject spawnArea, bool isFactionA, SpawnShape shape, Vector3 autoLookTarget, GameObject alignmentReference)
    {
        if (spawnArea == null) return;

        Vector3 areaCenter = spawnArea.transform.position;
        Vector3 areaSize = spawnArea.transform.localScale;

        Vector3 forwardDir;
        Vector3 rightDir;

        // --- 1. USTALANIE KIERUNKU ---
        if (useSpawnBoxRotation)
        {
            forwardDir = spawnArea.transform.forward;
        }
        else if (!isFactionA && forceEnemyAlignmentToPlayer && alignmentReference != null && spawnArea == spawnAreaB)
        {
            Vector3 playerForward = alignmentReference.transform.forward;
            forwardDir = -playerForward;
        }
        else
        {
            forwardDir = (autoLookTarget - areaCenter).normalized;
            if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;
        }

        // Zabezpieczenie przed zerowym wektorem (zapobiega sklejaniu w punkcie 0,0,0)
        forwardDir.y = 0;
        if (forwardDir.sqrMagnitude < 0.001f) forwardDir = Vector3.forward;
        forwardDir.Normalize();

        rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

        // --- 2. USTALANIE SZEROKOŚCI (FIX dla Pal Galur) ---
        float usableWidth = 100f;
        float depthSize = 100f;

        // A) Manual Override - Ostateczne rozwiązanie
        if (manualFormationWidth > 10f)
        {
            usableWidth = manualFormationWidth;
            depthSize = 50f; // Domyślna głębokość punktu startu
        }
        // B) Smart Projection - Dobre na góry/wąwozy
        else if (useSmartWidthProjection)
        {
            float projectedWidthX = Mathf.Abs(Vector3.Dot(spawnArea.transform.right * areaSize.x, rightDir));
            float projectedWidthZ = Mathf.Abs(Vector3.Dot(spawnArea.transform.forward * areaSize.z, rightDir));
            usableWidth = (projectedWidthX + projectedWidthZ) * 0.9f;

            float projectedDepthX = Mathf.Abs(Vector3.Dot(spawnArea.transform.right * areaSize.x, forwardDir));
            float projectedDepthZ = Mathf.Abs(Vector3.Dot(spawnArea.transform.forward * areaSize.z, forwardDir));
            depthSize = projectedDepthX + projectedDepthZ;
        }
        // C) Standard Mode - Dobre na płaskie (poprawiona logika)
        else
        {
            // Sprawdzamy, która oś lokalna Boxa jest bardziej równoległa do naszej linii frontu (rightDir)
            float alignmentX = Mathf.Abs(Vector3.Dot(spawnArea.transform.right, rightDir));
            
            if (alignmentX > 0.5f) // Box jest ustawiony bokiem wzdłuż frontu
            {
                usableWidth = areaSize.x * 0.9f;
                depthSize = areaSize.z;
            }
            else // Box jest ustawiony głębokością wzdłuż frontu (albo obrócony o 90 stopni)
            {
                usableWidth = areaSize.z * 0.9f;
                depthSize = areaSize.x;
            }
        }

        // --- ZABEZPIECZENIE ANTI-GLUE ---
        // Jeśli z jakiegoś powodu szerokość wyszła mikroskopijna, wymuś minimum.
        if (usableWidth < 50f) usableWidth = 50f;

        Vector3 frontLineCenter = areaCenter + (forwardDir * (depthSize * 0.4f));
        Vector3 rowStart = frontLineCenter - (rightDir * (usableWidth * 0.5f));

        float gapBetweenGroups = 15.0f;
        float currentXOffset = 0f;

        void SpawnTypeSequence(UnitType type, int totalCount)
        {
            if (totalCount <= 0) return;

            int groupSize = 150;
            int unitsSpawned = 0;
            float dynamicBlockSize = 40.0f;
            float blockDepth = 20.0f;

            if (type == UnitType.Cavalry) { dynamicBlockSize = 65.0f; blockDepth = 25.0f; }
            else if (type == UnitType.Archer) { dynamicBlockSize = 55.0f; blockDepth = 20.0f; }

            while (unitsSpawned < totalCount)
            {
                int countInThisGroup = Mathf.Min(groupSize, totalCount - unitsSpawned);

                // Sprawdzamy czy mieścimy się w rzędzie
                if (currentXOffset + dynamicBlockSize > usableWidth)
                {
                    currentXOffset = 0;
                    rowStart -= forwardDir * (blockDepth + gapBetweenGroups);
                }

                Vector3 groupCenter = rowStart + (rightDir * (currentXOffset + dynamicBlockSize * 0.5f));
                
                SpawnTightGroupRect(type, countInThisGroup, groupCenter, isFactionA, rightDir, forwardDir);

                currentXOffset += (dynamicBlockSize + gapBetweenGroups);
                unitsSpawned += countInThisGroup;
            }
        }

        SpawnTypeSequence(UnitType.Infantry, army.infantryCount);
        if (army.infantryCount > 0) { currentXOffset = 0; rowStart -= forwardDir * 40.0f; }
        
        SpawnTypeSequence(UnitType.Archer, army.archerCount);
        if (army.archerCount > 0) { currentXOffset = 0; rowStart -= forwardDir * 30.0f; }
        
        SpawnTypeSequence(UnitType.Cavalry, army.cavalryCount);
    }

    void SpawnTightGroupRect(UnitType type, int count, Vector3 center, bool isFactionA, Vector3 rightDir, Vector3 forwardDir)
    {
        float spacingMultiplier = 1.4f;
        float widthRatio = 3.0f;
        float rowSpacingMult = 1.0f;

        if (type == UnitType.Cavalry) { spacingMultiplier = 3.5f; widthRatio = 1.6f; rowSpacingMult = 0.8f; }
        else if (type == UnitType.Archer) { spacingMultiplier = 2.2f; widthRatio = 2.5f; }

        float baseSpacing = Mathf.Max(unitSpacing, 1.0f);
        float spacingX = baseSpacing * spacingMultiplier;
        float spacingZ = spacingX * rowSpacingMult;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count) * widthRatio);
        if (cols < 1) cols = 1;

        Vector3 startOffset = -(rightDir * (cols * spacingX) * 0.5f);

        float mapMinX = terrain.transform.position.x + 15.0f;
        float mapMaxX = terrain.transform.position.x + terrain.terrainData.size.x - 15.0f;
        float mapMinZ = terrain.transform.position.z + 15.0f;
        float mapMaxZ = terrain.transform.position.z + terrain.terrainData.size.z - 15.0f;

        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;

            Vector3 localPos = startOffset + (rightDir * col * spacingX) - (forwardDir * row * spacingZ);
            
            if (randomness > 0)
            {
                 localPos += (rightDir * Random.Range(-randomness, randomness) * 0.2f) +
                             (forwardDir * Random.Range(-randomness, randomness) * 0.2f);
            }

            Vector3 rawPos = center + localPos;
            rawPos.x = Mathf.Clamp(rawPos.x, mapMinX, mapMaxX);
            rawPos.z = Mathf.Clamp(rawPos.z, mapMinZ, mapMaxZ);

            float terrainY = terrain.SampleHeight(rawPos) + terrain.transform.position.y;
            float finalY = terrainY + (unitDiameter * 0.5f);
            if (waterFound && terrainY < waterSurfaceY) finalY = waterSurfaceY - swimmingDepth;

            Vector3 finalPos = new Vector3(rawPos.x, finalY, rawPos.z);
            Quaternion finalRotation = Quaternion.LookRotation(forwardDir);

            Matrix4x4 matrix = Matrix4x4.TRS(finalPos, finalRotation, Vector3.one * unitDiameter);

            if (isFactionA)
            {
                if (type == UnitType.Infantry) listA_Inf.Add(matrix);
                else if (type == UnitType.Archer) listA_Arc.Add(matrix);
                else listA_Cav.Add(matrix);
            }
            else
            {
                if (type == UnitType.Infantry) listB_Inf.Add(matrix);
                else if (type == UnitType.Archer) listB_Arc.Add(matrix);
                else listB_Cav.Add(matrix);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (spawnAreaA != null) DrawSpawnGizmo(spawnAreaA, true, Color.blue);
        if (spawnAreaB != null) DrawSpawnGizmo(spawnAreaB, false, Color.red);
        if (spawnAreaA_Secondary != null) DrawSpawnGizmo(spawnAreaA_Secondary, true, Color.cyan);
        if (spawnAreaB_Secondary != null) DrawSpawnGizmo(spawnAreaB_Secondary, false, Color.magenta);
    }

    void DrawSpawnGizmo(GameObject area, bool isFactionA, Color c)
    {
        Gizmos.color = c;
        Vector3 center = area.transform.position;
        Vector3 forward = area.transform.forward;

        if (useSpawnBoxRotation)
        {
            forward = area.transform.forward;
        }
        else if (!isFactionA && forceEnemyAlignmentToPlayer && area == spawnAreaB && spawnAreaA != null)
        {
             Vector3 playerFwd = spawnAreaA.transform.forward;
             playerFwd.y = 0; playerFwd.Normalize();
             forward = -playerFwd;
        }
        else
        {
             forward = area.transform.forward;
        }

        forward.y = 0; 
        if(forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Gizmos.DrawLine(center, center + forward * 30f);
        Gizmos.DrawSphere(center + forward * 30f, 2f);
        
        // Wizualizacja szerokości
        float vizWidth = 100f;
        if (manualFormationWidth > 10f) vizWidth = manualFormationWidth;
        else
        {
            Vector3 areaSize = area.transform.localScale;
            if (useSmartWidthProjection)
            {
                float projectedWidthX = Mathf.Abs(Vector3.Dot(area.transform.right * areaSize.x, right));
                float projectedWidthZ = Mathf.Abs(Vector3.Dot(area.transform.forward * areaSize.z, right));
                vizWidth = (projectedWidthX + projectedWidthZ) * 0.9f;
            }
            else
            {
                // Simple logic visualization
                float alignmentX = Mathf.Abs(Vector3.Dot(area.transform.right, right));
                vizWidth = (alignmentX > 0.5f) ? areaSize.x : areaSize.z;
                vizWidth *= 0.9f;
            }
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center, center + right * (vizWidth * 0.5f));
        Gizmos.DrawLine(center, center - right * (vizWidth * 0.5f));
    }

    void DetectWater()
    {
        GameObject waterObj = GameObject.FindGameObjectWithTag(waterTag);
        if (waterObj != null)
        {
            waterSurfaceY = waterObj.transform.position.y + (waterObj.transform.localScale.y * 0.5f);
            waterFound = true;
        }
    }

    void LoadCustomSettings()
    {
        bool hasCustomData =
            (BattleInfoDisplayOwnSettings.redInfantryCount > 0 ||
             BattleInfoDisplayOwnSettings.redRangeCount > 0 ||
             BattleInfoDisplayOwnSettings.redCavalryCount > 0 ||
             BattleInfoDisplayOwnSettings.blueInfantryCount > 0 ||
             BattleInfoDisplayOwnSettings.blueRangeCount > 0 ||
             BattleInfoDisplayOwnSettings.blueCavalryCount > 0);

        if (hasCustomData)
        {
            factionA_Army.infantryCount = BattleInfoDisplayOwnSettings.blueInfantryCount;
            factionA_Army.archerCount = BattleInfoDisplayOwnSettings.blueRangeCount;
            factionA_Army.cavalryCount = BattleInfoDisplayOwnSettings.blueCavalryCount;

            factionB_Army.infantryCount = BattleInfoDisplayOwnSettings.redInfantryCount;
            factionB_Army.archerCount = BattleInfoDisplayOwnSettings.redRangeCount;
            factionB_Army.cavalryCount = BattleInfoDisplayOwnSettings.redCavalryCount;
        }
    }

    void SetupCameraPosition()
    {
        if (battleCamera == null) battleCamera = Camera.main;
        if (battleCamera == null) return;

        GameObject mySpawn = spawnAreaA;
        if (mySpawn == null || spawnAreaA == null || spawnAreaB == null) return;

        Vector3 mapCenter = (spawnAreaA.transform.position + spawnAreaB.transform.position) / 2f;
        Vector3 directionToCenter = (mapCenter - mySpawn.transform.position).normalized;
        directionToCenter.y = 0;
        directionToCenter.Normalize();

        Vector3 startPos = mySpawn.transform.position;
        Vector3 newPos = startPos - (directionToCenter * cameraDistanceBack);

        float baseHeight = newPos.y;
        if (terrain != null) baseHeight = terrain.SampleHeight(newPos) + terrain.transform.position.y;

        newPos.y = baseHeight + cameraHeight;
        battleCamera.transform.position = newPos;
        battleCamera.transform.LookAt(mapCenter);

        Vector3 currentRotation = battleCamera.transform.rotation.eulerAngles;
        battleCamera.transform.rotation = Quaternion.Euler(cameraAngle, currentRotation.y, 0f);
    }

    void Update()
    {
        if (listA_Inf.Count > 0) DrawBatches(batchesA_Inf, infantryMesh, factionAMaterial);
        if (listA_Arc.Count > 0) DrawBatches(batchesA_Arc, archerMesh, factionAMaterial);
        if (listA_Cav.Count > 0) DrawBatches(batchesA_Cav, cavalryMesh, factionAMaterial);
        if (listB_Inf.Count > 0) DrawBatches(batchesB_Inf, infantryMesh, factionBMaterial);
        if (listB_Arc.Count > 0) DrawBatches(batchesB_Arc, archerMesh, factionBMaterial);
        if (listB_Cav.Count > 0) DrawBatches(batchesB_Cav, cavalryMesh, factionBMaterial);

        if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
        if (Input.GetKeyDown(KeyCode.C)) KillRandomUnit(false);
    }

    void KillRandomUnit(bool isFactionA)
    {
        var inf = isFactionA ? listA_Inf : listB_Inf;
        var arc = isFactionA ? listA_Arc : listB_Arc;
        var cav = isFactionA ? listA_Cav : listB_Cav;
        List<int> availableTypes = new List<int>();
        if (inf.Count > 0) availableTypes.Add(0);
        if (arc.Count > 0) availableTypes.Add(1);
        if (cav.Count > 0) availableTypes.Add(2);
        if (availableTypes.Count == 0) return;
        int randomTypeIndex = availableTypes[Random.Range(0, availableTypes.Count)];
        if (randomTypeIndex == 0) inf.RemoveAt(inf.Count - 1);
        else if (randomTypeIndex == 1) arc.RemoveAt(arc.Count - 1);
        else cav.RemoveAt(cav.Count - 1);
        if (isFactionA)
        {
            RebuildBatchesForList(listA_Inf, batchesA_Inf);
            RebuildBatchesForList(listA_Arc, batchesA_Arc);
            RebuildBatchesForList(listA_Cav, batchesA_Cav);
        }
        else
        {
            RebuildBatchesForList(listB_Inf, batchesB_Inf);
            RebuildBatchesForList(listB_Arc, batchesB_Arc);
            RebuildBatchesForList(listB_Cav, batchesB_Cav);
        }
        UpdateUICounters();
    }

    void UpdateUICounters()
    {
        int totalA = listA_Inf.Count + listA_Arc.Count + listA_Cav.Count;
        if (factionAText != null) factionAText.text = $"{totalA}";
        int totalB = listB_Inf.Count + listB_Arc.Count + listB_Cav.Count;
        if (factionBText != null) factionBText.text = $"{totalB}";
    }

    public void RebuildAllBatches()
    {
        RebuildBatchesForList(listA_Inf, batchesA_Inf);
        RebuildBatchesForList(listA_Arc, batchesA_Arc);
        RebuildBatchesForList(listA_Cav, batchesA_Cav);
        RebuildBatchesForList(listB_Inf, batchesB_Inf);
        RebuildBatchesForList(listB_Arc, batchesB_Arc);
        RebuildBatchesForList(listB_Cav, batchesB_Cav);
    }

    void RebuildBatchesForList(List<Matrix4x4> source, List<Matrix4x4[]> destinationBatches)
    {
        destinationBatches.Clear();
        for (int i = 0; i < source.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, source.Count - i);
            var batch = new Matrix4x4[count];
            source.CopyTo(i, batch, 0, count);
            destinationBatches.Add(batch);
        }
    }

    void DrawBatches(List<Matrix4x4[]> batches, Mesh mesh, Material mat)
    {
        if (mesh == null || mat == null) return;
        foreach (var batch in batches) Graphics.DrawMeshInstanced(mesh, 0, mat, batch);
    }
}