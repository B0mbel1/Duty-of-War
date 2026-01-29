using UnityEngine;
using System.Collections.Generic;
using System.Linq; // WAŻNE: Dodane do sortowania listy

public class TerrainScanner : MonoBehaviour
{
    [Header("Ustawienia Ogólne")]
    public Terrain terrain;
    public GameObject markerPrefab; 

    [Header("Wykrywanie Wzgórz")]
    public float heightThreshold = 20.0f; // Minimalna wysokość, żeby uznać to za wzgórze
    public float minDistanceBetweenPeaks = 50.0f; // Odstęp między znacznikami

    [Header("Wykrywanie Lasów")]
    public string forestLayerName = "Forest"; 
    public float forestDensityThreshold = 0.5f; 
    public float minDistanceBetweenForests = 60.0f;

    void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        ScanForHills_Improved(); // Nowa metoda
        ScanForForests();
    }

    // --- NOWA, LEPSZA METODA DO PŁASKOWYŻY ---
    void ScanForHills_Improved()
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 size = data.size;
        
        // Lista kandydatów na wzgórza
        List<Vector3> candidates = new List<Vector3>();

        int step = 10; // Przeskok dla optymalizacji (sprawdzamy co 10 punkt)

        for (int x = 0; x < res; x += step)
        {
            for (int y = 0; y < res; y += step)
            {
                float h = heights[y, x] * size.y; // Wysokość w metrach

                // Jeśli punkt jest wystarczająco wysoko, jest kandydatem
                // Nie sprawdzamy czy jest wyższy od sąsiadów - to naprawia problem płaskowyżu!
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

        // Sortujemy kandydatów od najwyższego do najniższego
        // Dzięki temu znacznik trafi w najwyższy punkt (lub środek płaskowyżu)
        candidates = candidates.OrderByDescending(v => v.y).ToList();

        List<Vector3> finalPeaks = new List<Vector3>();

        foreach (var point in candidates)
        {
            // Sprawdź czy ten punkt nie jest za blisko już postawionych znaczników
            if (IsFarEnough(point, finalPeaks, minDistanceBetweenPeaks))
            {
                finalPeaks.Add(point);
                CreateMarker(point, $"Hill_{finalPeaks.Count}", "Strategiczne Wzgórze", Color.yellow);
            }
        }
        
        Debug.Log($"TerrainScanner: Znaleziono {finalPeaks.Count} wzgórz.");
    }

    // --- RESZTA BEZ ZMIAN ---

    void ScanForForests()
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

        if (forestIdx == -1) return;

        float[,,] maps = data.GetAlphamaps(0, 0, alphaW, alphaH);
        List<Vector3> forests = new List<Vector3>();
        int step = 15; 

        for (int y = 0; y < alphaH; y += step)
        {
            for (int x = 0; x < alphaW; x += step)
            {
                if (maps[y, x, forestIdx] > forestDensityThreshold)
                {
                    float normX = x / (float)alphaW;
                    float normZ = y / (float)alphaH;

                    Vector3 worldPos = new Vector3(
                        normX * data.size.x + terrain.transform.position.x,
                        0,
                        normZ * data.size.z + terrain.transform.position.z
                    );
                    worldPos.y = terrain.SampleHeight(worldPos) + terrain.transform.position.y;

                    if (IsFarEnough(worldPos, forests, minDistanceBetweenForests))
                    {
                        forests.Add(worldPos);
                        CreateMarker(worldPos, $"Forest_{forests.Count}", "Las", Color.green);
                    }
                }
            }
        }
    }

    bool IsFarEnough(Vector3 pos, List<Vector3> others, float minDist)
    {
        foreach (var other in others)
        {
            // Liczymy dystans w poziomie (ignorujemy różnicę wysokości dla dystansu)
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