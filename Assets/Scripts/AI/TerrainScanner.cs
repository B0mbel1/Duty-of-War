using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TerrainScanner : MonoBehaviour
{
    [Header("Dostosowanie Punktów (Hitbox)")]
    [Tooltip("Przesuwa punkt z krawędzi w głąb lasu. 0.0 = Krawędź, 0.5 = Środek lasu.")]
    [Range(0.0f, 0.5f)] public float edgeInset = 0.15f;

    [Header("Ustawienia Ogólne")]
    public Terrain terrain;
    public GameObject markerPrefab;

    [Header("Wykrywanie Wzgórz")]
    public float heightThreshold = 20.0f;
    public float minDistanceBetweenPeaks = 50.0f;

    [Header("Wykrywanie Lasów")]
    public string forestLayerName = "Forest"; // [cite: 83]
    [Range(0.1f, 1f)] public float forestDensityThreshold = 0.5f;
    public int scanResolutionDivisor = 4;
    public int minForestSize = 50;

    [Header("Wykrywanie Wody (Rzeki)")]
    public string waterLayerName = "Water"; // Nazwa warstwy texture w Terrain
    public float riverWidthMultiplier = 0.4f;

    // Lista użytych nazw, aby uniknąć duplikatów
    private HashSet<string> usedNames = new HashSet<string>();

    class ForestCluster
    {
        public List<Vector3> points = new List<Vector3>();
        public float minX = float.MaxValue, maxX = float.MinValue;
        public float minZ = float.MaxValue, maxZ = float.MinValue;

        public void AddPoint(Vector3 p)
        {
            points.Add(p);
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }

        public Vector3 GetCenter()
        {
            if (points.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var p in points) sum += p;
            return sum / points.Count;
        }
    }

    void Start()
    {
        // ZMIANA: Sprawdzamy, czy punkty już istnieją na scenie (np. ustawione ręcznie).
        // Jeśli tak - nie robimy nic, zostawiamy Twoje ustawienie.
        var existingFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        if (existingFeatures.Length > 0)
        {
            Debug.Log("TerrainScanner: Znaleziono istniejące punkty. Pomijam generowanie.");
            return;
        }

        // Jeśli punktów nie ma, generujemy je automatycznie (fallback)
        GenerateAllFeatures();
    }

    // --- PRZYCISK W INSPECTORZE (Prawy przycisk myszy na skrypt -> Skanuj Teren Teraz) ---
    [ContextMenu("Skanuj Teren Teraz")]
    public void GenerateAllFeatures()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        // Czyścimy stare znaczniki "Auto_" (tylko te wygenerowane przez skrypt)
        // Dzięki temu możesz wygenerować na nowo, jeśli coś popsujesz, ale Twoje ręczne (jeśli zmieniłeś nazwę) zostaną
        var oldFeatures = FindObjectsByType<TerrainFeature>(FindObjectsSortMode.None);
        foreach (var f in oldFeatures)
        {
            if (f.name.StartsWith("Auto_"))
            {
                // DestroyImmediate jest wymagane w trybie Edytora (nie Play Mode)
                if (Application.isPlaying) Destroy(f.gameObject);
                else DestroyImmediate(f.gameObject);
            }
        }

        usedNames.Clear();
        ScanForHills_Improved();
        ScanForForests_Clustered();
        ScanForWater_Clustered();

        Debug.Log("Skanowanie zakończone. Możesz teraz przesunąć punkty ręcznie.");
    }

    void ScanForWater_Clustered()
    {
        TerrainData data = terrain.terrainData;
        int alphaW = data.alphamapWidth;
        int alphaH = data.alphamapHeight;

        int waterIdx = -1;
        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            // Szukamy warstwy wody
            if (data.terrainLayers[i].name == waterLayerName)
            {
                waterIdx = i;
                break;
            }
        }

        if (waterIdx == -1) return; // Brak wody na mapie

        float[,,] maps = data.GetAlphamaps(0, 0, alphaW, alphaH);
        bool[,] visited = new bool[alphaW, alphaH];
        List<ForestCluster> rivers = new List<ForestCluster>(); // Używamy tej samej klasy co lasy

        int step = scanResolutionDivisor;

        for (int x = 0; x < alphaW; x += step)
        {
            for (int y = 0; y < alphaH; y += step)
            {
                if (visited[x, y]) continue;
                if (maps[y, x, waterIdx] > 0.5f) // Jeśli to woda
                {
                    ForestCluster cluster = PerformFloodFill(x, y, alphaW, alphaH, maps, waterIdx, visited, step);
                    if (cluster.points.Count > minForestSize) rivers.Add(cluster);
                }
            }
        }

        foreach (var river in rivers)
        {
            GenerateRiverMarkers(river);
        }
    }

    void GenerateRiverMarkers(ForestCluster river)
    {
        Vector3 center = river.GetCenter();
        string dirName = GetLocationName(center);
        string baseID = GetUniqueName("River_" + dirName);

        float widthX = river.maxX - river.minX;
        float depthZ = river.maxZ - river.minZ;

        // Rzeka zazwyczaj płynie wzdłuż dłuższej krawędzi
        Vector3 orientation = (widthX > depthZ) ? Vector3.right : Vector3.forward;
        float length = (widthX > depthZ) ? widthX : depthZ;

        // Tworzymy znacznik
        // WAŻNE: Ostatni parametr to isRiver = true
        CreateMarker(center, baseID, "Rzeka", Color.blue, length * riverWidthMultiplier, orientation, true);
    }
    // --- WZGÓRZA ---
    void ScanForHills_Improved()
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 size = data.size;

        List<Vector3> candidates = new List<Vector3>();
        int step = 10;

        for (int x = 0; x < res; x += step)
        {
            for (int y = 0; y < res; y += step)
            {
                float h = heights[y, x] * size.y;
                if (h > heightThreshold)
                {
                    Vector3 worldPos = new Vector3(
                        (x / (float)res) * size.x + terrain.transform.position.x,
                        h + terrain.transform.position.y,
                        (y / (float)res) * size.z + terrain.transform.position.z
                    );
                    candidates.Add(worldPos);
                }
            }
        }

        candidates = candidates.OrderByDescending(v => v.y).ToList();
        List<Vector3> finalPeaks = new List<Vector3>();

        foreach (var point in candidates)
        {
            if (IsFarEnough(point, finalPeaks, minDistanceBetweenPeaks))
            {
                finalPeaks.Add(point);
                string dirName = GetLocationName(point);
                string uniqueName = GetUniqueName("Hill_" + dirName);

                CreateMarker(point, uniqueName, $"Wzgórze ({TranslateDir(dirName)})", Color.yellow);
            }
        }
    }

    // --- LASY (Zachowana Twoja logika klastrów) ---
    void ScanForForests_Clustered()
    {
        TerrainData data = terrain.terrainData;
        int alphaW = data.alphamapWidth;
        int alphaH = data.alphamapHeight;

        int forestIdx = -1;
        for (int i = 0; i < data.terrainLayers.Length; i++)
        {
            if (data.terrainLayers[i].name == forestLayerName)
            {
                forestIdx = i;
                break;
            }
        }

        if (forestIdx == -1)
        {
            Debug.LogWarning($"TerrainScanner: Nie znaleziono warstwy '{forestLayerName}'!");
            return;
        }

        float[,,] maps = data.GetAlphamaps(0, 0, alphaW, alphaH);
        bool[,] visited = new bool[alphaW, alphaH];
        List<ForestCluster> forests = new List<ForestCluster>();

        int step = scanResolutionDivisor;

        for (int x = 0; x < alphaW; x += step)
        {
            for (int y = 0; y < alphaH; y += step)
            {
                if (visited[x, y]) continue;

                if (maps[y, x, forestIdx] > forestDensityThreshold)
                {
                    ForestCluster cluster = PerformFloodFill(x, y, alphaW, alphaH, maps, forestIdx, visited, step);
                    if (cluster.points.Count > minForestSize)
                    {
                        forests.Add(cluster);
                    }
                }
            }
        }

        Debug.Log($"TerrainScanner: Znaleziono {forests.Count} lasów.");

        foreach (var forest in forests)
        {
            GenerateMarkersForCluster(forest);
        }
    }

    ForestCluster PerformFloodFill(int startX, int startY, int w, int h, float[,,] maps, int layerIdx, bool[,] visited, int step)
    {
        ForestCluster cluster = new ForestCluster();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();

            float normX = p.x / (float)w;
            float normZ = p.y / (float)h;
            Vector3 worldPos = new Vector3(
                normX * terrainSize.x + terrainPos.x,
                0,
                normZ * terrainSize.z + terrainPos.z
            );
            worldPos.y = terrain.SampleHeight(worldPos) + terrainPos.y;
            cluster.AddPoint(worldPos);

            int[] dx = { -step, step, 0, 0 };
            int[] dy = { 0, 0, -step, step };

            for (int i = 0; i < 4; i++)
            {
                int nx = p.x + dx[i];
                int ny = p.y + dy[i];

                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                {
                    if (!visited[nx, ny] && maps[ny, nx, layerIdx] > forestDensityThreshold)
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
        return cluster;
    }

    // --- LOGIKA NAZEWNICTWA ---

    void GenerateMarkersForCluster(ForestCluster forest)
    {
        Vector3 center = forest.GetCenter();
        string dirName = GetLocationName(center); // np. "North", "East"

        // Generujemy nazwę
        string baseID = GetUniqueName("Forest_" + dirName);
        string plDesc = TranslateDir(dirName) + " las";

        // Obliczamy wymiary lasu
        float widthX = forest.maxX - forest.minX;
        float depthZ = forest.maxZ - forest.minZ;

        // Domyślne ustawienia linii
        float calculatedLength = 50f;
        Vector3 calculatedOrientation = Vector3.right;

        // LOGIKA AUTOMATYCZNEGO DOPASOWANIA LINII:
        // Jeśli las jest na Północy/Południu -> Linia powinna być pozioma (oś X)
        // Jeśli las jest na Wschodzie/Zachodzie -> Linia powinna być pionowa (oś Z)
        // Jeśli Centrum -> Bierzemy dłuższą oś lasu

        if (dirName == "North" || dirName == "South")
        {
            calculatedOrientation = Vector3.right; // Poziomo
            calculatedLength = widthX * 0.8f; // 80% szerokości lasu
        }
        else if (dirName == "East" || dirName == "West")
        {
            calculatedOrientation = Vector3.forward; // Pionowo
            calculatedLength = depthZ * 0.8f; // 80% głębokości lasu
        }
        else // Center
        {
            if (widthX > depthZ)
            {
                calculatedOrientation = Vector3.right;
                calculatedLength = widthX * 0.6f;
            }
            else
            {
                calculatedOrientation = Vector3.forward;
                calculatedLength = depthZ * 0.6f;
            }
        }

        // Tworzymy znacznik z danymi o linii
        CreateMarker(center, baseID, plDesc, Color.green, calculatedLength, calculatedOrientation);
    }

    void CreateMarker(Vector3 pos, string idName, string desc, Color color, float len = 0.0f, Vector3 orient = default(Vector3), bool isRiver = false)
    {
        if (orient == Vector3.zero) orient = Vector3.forward;

        GameObject marker = new GameObject($"Auto_{idName}");
        marker.transform.position = pos;

        TerrainFeature feature = marker.AddComponent<TerrainFeature>();
        feature.featureName = idName;
        feature.description = desc;
        feature.debugColor = color;
        feature.lineLength = len;
        feature.lineOrientation = orient;

        // NOWOŚĆ: Ustawiamy czy to rzeka i szerokość brzegu
        feature.isRiver = isRiver;
        if (isRiver) feature.riverBankOffset = 15.0f; // np. 15 metrów od środka wody
    }

    string GetLocationName(Vector3 pos)
    {
        Vector3 tPos = terrain.transform.position;
        Vector3 tSize = terrain.terrainData.size;

        float centerX = tPos.x + tSize.x * 0.5f;
        float centerZ = tPos.z + tSize.z * 0.5f;

        float dx = pos.x - centerX;
        float dz = pos.z - centerZ;

        float deadZone = tSize.x * 0.15f;

        if (Mathf.Abs(dx) < deadZone && Mathf.Abs(dz) < deadZone)
        {
            return "Center";
        }

        if (Mathf.Abs(dz) > Mathf.Abs(dx))
        {
            return (dz > 0) ? "North" : "South";
        }
        else
        {
            return (dx > 0) ? "East" : "West";
        }
    }

    string TranslateDir(string enDir)
    {
        switch (enDir)
        {
            case "North": return "Północny";
            case "South": return "Południowy";
            case "East": return "Wschodni";
            case "West": return "Zachodni";
            case "Center": return "Centralny";
            default: return enDir;
        }
    }

    string GetUniqueName(string baseName)
    {
        if (!usedNames.Contains(baseName))
        {
            usedNames.Add(baseName);
            return baseName;
        }

        int counter = 2;
        while (usedNames.Contains(baseName + "_" + counter))
        {
            counter++;
        }

        string newName = baseName + "_" + counter;
        usedNames.Add(newName);
        return newName;
    }

    float GetY(Vector3 pos)
    {
        return terrain.SampleHeight(pos) + terrain.transform.position.y;
    }

    bool IsFarEnough(Vector3 pos, List<Vector3> others, float minDist)
    {
        foreach (var other in others)
        {
            if (Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(other.x, 0, other.z)) < minDist)
                return false;
        }
        return true;
    }

}