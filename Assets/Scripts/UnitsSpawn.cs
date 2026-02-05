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
        ProcessFactionSpawns(factionA_Army, spawnAreaA, spawnAreaA_Secondary, true);
        ProcessFactionSpawns(factionB_Army, spawnAreaB, spawnAreaB_Secondary, false);

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

    void ProcessFactionSpawns(ArmyComposition army, GameObject mainArea, GameObject secArea, bool isFactionA)
    {
        if (mainArea == null) return;
        mainArea.GetComponent<Renderer>().enabled = false;
        if (secArea != null) secArea.GetComponent<Renderer>().enabled = false;

        // Cel patrzenia
        Vector3 enemyTargetPos = isFactionA ? spawnAreaB.transform.position : spawnAreaA.transform.position;

        Vector3 startPos = mainArea.transform.position;
        Vector3 areaSize = mainArea.transform.localScale;

        // --- POPRAWKA 1: WYMUSZENIE MINIMALNEJ SZEROKOŚCI ---
        // Jeśli Cube jest mały (np. marker), uznajemy że mamy do dyspozycji 500m szerokości
        float usableWidth = Mathf.Max(areaSize.x * 0.9f, 500.0f);

        // --- POPRAWKA 2: KIERUNEK UKŁADANIA ---
        // Frakcja A (Dół): Zaczynamy od górnej krawędzi spawnu (najbliżej wroga) i cofamy się (Z-)
        // Frakcja B (Góra): Zaczynamy od dolnej krawędzi spawnu (najbliżej wroga) i cofamy się (Z+) w głąb bazy
        float zDirection = isFactionA ? -1.0f : 1.0f;

        // Punkt startowy (Front Center spawnu)
        // Zamiast lewego rogu, zacznijmy bardziej od środka lewej strony
        float startZ = isFactionA ? (startPos.z + areaSize.z * 0.4f) : (startPos.z - areaSize.z * 0.4f);
        // Startujemy od lewej krawędzi (X)
        Vector3 cursor = new Vector3(startPos.x - (usableWidth * 0.5f), startPos.y, startZ);

        float gapBetweenGroups = 25.0f;
        float currentXOffset = 0f;

        // Kopia kursora dla rzędów
        Vector3 rowStart = cursor;

        void SpawnTypeSequence(UnitType type, int totalCount)
        {
            int groupSize = 150;
            int unitsSpawned = 0;

            // --- KONFIGURACJA ODSTĘPÓW ---

            // 1. WYMIARY POJEDYNCZEGO ODDZIAŁU
            float groupWidth = 40.0f;  // Ile miejsca zajmuje na szerokość
            float groupDepth = 20.0f;  // Ile miejsca zajmuje na głębokość (To decyduje o odległości linii!)

            // 2. PRZERWY MIĘDZY ODDZIAŁAMI
            float horizontalGap = 5.0f; // Odstęp między oddziałami w tym samym rzędzie (lewo-prawo)
            float verticalGap = 2.0f;   // Odstęp między liniami oddziałów (góra-dół)

            // --- SPECJALNE USTAWIENIA DLA JAZDY ---
            if (type == UnitType.Cavalry)
            {
                // SZEROKOŚĆ: Zmniejszamy, bo oddziały są wąskie
                groupWidth = 35.0f;

                // ODSTĘP POZIOMY: Zwiększamy, żeby oddziały były odseparowane
                horizontalGap = 25.0f;

                // GŁĘBOKOŚĆ (To naprawi Twój problem z odległością linii):
                // Ustawiamy mało (np. 15m), żeby następny rząd zaczynał się blisko pleców poprzedniego.
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

                // Sprawdzamy, czy mieścimy się w poziomie
                if (currentXOffset + groupWidth > usableWidth)
                {
                    currentXOffset = 0;

                    // --- TWORZENIE NOWEJ LINII ---
                    // Przesuwamy się w głąb tylko o (głębokość oddziału + mała przerwa)
                    rowStart.z += (groupDepth + verticalGap) * zDirection;
                }

                Vector3 groupCenter = rowStart + new Vector3(currentXOffset, 0, 0);

                SpawnTightGroupRect(type, countInThisGroup, groupCenter, isFactionA, enemyTargetPos);

                // Przesuwamy w bok o (szerokość oddziału + duża przerwa pozioma)
                currentXOffset += (groupWidth + horizontalGap);
                unitsSpawned += countInThisGroup;
            }
        }

        // Kolejność linii bojowych (od frontu do tyłu)

        // 1. LINIA: Piechota
        SpawnTypeSequence(UnitType.Infantry, army.infantryCount);

        // Odstęp między liniami
        currentXOffset = 0;
        rowStart.z += (30.0f) * zDirection;

        // 2. LINIA: Łucznicy (za piechotą)
        SpawnTypeSequence(UnitType.Archer, army.archerCount);

        // Odstęp
        currentXOffset = 0;
        rowStart.z += (20.0f) * zDirection;

        // 3. LINIA: Jazda (na tyłach lub skrzydłach - tu dajemy na tyłach szeroko)
        SpawnTypeSequence(UnitType.Cavalry, army.cavalryCount);
    }

    // --- DODAJ TO DO UnitsSpawn.cs ---
    void SpawnTightGroupRect(UnitType type, int count, Vector3 center, bool isFactionA, Vector3 lookTarget)
    {
        // 1. JESZCZE WIĘKSZY LUZ (Drastyczne zwiększenie odstępów)
        float spacingMultiplier = 1.4f; // Piechota luźniej
        float widthRatio = 2.5f;
        float rowSpacingMultiplier = 1.0f;

        if (type == UnitType.Cavalry)
        {
            spacingMultiplier = 2.0f; // Odstęp: Większy niż piechota, ale mniejszy niż poprzednio (było 6.0)
            widthRatio = 2.2f;        // Bardzo szeroki szereg (szarża)
            rowSpacingMultiplier = 0.6f;
        }
        else if (type == UnitType.Archer)
        {
            spacingMultiplier = 1.4f; // Łucznicy luźno
            widthRatio = 2.5f;
        }

        // Zabezpieczenie: jeśli unitSpacing jest ustawiony na 0 w inspectorze
        float baseSpacing = Mathf.Max(unitSpacing, 1.0f);
        float spacingX = baseSpacing * spacingMultiplier;

        // Obliczamy odstęp rzędów (na głębokość)
        float spacingZ = spacingX * rowSpacingMultiplier;

        // Obliczamy liczbę kolumn
        int cols = Mathf.CeilToInt(Mathf.Sqrt(count) * widthRatio);

        // Centrowanie (tylko w osi X, bo Z rośnie w tył)
        Vector3 startOffset = new Vector3(-(cols * spacingX) / 2f, 0, 0);

        // --- GRANICE MAPY ---
        float mapMinX = terrain.transform.position.x + 20.0f;
        float mapMaxX = terrain.transform.position.x + terrain.terrainData.size.x - 20.0f;
        float mapMinZ = terrain.transform.position.z + 20.0f;
        float mapMaxZ = terrain.transform.position.z + terrain.terrainData.size.z - 20.0f;

        for (int i = 0; i < count; i++)
        {
            int x = i % cols;
            int z = i / cols;

            // Tworzymy pozycję lokalną
            Vector3 localPos = startOffset + new Vector3(x * spacingX, 0, -z * spacingZ);
            Vector3 rawPos = center + localPos;

            // Clamp do mapy
            rawPos.x = Mathf.Clamp(rawPos.x, mapMinX, mapMaxX);
            rawPos.z = Mathf.Clamp(rawPos.z, mapMinZ, mapMaxZ);

            // Wysokość
            float terrainY = terrain.SampleHeight(rawPos) + terrain.transform.position.y;
            float finalY = terrainY + (unitDiameter * 0.5f);

            if (waterFound && terrainY < waterSurfaceY)
            {
                finalY = waterSurfaceY - swimmingDepth;
            }

            Vector3 finalPos = new Vector3(rawPos.x, finalY, rawPos.z);

            // Obrót w stronę wroga
            Vector3 directionToEnemy = (lookTarget - finalPos).normalized;
            directionToEnemy.y = 0;
            if (directionToEnemy == Vector3.zero) directionToEnemy = Vector3.forward;
            Quaternion finalRotation = Quaternion.LookRotation(directionToEnemy);

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