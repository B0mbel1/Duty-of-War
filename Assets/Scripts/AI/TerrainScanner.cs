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
        
        Debug.Log("Skanowanie zakończone. Możesz teraz przesunąć punkty ręcznie.");
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

        // 1. Ustal, w jakiej części mapy leży las (np. "North", "West")
        string directionName = GetLocationName(center);

        // 2. Zbuduj nazwę techniczną
        string uniqueID = GetUniqueName("Forest_" + directionName);
        string plDesc = TranslateDir(directionName) + " las";

        // Zastosowanie Twojego Edge Inset dla punktu centralnego
        // Jeśli chcesz, żeby punkt centralny też był lekko przesuwany względem środka masy (opcjonalne), 
        // ale standardowo Center to Center. 
        // W poprzedniej wersji przesuwaliśmy skrajne punkty. 
        // Skoro tutaj generujemy TYLKO Center (jak w Twoim kodzie), to wstawiamy go po prostu w center.
        
        CreateMarker(center, uniqueID, plDesc, Color.green);
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

    void CreateMarker(Vector3 pos, string idName, string desc, Color color)
    {
        GameObject marker = new GameObject($"Auto_{idName}");
        marker.transform.position = pos;

        TerrainFeature feature = marker.AddComponent<TerrainFeature>();
        feature.featureName = idName; 
        feature.description = desc;   
        feature.debugColor = color;
    }
}