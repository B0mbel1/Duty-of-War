using UnityEngine;
using System.Collections.Generic;

public enum UnitType { Infantry, Archer, Cavalry }

[System.Serializable]
public class UnitArchetype
{
    public UnitType type;
    public Mesh unitMesh;

    // Materiał aktualny (może być zielony jak zaznaczysz)
    public Material unitMaterial;
    // Materiał oryginalny (zapamiętany do odznaczenia) - NOWOŚĆ
    public Material originalMaterial;

    public Mesh arrowMesh;
    public Material arrowMaterial;

    public float speedMultiplier = 1.0f;

    public List<Matrix4x4[]> batchBuffers = new List<Matrix4x4[]>();

    public int currentCount = 0;
}

[System.Serializable]
public class TacticalGroup
{
    public string groupName;
    public string customName = "";
    public bool isMergedGroup = false;
    public HierarchyRank rank;

    public List<UnitArchetype> archetypes = new List<UnitArchetype>();
    public List<int> unitArchetypeIndices = new List<int>();

    public List<Matrix4x4> unitMatrices = new List<Matrix4x4>();
    public List<float> unitHealth = new List<float>();
    public List<float> attackCooldowns = new List<float>();
    public List<Vector3> lastPositions = new List<Vector3>();
    public List<Vector3> formationTargets = new List<Vector3>();
    public bool isReforming = false;
    public Vector3 currentDestination;
    public bool isMoving = false;
    public Vector3 groupCenter;
    public float groupRadius = 5.0f;
    public float minGroupSpeedMultiplier = 1.0f;

    private const int BATCH_SIZE = 1023;

    public TacticalGroup(string name, Mesh mesh, Material mat, HierarchyRank r, UnitType unitType)
    {
        groupName = name;
        rank = r;

        UnitArchetype baseArch = new UnitArchetype();
        baseArch.type = unitType;
        baseArch.unitMesh = mesh;
        baseArch.unitMaterial = mat;
        baseArch.originalMaterial = mat; // Zapamiętujemy oryginał
        baseArch.speedMultiplier = 1.0f;
        archetypes.Add(baseArch);

        RecalculateStats();
    }

    public void AddUnit(Matrix4x4 matrix, float hp, int archetypeIndex)
    {
        unitMatrices.Add(matrix);
        unitHealth.Add(hp);
        attackCooldowns.Add(0f);
        lastPositions.Add((Vector3)matrix.GetColumn(3));
        unitArchetypeIndices.Add(archetypeIndex);
        formationTargets.Add((Vector3)matrix.GetColumn(3));
    }

    public void RemoveUnitAt(int index)
    {
        int lastIndex = unitMatrices.Count - 1;
        if (index != lastIndex)
        {
            unitMatrices[index] = unitMatrices[lastIndex];
            unitHealth[index] = unitHealth[lastIndex];
            attackCooldowns[index] = attackCooldowns[lastIndex];
            lastPositions[index] = lastPositions[lastIndex];
            unitArchetypeIndices[index] = unitArchetypeIndices[lastIndex];
            if (formationTargets.Count > lastIndex)
                formationTargets[index] = formationTargets[lastIndex];
        }
        unitMatrices.RemoveAt(lastIndex);
        unitHealth.RemoveAt(lastIndex);
        attackCooldowns.RemoveAt(lastIndex);
        lastPositions.RemoveAt(lastIndex);
        unitArchetypeIndices.RemoveAt(lastIndex);

        if (formationTargets.Count > lastIndex)
            formationTargets.RemoveAt(lastIndex);

        RebuildBatchesFull();
    }

    public void RecalculateStats()
    {
        minGroupSpeedMultiplier = 100f;
        if (archetypes.Count == 0) minGroupSpeedMultiplier = 1.0f;

        foreach (var arch in archetypes)
        {
            if (arch.speedMultiplier < minGroupSpeedMultiplier)
                minGroupSpeedMultiplier = arch.speedMultiplier;
        }
    }

    public void RebuildBatchesFull()
    {
        foreach (var arch in archetypes)
        {
            arch.batchBuffers.Clear();
            arch.currentCount = 0;
        }

        List<List<Matrix4x4>> tempSeparated = new List<List<Matrix4x4>>();
        for (int i = 0; i < archetypes.Count; i++) tempSeparated.Add(new List<Matrix4x4>());

        for (int i = 0; i < unitMatrices.Count; i++)
        {
            int archIndex = unitArchetypeIndices[i];
            if (archIndex < tempSeparated.Count)
                tempSeparated[archIndex].Add(unitMatrices[i]);
        }

        for (int i = 0; i < archetypes.Count; i++)
        {
            var arch = archetypes[i];
            var list = tempSeparated[i];
            arch.currentCount = list.Count;

            for (int k = 0; k < list.Count; k += BATCH_SIZE)
            {
                int count = Mathf.Min(BATCH_SIZE, list.Count - k);
                Matrix4x4[] batch = new Matrix4x4[count];
                for (int m = 0; m < count; m++) batch[m] = list[k + m];
                arch.batchBuffers.Add(batch);
            }
        }
        RecalculateCenter();
        RecalculateStats();
    }

    public void UpdateBatchData()
    {
        int[] counters = new int[archetypes.Count];

        for (int i = 0; i < unitMatrices.Count; i++)
        {
            int archIndex = unitArchetypeIndices[i];
            var arch = archetypes[archIndex];

            int localIndex = counters[archIndex];
            int batchIndex = localIndex / BATCH_SIZE;
            int elementIndex = localIndex % BATCH_SIZE;

            if (batchIndex < arch.batchBuffers.Count)
                arch.batchBuffers[batchIndex][elementIndex] = unitMatrices[i];

            counters[archIndex]++;
        }
    }

    public void RecalculateCenter()
    {
        if (unitMatrices.Count == 0) return;
        Vector3 sum = Vector3.zero;
        int step = Mathf.Max(1, unitMatrices.Count / 50);
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
        foreach (var arch in archetypes)
        {
            if (arch.unitMesh == null || arch.unitMaterial == null) continue;
            foreach (var batch in arch.batchBuffers)
                Graphics.DrawMeshInstanced(arch.unitMesh, 0, arch.unitMaterial, batch);
        }
    }

    public void RenderIndicators()
    {
        foreach (var arch in archetypes)
        {
            if (arch.arrowMesh == null || arch.arrowMaterial == null) continue;
            foreach (var batch in arch.batchBuffers)
                Graphics.DrawMeshInstanced(arch.arrowMesh, 0, arch.arrowMaterial, batch);
        }
    }

    public UnitArchetype GetUnitArchetype(int unitIndex)
    {
        if (unitIndex >= 0 && unitIndex < unitArchetypeIndices.Count)
        {
            int archIndex = unitArchetypeIndices[unitIndex];
            if (archIndex >= 0 && archIndex < archetypes.Count)
                return archetypes[archIndex];
        }
        return null;
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