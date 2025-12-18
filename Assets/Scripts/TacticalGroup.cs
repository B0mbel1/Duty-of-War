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

    // Dane matematyczne (Zamiast jednej wielkiej listy w UnitsSpawn, ka¿dy oddzia³ ma swoj¹)
    public List<Matrix4x4> unitMatrices = new List<Matrix4x4>();
    public List<Matrix4x4[]> renderBatches = new List<Matrix4x4[]>(); // Batche do renderowania

    // Logika ruchu
    public Vector3 currentDestination;
    public bool isMoving = false;
    public Vector3 groupCenter;        // Œrodek formacji

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
        RecalculateCenter();
    }

    public void RecalculateCenter()
    {
        if (unitMatrices.Count == 0) return;
        Vector3 sum = Vector3.zero;
        foreach (var mat in unitMatrices)
        {
            sum += (Vector3)mat.GetColumn(3);
        }
        groupCenter = sum / unitMatrices.Count;
    }

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

// Hierarchia wg Twojego obrazka
public enum HierarchyRank
{
    Squad = 10,       // Dru¿yna (~10)
    Platoon = 40,     // Pluton (~40)
    Company = 150,    // Kompania (~150)
    Battalion = 800,  // Batalion (~800) - Podstawa Total War
    Brigade = 3000    // Brygada (~3000)
}