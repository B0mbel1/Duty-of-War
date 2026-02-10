using UnityEngine;

public static class MeshModifier
{
    public static Mesh CreateModifiedMesh(Mesh sourceMesh, Vector3 positionOffset, Vector3 rotationOffset, Vector3 scale)
    {
        if (sourceMesh == null) return null;

        Mesh newMesh = new Mesh();
        newMesh.name = sourceMesh.name + "_Modified";
        Vector3[] originalVertices = sourceMesh.vertices;
        Vector3[] newVertices = new Vector3[originalVertices.Length];
        Quaternion rotation = Quaternion.Euler(rotationOffset);
        Matrix4x4 transformMatrix = Matrix4x4.TRS(positionOffset, rotation, scale);

        for (int i = 0; i < originalVertices.Length; i++)
        {
            newVertices[i] = transformMatrix.MultiplyPoint3x4(originalVertices[i]);
        }
        newMesh.vertices = newVertices;
        newMesh.triangles = sourceMesh.triangles;
        newMesh.uv = sourceMesh.uv;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }
}