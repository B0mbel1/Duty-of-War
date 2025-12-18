using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class UnitsSpawnCustom : MonoBehaviour
{
    // --- KONFIGURACJA SK£ADU ARMII ---
    [System.Serializable]
    public class ArmyComposition
    {
        public string name = "Army Settings";
        public int infantryCount = 0;
        public int archerCount = 0;
        public int cavalryCount = 0;
    }

    // Definicja dostêpnych kszta³tów spawnu
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

    // --- NOWOŒÆ: USTAWIENIA WODY DLA SPAWNU ---
    [Header("Water Settings (Spawn Correction)")]
    public string waterTag = "Water";
    public float swimmingDepth = 0.5f;
    // -------------------------------------------

    [Header("Spawn Area Shapes")]
    public SpawnShape factionAShape = SpawnShape.Rectangle;
    public SpawnShape factionBShape = SpawnShape.Rectangle;

    [Header("Camera Start Settings")]
    public Camera battleCamera;
    public float cameraHeight = 60f;
    public float cameraDistanceBack = 40f;
    public float cameraAngle = 55f;

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

    // Zmienne wody
    private float waterSurfaceY;
    private bool waterFound = false;

    void Start()
    {
        if (factionAMaterial) factionAMaterial.enableInstancing = true;
        if (factionBMaterial) factionBMaterial.enableInstancing = true;

        terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        LoadCustomSettings();

        // 1. Wykryj wodê
        DetectWater();

        // 2. Spawnuj
        ProcessFactionSpawns(factionA_Army, spawnAreaA, spawnAreaA_Secondary, true, factionAShape);
        ProcessFactionSpawns(factionB_Army, spawnAreaB, spawnAreaB_Secondary, false, factionBShape);

        // 3. Batche i UI
        RebuildAllBatches();
        UpdateUICounters();

        // 4. Kamera
        SetupCameraPosition();
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

        GameObject mySpawn = spawnAreaA; // Custom = Zawsze Frakcja A

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

    void ProcessFactionSpawns(ArmyComposition army, GameObject mainArea, GameObject secArea, bool isFactionA, SpawnShape shape)
    {
        if (mainArea == null) return;
        mainArea.GetComponent<Renderer>().enabled = false;
        if (secArea != null) secArea.GetComponent<Renderer>().enabled = false;

        System.Random rand = new System.Random();

        if (secArea == null)
        {
            Queue<Vector3> queue = GenerateSafeGrid(mainArea, shape);
            SpawnUnitGroup(UnitType.Infantry, army.infantryCount, queue, rand, isFactionA);
            SpawnUnitGroup(UnitType.Archer, army.archerCount, queue, rand, isFactionA);
            SpawnUnitGroup(UnitType.Cavalry, army.cavalryCount, queue, rand, isFactionA);
        }
        else
        {
            Queue<Vector3> queueMain = GenerateSafeGrid(mainArea, shape);
            Queue<Vector3> queueSec = GenerateSafeGrid(secArea, shape);
            SplitAndSpawn(UnitType.Infantry, army.infantryCount, queueMain, queueSec, rand, isFactionA);
            SplitAndSpawn(UnitType.Archer, army.archerCount, queueMain, queueSec, rand, isFactionA);
            SplitAndSpawn(UnitType.Cavalry, army.cavalryCount, queueMain, queueSec, rand, isFactionA);
        }
    }

    void SplitAndSpawn(UnitType type, int totalCount, Queue<Vector3> q1, Queue<Vector3> q2, System.Random rand, bool isFactionA)
    {
        int count1 = totalCount / 2;
        int count2 = totalCount - count1;
        SpawnUnitGroup(type, count1, q1, rand, isFactionA);
        SpawnUnitGroup(type, count2, q2, rand, isFactionA);
    }

    void SpawnUnitGroup(UnitType type, int count, Queue<Vector3> positionQueue, System.Random rand, bool isFactionA)
    {
        for (int i = 0; i < count; i++)
        {
            if (positionQueue.Count == 0) break;
            Vector3 rawPos = positionQueue.Dequeue();

            // Pobieramy wysokoœæ terenu
            float terrainY = terrain.SampleHeight(rawPos) + terrain.transform.position.y;

            // Domyœlna pozycja (L¹d)
            float finalY = terrainY + (unitDiameter * 0.5f);

            // --- KOREKTA WODNA ---
            if (waterFound)
            {
                if (terrainY < waterSurfaceY)
                {
                    finalY = waterSurfaceY - swimmingDepth;
                }
            }
            // ---------------------

            Vector3 finalPos = new Vector3(rawPos.x, finalY, rawPos.z);
            float rotY = (float)rand.NextDouble() * 360f;
            Quaternion rotation = Quaternion.Euler(0, rotY, 0);
            Matrix4x4 matrix = Matrix4x4.TRS(finalPos, rotation, Vector3.one * unitDiameter);

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

    Queue<Vector3> GenerateSafeGrid(GameObject area, SpawnShape shape)
    {
        List<Vector3> points = new List<Vector3>();
        if (area == null) return new Queue<Vector3>();

        Vector3 center = area.transform.position;
        Vector3 size = area.transform.localScale;

        float radiusX = size.x * 0.5f;
        float radiusZ = size.z * 0.5f;

        if (unitSpacing <= unitDiameter) unitSpacing = unitDiameter + 0.1f;

        int cols = Mathf.FloorToInt(size.x / unitSpacing);
        int rows = Mathf.FloorToInt(size.z / unitSpacing);

        float freeSpace = unitSpacing - unitDiameter;
        float maxJitter = (freeSpace * 0.5f) * randomness;

        Vector3 startPos = center - new Vector3(size.x * 0.5f, 0, size.z * 0.5f);

        for (int x = 0; x < cols; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                float posX = startPos.x + (x * unitSpacing) + (unitSpacing * 0.5f);
                float posZ = startPos.z + (z * unitSpacing) + (unitSpacing * 0.5f);

                float offX = Random.Range(-maxJitter, maxJitter);
                float offZ = Random.Range(-maxJitter, maxJitter);

                Vector3 candidatePos = new Vector3(posX + offX, 0, posZ + offZ);

                if (shape == SpawnShape.Ellipse)
                {
                    float dx = candidatePos.x - center.x;
                    float dz = candidatePos.z - center.z;
                    float distNormalized = (dx * dx) / (radiusX * radiusX) + (dz * dz) / (radiusZ * radiusZ);
                    if (distNormalized > 1.0f) continue;
                }

                points.Add(candidatePos);
            }
        }

        var rand = new System.Random();
        int n = points.Count;
        while (n > 1) { n--; int k = rand.Next(n + 1); Vector3 value = points[k]; points[k] = points[n]; points[n] = value; }

        return new Queue<Vector3>(points);
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