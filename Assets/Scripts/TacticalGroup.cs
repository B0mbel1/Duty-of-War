using UnityEngine;
using System.Collections.Generic;

public enum UnitType { Infantry, Archer, Cavalry }

[System.Serializable]
public class TacticalGroup
{
    public string groupName;
    public string customName = "";
    public bool isMergedGroup = false;
    public UnitType type;
    public int unitID;
    public HierarchyRank rank;

    // Wizualia
    public Mesh unitMesh;
    public Material unitMaterial;
    public Mesh arrowMesh;
    public Material arrowMaterial;
    public float speedMultiplier = 1.0f;

    // --- DANE ---
    public List<Matrix4x4> unitMatrices = new List<Matrix4x4>();
    
    // ZMIANA: Buforowane tablice do renderowania (żeby nie tworzyć nowych co klatkę)
    // Instancing wymaga tablicy max 1023 elementowej.
    public List<Matrix4x4[]> batchBuffers = new List<Matrix4x4[]>(); 
    
    public List<float> unitHealth = new List<float>();
    public List<float> attackCooldowns = new List<float>();
    public List<Vector3> lastPositions = new List<Vector3>();

    public Vector3 currentDestination;
    public bool isMoving = false;
    public Vector3 groupCenter;
    public float groupRadius = 5.0f;

    private const int BATCH_SIZE = 1023;

    public TacticalGroup(string name, Mesh mesh, Material mat, HierarchyRank r, UnitType unitType)
    {
        groupName = name;
        unitMesh = mesh;
        unitMaterial = mat;
        rank = r;
        type = unitType;
    }

    public void AddUnit(Matrix4x4 matrix, float hp)
    {
        unitMatrices.Add(matrix);
        unitHealth.Add(hp);
        attackCooldowns.Add(0f);
        lastPositions.Add((Vector3)matrix.GetColumn(3));
    }

    public void RemoveUnitAt(int index)
    {
        // Swap-and-pop (szybsze usuwanie, ale zmienia kolejność - w instancingu to nie przeszkadza wizualnie)
        int lastIndex = unitMatrices.Count - 1;
        if (index != lastIndex)
        {
            unitMatrices[index] = unitMatrices[lastIndex];
            unitHealth[index] = unitHealth[lastIndex];
            attackCooldowns[index] = attackCooldowns[lastIndex];
            lastPositions[index] = lastPositions[lastIndex];
        }

        unitMatrices.RemoveAt(lastIndex);
        unitHealth.RemoveAt(lastIndex);
        attackCooldowns.RemoveAt(lastIndex);
        lastPositions.RemoveAt(lastIndex);
        
        // Po usunięciu jednostki musimy przebudować strukturę tablic
        RebuildBatchesFull(); 
    }

    // Pełna przebudowa (tylko przy starcie lub śmierci jednostki) -> Tworzy nowe tablice (Alokacja)
    public void RebuildBatchesFull()
    {
        batchBuffers.Clear();
        for (int i = 0; i < unitMatrices.Count; i += BATCH_SIZE)
        {
            int count = Mathf.Min(BATCH_SIZE, unitMatrices.Count - i);
            batchBuffers.Add(new Matrix4x4[count]); // Tu alokujemy pamięć
        }
        UpdateBatchData(); // Wypełnij danymi
        RecalculateCenter();
    }

    // Szybka aktualizacja (co klatkę przy ruchu) -> NIE ALOKUJE PAMIĘCI!
    public void UpdateBatchData()
    {
        int globalIndex = 0;
        for (int b = 0; b < batchBuffers.Count; b++)
        {
            Matrix4x4[] batch = batchBuffers[b];
            for (int k = 0; k < batch.Length; k++)
            {
                if (globalIndex < unitMatrices.Count)
                {
                    batch[k] = unitMatrices[globalIndex];
                    globalIndex++;
                }
            }
        }
    }

    public void RecalculateCenter()
    {
        if (unitMatrices.Count == 0) return;
        // Optymalizacja: Licz środek tylko na podstawie co 10 jednostki (przybliżenie)
        // przy 5000 jednostek, iterowanie wszystkich co klatkę jest zbędne.
        Vector3 sum = Vector3.zero;
        int step = Mathf.Max(1, unitMatrices.Count / 50); // Próbkuj max 50 jednostek
        int sampledCount = 0;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        for (int i = 0; i < unitMatrices.Count; i += step)
        {
            Vector3 pos = (Vector3)unitMatrices[i].GetColumn(3);
            sum += pos;
            if (pos.x < minX) minX = pos.x; if (pos.x > maxX) maxX = pos.x;
            if (pos.z < minZ) minZ = pos.z; if (pos.z > maxZ) maxZ = pos.z;
            sampledCount++;
        }

        groupCenter = sum / sampledCount;
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        groupRadius = (Mathf.Max(sizeX, sizeZ) * 0.5f) + 1.0f;
    }

    public void RenderGroup()
    {
        if (unitMesh == null || unitMaterial == null) return;
        foreach (var batch in batchBuffers) Graphics.DrawMeshInstanced(unitMesh, 0, unitMaterial, batch);
    }

    public void RenderIndicators()
    {
        if (arrowMesh == null || arrowMaterial == null) return;
        foreach (var batch in batchBuffers) Graphics.DrawMeshInstanced(arrowMesh, 0, arrowMaterial, batch);
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