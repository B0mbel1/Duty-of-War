using UnityEngine;
using System.Collections.Generic;

public class TerrainScanner : MonoBehaviour
{
    public Terrain terrain;
    public float heightThreshold = 20.0f; // Minimalna wysokość, żeby uznać to za wzgórze
    public float minDistanceBetweenPeaks = 50.0f; // Żeby nie wykryło 10 szczytów na jednej górce
    public GameObject markerPrefab; // Prefab ze skryptem TerrainFeature (opcjonalne, albo dodamy kodem)

    void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        ScanForHills();
    }

    void ScanForHills()
    {
        TerrainData data = terrain.terrainData;
        float[,] heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        
        List<Vector3> peaks = new List<Vector3>();
        int resolution = data.heightmapResolution;
        Vector3 size = data.size;

        // Proste szukanie lokalnych maksimów (szczytów)
        // Przeskakujemy co 10 punktów dla optymalizacji
        int step = 10; 
        
        for (int x = step; x < resolution - step; x += step)
        {
            for (int y = step; y < resolution - step; y += step)
            {
                float h = heights[y, x] * size.y; // Rzeczywista wysokość w metrach

                if (h > heightThreshold)
                {
                    // Sprawdź czy to lokalny szczyt (wyższy niż sąsiedzi)
                    if (h > heights[y+step, x]*size.y && h > heights[y-step, x]*size.y &&
                        h > heights[y, x+step]*size.y && h > heights[y, x-step]*size.y)
                    {
                        Vector3 worldPos = new Vector3(
                            (x / (float)resolution) * size.x + terrain.transform.position.x,
                            h + terrain.transform.position.y,
                            (y / (float)resolution) * size.z + terrain.transform.position.z
                        );

                        // Sprawdź czy nie jest za blisko innego znalezionego szczytu
                        bool tooClose = false;
                        foreach (var p in peaks)
                        {
                            if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(worldPos.x, 0, worldPos.z)) < minDistanceBetweenPeaks)
                            {
                                tooClose = true;
                                break;
                            }
                        }

                        if (!tooClose)
                        {
                            peaks.Add(worldPos);
                            CreateHillMarker(worldPos, peaks.Count);
                        }
                    }
                }
            }
        }
    }

    void CreateHillMarker(Vector3 pos, int index)
    {
        GameObject marker = new GameObject($"Auto_Hill_{index}");
        marker.transform.position = pos;
        
        // Dodajemy skrypt TerrainFeature, żeby LLM go widział
        TerrainFeature feature = marker.AddComponent<TerrainFeature>();
        feature.featureName = $"Hill_{index}"; // AI zobaczy nazwę "Hill_1", "Hill_2"
        feature.description = "Wzniesienie terenu.";
        
        // Opcjonalnie: Dodaj ikonkę w edytorze
        feature.debugColor = Color.yellow;
    }
}