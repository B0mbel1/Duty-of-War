using UnityEngine;
using System.Collections.Generic;

public class test : MonoBehaviour
{
    public Mesh sphereMesh;
    public Material sphereMaterial;
    public int count = 10000;
    public Vector2 spawnArea = new Vector2(200, 200);

    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private const int batchSize = 1023; // limit Unity
    private List<Matrix4x4[]> batches = new List<Matrix4x4[]>();

    void Start()
    {
        if (!sphereMaterial.enableInstancing)
            sphereMaterial.enableInstancing = true;

        // losowo rozmieszczone sfery
        var rand = new System.Random();
        for (int i = 0; i < count; i++)
        {
            float x = (float)(rand.NextDouble() - 0.5f) * spawnArea.x;
            float z = (float)(rand.NextDouble() - 0.5f) * spawnArea.y;
            float y = 0f;
            var matrix = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity, Vector3.one);
            matrices.Add(matrix);
        }

        // batchowanie do DrawMeshInstanced
        for (int i = 0; i < matrices.Count; i += batchSize)
        {
            int batchCount = Mathf.Min(batchSize, matrices.Count - i);
            var batch = new Matrix4x4[batchCount];
            matrices.CopyTo(i, batch, 0, batchCount);
            batches.Add(batch);
        }
    }

    void Update()
    {
        foreach (var batch in batches)
            Graphics.DrawMeshInstanced(sphereMesh, 0, sphereMaterial, batch);
    }
}
