using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class UnitsSpawn : MonoBehaviour
{
    // --- KONFIGURACJA SK£ADU ARMII ---
    [System.Serializable]
    public class ArmyComposition
    {
        public string name = "Army Settings";
        public int infantryCount = 5000;
        public int archerCount = 3000;
        public int cavalryCount = 1000;
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
    [Tooltip("Opcjonalny drugi spawn. Jeœli przypisany, armia podzieli siê 50/50 miêdzy g³ówny a ten obszar.")]
    public GameObject spawnAreaA_Secondary;
    public GameObject spawnAreaB_Secondary;

    [Header("Grid Settings")]
    public float unitDiameter = 1.0f;
    public float unitSpacing = 1.5f;
    [Range(0f, 1f)]
    public float randomness = 0.8f;

    // --- DANE JEDNOSTEK ---
    private List<Matrix4x4> listA_Inf = new List<Matrix4x4>();
    private List<Matrix4x4> listA_Arc = new List<Matrix4x4>();
    private List<Matrix4x4> listA_Cav = new List<Matrix4x4>();

    private List<Matrix4x4> listB_Inf = new List<Matrix4x4>();
    private List<Matrix4x4> listB_Arc = new List<Matrix4x4>();
    private List<Matrix4x4> listB_Cav = new List<Matrix4x4>();

    // Batche do renderowania
    private List<Matrix4x4[]> batchesA_Inf = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesA_Arc = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesA_Cav = new List<Matrix4x4[]>();

    private List<Matrix4x4[]> batchesB_Inf = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesB_Arc = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> batchesB_Cav = new List<Matrix4x4[]>();

    private Terrain terrain;
    private const int batchSize = 1023;
    enum UnitType { Infantry, Archer, Cavalry }

    void Start()
    {
        if (factionAMaterial) factionAMaterial.enableInstancing = true;
        if (factionBMaterial) factionBMaterial.enableInstancing = true;

        terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        // 1. Logika Spawnowania Frakcji (Obs³uga 1 lub 2 spawnów)
        ProcessFactionSpawns(factionA_Army, spawnAreaA, spawnAreaA_Secondary, true);
        ProcessFactionSpawns(factionB_Army, spawnAreaB, spawnAreaB_Secondary, false);

        // 2. Budowanie batchy (GPU) i aktualizacja UI
        RebuildAllBatches();
        UpdateUICounters();
    }

    void Update()
    {
        // --- RENDEROWANIE ---
        DrawBatches(batchesA_Inf, infantryMesh, factionAMaterial);
        DrawBatches(batchesA_Arc, archerMesh, factionAMaterial);
        DrawBatches(batchesA_Cav, cavalryMesh, factionAMaterial);

        DrawBatches(batchesB_Inf, infantryMesh, factionBMaterial);
        DrawBatches(batchesB_Arc, archerMesh, factionBMaterial);
        DrawBatches(batchesB_Cav, cavalryMesh, factionBMaterial);

        // --- TESTOWANIE (KILL SWITCH) ---
        if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
        if (Input.GetKeyDown(KeyCode.C)) KillRandomUnit(false);
    }

    // --- NOWA LOGIKA DISTRYBUCJI ---
    // Ta funkcja decyduje czy spawnujemy wszystko w jednym miejscu, czy dzielimy na pó³
    void ProcessFactionSpawns(ArmyComposition army, GameObject mainArea, GameObject secArea, bool isFactionA)
    {
        if (mainArea == null) return;

        // Ukrywamy renderery (¿eby nie by³o widaæ boxów w grze)
        mainArea.GetComponent<Renderer>().enabled = false;
        if (secArea != null) secArea.GetComponent<Renderer>().enabled = false;

        System.Random rand = new System.Random();

        // SCENARIUSZ 1: Tylko jeden spawn (standardowy)
        if (secArea == null)
        {
            Queue<Vector3> queue = GenerateSafeGrid(mainArea);

            SpawnUnitGroup(UnitType.Infantry, army.infantryCount, queue, rand, isFactionA);
            SpawnUnitGroup(UnitType.Archer, army.archerCount, queue, rand, isFactionA);
            SpawnUnitGroup(UnitType.Cavalry, army.cavalryCount, queue, rand, isFactionA);
        }
        // SCENARIUSZ 2: Dwa spawny (dzielimy armiê 50/50)
        else
        {
            Queue<Vector3> queueMain = GenerateSafeGrid(mainArea);
            Queue<Vector3> queueSec = GenerateSafeGrid(secArea);

            // Pomocnicza funkcja do dzielenia i spawnowania
            SplitAndSpawn(UnitType.Infantry, army.infantryCount, queueMain, queueSec, rand, isFactionA);
            SplitAndSpawn(UnitType.Archer, army.archerCount, queueMain, queueSec, rand, isFactionA);
            SplitAndSpawn(UnitType.Cavalry, army.cavalryCount, queueMain, queueSec, rand, isFactionA);
        }
    }

    // Funkcja dziel¹ca konkretny oddzia³ na pó³ i wysy³aj¹ca do dwóch kolejek
    void SplitAndSpawn(UnitType type, int totalCount, Queue<Vector3> q1, Queue<Vector3> q2, System.Random rand, bool isFactionA)
    {
        // Dzielenie na pó³ (zabezpieczenie dla liczb nieparzystych)
        int count1 = totalCount / 2;
        int count2 = totalCount - count1; // Reszta idzie do drugiego, wiêc nic nie tracimy

        SpawnUnitGroup(type, count1, q1, rand, isFactionA);
        SpawnUnitGroup(type, count2, q2, rand, isFactionA);
    }

    // --- FUNKCJA SPAWNUJ¥CA (Bez zmian w logice, tylko wywo³ywana z odpowiednimi liczbami) ---
    void SpawnUnitGroup(UnitType type, int count, Queue<Vector3> positionQueue, System.Random rand, bool isFactionA)
    {
        for (int i = 0; i < count; i++)
        {
            if (positionQueue.Count == 0)
            {
                Debug.LogWarning($"Brakuje miejsca w strefie spawnu dla {type} frakcji {(isFactionA ? "A" : "B")}");
                break;
            }

            Vector3 rawPos = positionQueue.Dequeue();
            float y = terrain.SampleHeight(rawPos) + terrain.transform.position.y + (unitDiameter * 0.5f);
            Vector3 finalPos = new Vector3(rawPos.x, y, rawPos.z);

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

    // --- GENEROWANIE SIATKI (Bez zmian) ---
    Queue<Vector3> GenerateSafeGrid(GameObject area)
    {
        List<Vector3> points = new List<Vector3>();
        if (area == null) return new Queue<Vector3>();

        Vector3 center = area.transform.position;
        Vector3 size = area.transform.localScale;

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
                points.Add(new Vector3(posX + offX, 0, posZ + offZ));
            }
        }

        var rand = new System.Random();
        int n = points.Count;
        while (n > 1) { n--; int k = rand.Next(n + 1); Vector3 value = points[k]; points[k] = points[n]; points[n] = value; }

        return new Queue<Vector3>(points);
    }

    // --- LOGIKA ZABIJANIA (Bez zmian) ---
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

    void RebuildAllBatches()
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