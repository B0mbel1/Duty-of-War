using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class UnitsSpawn : MonoBehaviour
{
    // --- KONFIGURACJA SK�ADU ARMII ---
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
    public GameObject spawnAreaA_Secondary;
    public GameObject spawnAreaB_Secondary;

    [Header("Grid Settings")]
    public float unitDiameter = 1.0f;
    public float unitSpacing = 1.5f;
    [Range(0f, 1f)]
    public float randomness = 0.8f;

    // --- NOWO��: USTAWIENIA WODY DLA SPAWNU ---
    [Header("Water Settings (Spawn Correction)")]
    public string waterTag = "Water";
    public float swimmingDepth = 0.5f; // Musi by� takie samo jak w Managerze
    // -------------------------------------------

    [Header("Camera Start Settings")]
    public Camera battleCamera;
    public float cameraHeight = 60f;
    public float cameraDistanceBack = 40f;
    public float cameraAngle = 55f;

    // --- DANE JEDNOSTEK ---
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

        // 1. Wykryj wod� PRZED spawnowaniem
        DetectWater();

        // 2. Spawnowanie
        HandleFactionSpawning(factionA_Army, spawnAreaA, spawnAreaA_Secondary, true);
        HandleFactionSpawning(factionB_Army, spawnAreaB, spawnAreaB_Secondary, false);

        // 3. Batche
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
            // Zak�adamy p�ask� tafl� wody (proste rozwi�zanie dla spawnu)
            waterSurfaceY = waterObj.transform.position.y + (waterObj.transform.localScale.y * 0.5f);
            waterFound = true;
        }
    }

    void SetupCameraPosition()
    {
        if (battleCamera == null) battleCamera = Camera.main;
        if (battleCamera == null) return;

        string chosenSide = PlayerPrefs.GetString("PlayerSide", "SideA");
        GameObject mySpawn = (chosenSide == "SideA") ? spawnAreaA : spawnAreaB;

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
        DrawBatches(batchesA_Inf, infantryMesh, factionAMaterial);
        DrawBatches(batchesA_Arc, archerMesh, factionAMaterial);
        DrawBatches(batchesA_Cav, cavalryMesh, factionAMaterial);
        DrawBatches(batchesB_Inf, infantryMesh, factionBMaterial);
        DrawBatches(batchesB_Arc, archerMesh, factionBMaterial);
        DrawBatches(batchesB_Cav, cavalryMesh, factionBMaterial);

        if (Input.GetKeyDown(KeyCode.X)) KillRandomUnit(true);
        if (Input.GetKeyDown(KeyCode.C)) KillRandomUnit(false);
    }

    void HandleFactionSpawning(ArmyComposition fullArmy, GameObject mainArea, GameObject secArea, bool isFactionA)
    {
        if (mainArea == null) return;

        // Ukryj renderery stref spawnu
        mainArea.GetComponent<Renderer>().enabled = false;
        if (secArea != null) secArea.GetComponent<Renderer>().enabled = false;

        // Cel patrzenia (zawsze główny spawn wroga)
        GameObject enemyMainSpawn = isFactionA ? spawnAreaB : spawnAreaA;
        // Jeśli nie ma spawnu wroga, patrz w stronę środka mapy (0,0,0) jako fallback
        Vector3 enemyPos = enemyMainSpawn != null ? enemyMainSpawn.transform.position : Vector3.zero;

        if (secArea != null)
        {
            // --- RĘCZNE DZIELENIE ARMII ---
            // Tworzymy tymczasowe obiekty składu armii dla obu stref
            
            // Armia 1 (Połowa sił)
            ArmyComposition army1 = new ArmyComposition();
            army1.name = fullArmy.name + "_Part1";
            army1.infantryCount = fullArmy.infantryCount / 2;
            army1.archerCount = fullArmy.archerCount / 2;
            army1.cavalryCount = fullArmy.cavalryCount / 2;

            // Armia 2 (Reszta sił - odejmujemy, żeby nie zgubić nieparzystych jednostek)
            ArmyComposition army2 = new ArmyComposition();
            army2.name = fullArmy.name + "_Part2";
            army2.infantryCount = fullArmy.infantryCount - army1.infantryCount;
            army2.archerCount = fullArmy.archerCount - army1.archerCount;
            army2.cavalryCount = fullArmy.cavalryCount - army1.cavalryCount;

            // Spawnuj w obu strefach
            ProcessFactionSpawns(army1, mainArea, isFactionA, enemyPos);
            ProcessFactionSpawns(army2, secArea, isFactionA, enemyPos);
        }
        else
        {
            // Spawnuj całość w głównej strefie
            ProcessFactionSpawns(fullArmy, mainArea, isFactionA, enemyPos);
        }
    }

    void ProcessFactionSpawns(ArmyComposition army, GameObject spawnArea, bool isFactionA, Vector3 enemyTargetPos)
    {
        Vector3 areaCenter = spawnArea.transform.position;
        Vector3 areaSize = spawnArea.transform.localScale;

        // Obliczamy wektor "DO PRZODU" (w stronę wroga)
        Vector3 forwardDir = (enemyTargetPos - areaCenter).normalized;
        forwardDir.y = 0;
        forwardDir.Normalize();

        if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;

        // Obliczamy wektor "W PRAWO" (prostopadły do przodu)
        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

        // Ustalamy szerokość dostępną dla formacji (rzutujemy wymiary Area na nasz wektor rightDir)
        // Dla uproszczenia bierzemy większy wymiar boxa jako szerokość, jeśli box jest "płaski"
        // Ale bezpieczniej przyjąć "szerokość logiczną"
        float usableWidth = Mathf.Max(areaSize.x, areaSize.z) * 0.9f;
        if (usableWidth < 100) usableWidth = 500f; // Fallback dla małych markerów

        // Punkt startowy: Front boxa (najbliżej wroga)
        // Przesuwamy się od środka boxa w stronę wroga o połowę "głębokości" boxa wzdłuż osi Forward
        // Tutaj prosta aproksymacja: startujemy po prostu od środka obszaru + lekki offset w stronę wroga
        // Żeby było idealnie na krawędzi, trzeba by rzutować boxa, ale dla uproszczenia:
        Vector3 frontLineCenter = areaCenter + (forwardDir * (Mathf.Min(areaSize.x, areaSize.z) * 0.4f));

        // Zaczynamy układać od lewej strony (patrząc w stronę wroga)
        Vector3 rowStart = frontLineCenter - (rightDir * (usableWidth * 0.5f));

        float gapBetweenGroups = 25.0f;
        float currentXOffset = 0f; // Przesunięcie wzdłuż wektora rightDir

        void SpawnTypeSequence(UnitType type, int totalCount)
        {
            int groupSize = 150;
            int unitsSpawned = 0;

            float groupWidth = 40.0f;
            float groupDepth = 20.0f;
            float horizontalGap = 5.0f;
            float verticalGap = 2.0f;

            if (type == UnitType.Cavalry)
            {
                groupWidth = 35.0f;
                horizontalGap = 25.0f;
                groupDepth = 15.0f;
            }
            else if (type == UnitType.Archer)
            {
                groupWidth = 55.0f;
                groupDepth = 20.0f;
                horizontalGap = 5.0f;
            }

            while (unitsSpawned < totalCount)
            {
                int countInThisGroup = Mathf.Min(groupSize, totalCount - unitsSpawned);

                // Czy mieścimy się w szerokości?
                if (currentXOffset + groupWidth > usableWidth)
                {
                    currentXOffset = 0;
                    // Nowa linia - cofamy się (przeciwnie do forwardDir)
                    rowStart -= forwardDir * (groupDepth + verticalGap);
                }

                // Środek grupy to: StartRzędu + (W Prawo * offset)
                Vector3 groupCenter = rowStart + (rightDir * currentXOffset);

                // Ważne: Przekazujemy forwardDir jako kierunek patrzenia grupy
                SpawnTightGroupRect(type, countInThisGroup, groupCenter, isFactionA, enemyTargetPos, rightDir, forwardDir);

                currentXOffset += (groupWidth + horizontalGap);
                unitsSpawned += countInThisGroup;
            }
        }

        // 1. LINIA: Piechota
        SpawnTypeSequence(UnitType.Infantry, army.infantryCount);

        // Odstęp między liniami (cofamy się)
        currentXOffset = 0;
        rowStart -= forwardDir * 30.0f;

        // 2. LINIA: Łucznicy
        SpawnTypeSequence(UnitType.Archer, army.archerCount);

        // Odstęp
        currentXOffset = 0;
        rowStart -= forwardDir * 20.0f;

        // 3. LINIA: Jazda
        SpawnTypeSequence(UnitType.Cavalry, army.cavalryCount);
    }

    // --- DODAJ TO DO UnitsSpawn.cs ---
    void SpawnTightGroupRect(UnitType type, int count, Vector3 center, bool isFactionA, Vector3 lookTarget, Vector3 rightDir, Vector3 forwardDir)
    {
        float spacingMultiplier = 1.4f;
        float widthRatio = 2.5f;
        float rowSpacingMultiplier = 1.0f;

        if (type == UnitType.Cavalry)
        {
            spacingMultiplier = 2.0f;
            widthRatio = 2.2f;
            rowSpacingMultiplier = 0.6f;
        }
        else if (type == UnitType.Archer)
        {
            spacingMultiplier = 1.4f;
            widthRatio = 2.5f;
        }

        float baseSpacing = Mathf.Max(unitSpacing, 1.0f);
        float spacingX = baseSpacing * spacingMultiplier;
        float spacingZ = spacingX * rowSpacingMultiplier;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count) * widthRatio);

        // Offset startowy wewnątrz grupy (żeby center był na środku)
        // Przesuwamy w lewo o połowę szerokości
        Vector3 startOffset = -(rightDir * (cols * spacingX) / 2f);

        // Granice mapy
        float mapMinX = terrain.transform.position.x + 20.0f;
        float mapMaxX = terrain.transform.position.x + terrain.terrainData.size.x - 20.0f;
        float mapMinZ = terrain.transform.position.z + 20.0f;
        float mapMaxZ = terrain.transform.position.z + terrain.terrainData.size.z - 20.0f;

        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;

            // Pozycja jednostki: Start + (Prawo * kolumna) - (Przód * rząd) -> bo rzędy rosną w głąb (do tyłu)
            Vector3 localPos = startOffset + (rightDir * col * spacingX) - (forwardDir * row * spacingZ);
            Vector3 rawPos = center + localPos;

            // Clamp do mapy
            rawPos.x = Mathf.Clamp(rawPos.x, mapMinX, mapMaxX);
            rawPos.z = Mathf.Clamp(rawPos.z, mapMinZ, mapMaxZ);

            float terrainY = terrain.SampleHeight(rawPos) + terrain.transform.position.y;
            float finalY = terrainY + (unitDiameter * 0.5f);

            if (waterFound && terrainY < waterSurfaceY)
            {
                finalY = waterSurfaceY - swimmingDepth;
            }

            Vector3 finalPos = new Vector3(rawPos.x, finalY, rawPos.z);

            // Obrót w stronę wroga (globalny dla grupy)
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

    void SplitAndSpawn(UnitType type, int totalCount, Queue<Vector3> q1, Queue<Vector3> q2, System.Random rand, bool isFactionA, Vector3 lookTarget)
    {
        int count1 = totalCount / 2;
        int count2 = totalCount - count1;
        SpawnUnitGroup(type, count1, q1, rand, isFactionA, lookTarget);
        SpawnUnitGroup(type, count2, q2, rand, isFactionA, lookTarget);
    }

    void SpawnUnitGroup(UnitType type, int count, Queue<Vector3> positionQueue, System.Random rand, bool isFactionA, Vector3 lookTarget)
    {
        for (int i = 0; i < count; i++)
        {
            if (positionQueue.Count == 0) break;
            Vector3 rawPos = positionQueue.Dequeue();

            float terrainY = terrain.SampleHeight(rawPos) + terrain.transform.position.y;
            float finalY = terrainY + (unitDiameter * 0.5f);

            if (waterFound)
            {
                if (terrainY < waterSurfaceY)
                {
                    finalY = waterSurfaceY - swimmingDepth;
                }
            }

            Vector3 finalPos = new Vector3(rawPos.x, finalY, rawPos.z);

            // --- NOWOŚĆ: Obliczanie rotacji w stronę wroga ---
            Vector3 directionToEnemy = (lookTarget - finalPos).normalized;
            directionToEnemy.y = 0; // Ignoruj wysokość, żeby nie pochylali się w dół/górę

            if (directionToEnemy == Vector3.zero) directionToEnemy = Vector3.forward;

            Quaternion baseRotation = Quaternion.LookRotation(directionToEnemy);

            // Dodajemy mały losowy odchył (jitter), np. +/- 10 stopni, żeby nie wyglądali jak roboty
            // float randomJitter = (float)(rand.NextDouble() * 20.0 - 10.0); 
            Quaternion finalRotation = baseRotation; // CZYSTA ROTACJA            // -----------------------------------------------

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