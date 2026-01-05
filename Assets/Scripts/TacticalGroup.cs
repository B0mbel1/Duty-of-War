using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TacticalGroup
{
    public string groupName;           // Np. "1. Kompania Piechoty"
    public int unitID;                 // Unikalne ID do UI
    public HierarchyRank rank;         // Np. Kompania, Batalion

    // Wizualia
    public Mesh unitMesh;
    public Material unitMaterial;

    public Mesh arrowMesh;
    public Material arrowMaterial;

    public float speedMultiplier = 1.0f;

    // Dane matematyczne
    public List<Matrix4x4> unitMatrices = new List<Matrix4x4>();
    public List<Matrix4x4[]> renderBatches = new List<Matrix4x4[]>(); // Batche do renderowania

    // Logika ruchu
    public Vector3 currentDestination;
    public bool isMoving = false;

    public Vector3 groupCenter;        // Œrodek formacji (œrednia arytmetyczna)

    // --- NOWOŒÆ: Rzeczywisty promieñ grupy (do wykrywania wody na krawêdziach) ---
    public float groupRadius = 5.0f;
    // -----------------------------------------------------------------------------

    // Konstruktor
    public TacticalGroup(string name, Mesh mesh, Material mat, HierarchyRank r)
    {
        groupName = name;
        unitMesh = mesh;
        unitMaterial = mat;
        rank = r;
    }

    // Przeliczanie batchy (GPU Instancing wymaga paczek po 1023)
    public void RebuildBatches()
    {
        renderBatches.Clear();
        int batchSize = 1023;
        for (int i = 0; i < unitMatrices.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, unitMatrices.Count - i);
            var batch = new Matrix4x4[count];
            unitMatrices.CopyTo(i, batch, 0, count);
            renderBatches.Add(batch);
        }

        // Zawsze po przebudowaniu batchy przeliczamy centrum i rozmiar
        RecalculateCenter();
    }

    public void RenderIndicators()
    {
        if (arrowMesh == null || arrowMaterial == null) return;

        // U¿ywamy tych samych batchy co jednostki, wiêc strza³ki 
        // bêd¹ mia³y identyczn¹ pozycjê i rotacjê.
        foreach (var batch in renderBatches)
        {
            Graphics.DrawMeshInstanced(arrowMesh, 0, arrowMaterial, batch);
        }
    }

    // --- ZMODYFIKOWANA METODA: OBLICZA ŒRODEK I ROZMIAR (RADIUS) ---
    public void RecalculateCenter()
    {
        if (unitMatrices.Count == 0) return;

        Vector3 sum = Vector3.zero;

        // Zmienne do znalezienia skrajnych punktów (Bounding Box)
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (var mat in unitMatrices)
        {
            Vector3 pos = (Vector3)mat.GetColumn(3); // Pobierz pozycjê z macierzy
            sum += pos;

            // Sprawdzamy granice
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.z < minZ) minZ = pos.z;
            if (pos.z > maxZ) maxZ = pos.z;
        }

        // 1. Œrodek masy
        groupCenter = sum / unitMatrices.Count;

        // 2. Obliczanie promienia (po³owa d³u¿szego boku prostok¹ta otaczaj¹cego)
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;

        // Bierzemy wiêkszy wymiar i dzielimy na pó³. 
        // Dodajemy minimalny margines 1.0f, ¿eby promieñ nie by³ zerowy dla pojedynczej jednostki.
        groupRadius = (Mathf.Max(sizeX, sizeZ) * 0.5f) + 1.0f;
    }
    // ---------------------------------------------------------------

    // Renderowanie tego konkretnego oddzia³u
    public void RenderGroup()
    {
        if (unitMesh == null || unitMaterial == null) return;
        foreach (var batch in renderBatches)
        {
            Graphics.DrawMeshInstanced(unitMesh, 0, unitMaterial, batch);
        }
    }
}

// Hierarchia
public enum HierarchyRank
{
    Squad = 10,
    Platoon = 40,
    Company = 150,
    Battalion = 800,
    Brigade = 3000
}