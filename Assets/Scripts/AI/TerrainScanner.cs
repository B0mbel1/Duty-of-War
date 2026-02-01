using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TerrainScanner : MonoBehaviour
{
    [Header("Ustawienia Ogólne")]
    public Terrain terrain;
    public GameObject markerPrefab;

    [Header("Wykrywanie Wzgórz")]
    public float heightThreshold = 20.0f;
    public float minDistanceBetweenPeaks = 50.0f;

    [Header("Wykrywanie Lasów (Grupowanie)")]
    public string forestLayerName = "Forest";
    [Range(0.1f, 1f)] public float forestDensityThreshold = 0.5f;
    public int scanResolutionDivisor = 4; // Optymalizacja: Skanuj co 4 piksel mapy (szybciej)
    public int minForestSize = 50; // Ignoruj małe kępki drzew (ilość punktów w klastrze)

    // Struktura pomocnicza dla klastrów
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
            Vector3 sum = Vector3.zero;
            foreach (var p in points) sum += p;
            return sum / points.Count;
        }
    }

    void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        ScanForHills_Improved();
        ScanForForests_Clustered();
    }

    // --- WZGÓRZA (Bez zmian - działa dobrze) ---
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
                CreateMarker(point, $"Hill_{finalPeaks.Count}", "Strategiczne Wzgórze", Color.yellow);
            }
        }
    }

    // --- LASY (Algorytm Flood Fill / Grupowanie) ---
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

        // Pobieramy mapę tekstur
        float[,,] maps = data.GetAlphamaps(0, 0, alphaW, alphaH);
        
        // Tablica odwiedzonych punktów, żeby nie liczyć tego samego lasu 2 razy
        bool[,] visited = new bool[alphaW, alphaH];
        List<ForestCluster> forests = new List<ForestCluster>();

        int step = scanResolutionDivisor; 

        // 1. Skanujemy mapę w poszukiwaniu "wysp" lasu
        for (int x = 0; x < alphaW; x += step)
        {
            for (int y = 0; y < alphaH; y += step)
            {
                if (visited[x, y]) continue;

                // Jeśli znaleźliśmy las -> odpalamy Flood Fill żeby znaleźć całą wyspę
                if (maps[y, x, forestIdx] > forestDensityThreshold)
                {
                    ForestCluster cluster = PerformFloodFill(x, y, alphaW, alphaH, maps, forestIdx, visited, step);
                    
                    // Jeśli las jest wystarczająco duży, dodajemy go
                    if (cluster.points.Count > minForestSize)
                    {
                        forests.Add(cluster);
                    }
                }
            }
        }

        Debug.Log($"TerrainScanner: Znaleziono {forests.Count} oddzielnych lasów.");

        // 2. Generujemy punkty dla każdego znalezionego lasu
        int forestCounter = 1;
        foreach (var forest in forests)
        {
            GenerateMarkersForCluster(forest, forestCounter);
            forestCounter++;
        }
    }

    // Algorytm "Zalewania" do znajdowania połączonych obszarów
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
            
            // Oblicz pozycję w świecie
            float normX = p.x / (float)w;
            float normZ = p.y / (float)h;
            Vector3 worldPos = new Vector3(
                normX * terrainSize.x + terrainPos.x,
                0,
                normZ * terrainSize.z + terrainPos.z
            );
            worldPos.y = terrain.SampleHeight(worldPos) + terrainPos.y;
            
            cluster.AddPoint(worldPos);

            // Sprawdź sąsiadów
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

    void GenerateMarkersForCluster(ForestCluster forest, int id)
    {
        Vector3 center = forest.GetCenter();
        
        // --- 1. Punkt Centralny ---
        CreateMarker(center, $"Forest_{id}_Center", "Centrum Lasu", Color.green);

        // --- 2. Punkty Kierunkowe (Płn, Płd, Wsch, Zach) ---
        // Dodajemy je tylko jeśli las jest wystarczająco duży (np. szerszy niż 100m)
        float width = forest.maxX - forest.minX;
        float depth = forest.maxZ - forest.minZ;

        if (width > 80 && depth > 80)
        {
            float y = center.y; // Uproszczenie wysokości

            Vector3 north = new Vector3(center.x, y, forest.maxZ);
            Vector3 south = new Vector3(center.x, y, forest.minZ);
            Vector3 east = new Vector3(forest.maxX, y, center.z);
            Vector3 west = new Vector3(forest.minX, y, center.z);

            // Narożniki (dla rozkazów typu "południowy wschód")
            Vector3 northEast = new Vector3(forest.maxX, y, forest.maxZ);
            Vector3 southEast = new Vector3(forest.maxX, y, forest.minZ);
            Vector3 southWest = new Vector3(forest.minX, y, forest.minZ);
            Vector3 northWest = new Vector3(forest.minX, y, forest.maxZ);

            // Korygujemy wysokość punktów (żeby nie lewitowały)
            north.y = GetY(north); south.y = GetY(south); east.y = GetY(east); west.y = GetY(west);
            northEast.y = GetY(northEast); southEast.y = GetY(southEast); 
            southWest.y = GetY(southWest); northWest.y = GetY(northWest);

            // Tworzymy znaczniki
            CreateMarker(north, $"Forest_{id}_North", "Północny skraj lasu", Color.cyan);
            CreateMarker(south, $"Forest_{id}_South", "Południowy skraj lasu", Color.cyan);
            CreateMarker(east, $"Forest_{id}_East", "Wschodni skraj lasu", Color.cyan);
            CreateMarker(west, $"Forest_{id}_West", "Zachodni skraj lasu", Color.cyan);
            
            // Narożniki (opcjonalnie, jeśli chcesz bardzo precyzyjne rozkazy)
            CreateMarker(southEast, $"Forest_{id}_SouthEast", "Płd-Wsch część lasu", Color.blue);
            CreateMarker(northWest, $"Forest_{id}_NorthWest", "Płn-Zach część lasu", Color.blue);
        }
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