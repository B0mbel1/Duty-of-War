using UnityEngine;

public static class MeshModifier
{
    // Funkcja tworzy kopiê mesha z przesuniêciem, obrotem i skal¹
    public static Mesh CreateModifiedMesh(Mesh sourceMesh, Vector3 positionOffset, Vector3 rotationOffset, Vector3 scale)
    {
        if (sourceMesh == null) return null;

        Mesh newMesh = new Mesh();
        newMesh.name = sourceMesh.name + "_Modified";

        // Pobieramy oryginalne wierzcho³ki
        Vector3[] originalVertices = sourceMesh.vertices;
        Vector3[] newVertices = new Vector3[originalVertices.Length];

        // Tworzymy macierz transformacji dla samego Mesha
        Quaternion rotation = Quaternion.Euler(rotationOffset);
        Matrix4x4 transformMatrix = Matrix4x4.TRS(positionOffset, rotation, scale);

        // Przeliczamy ka¿dy punkt siatki
        for (int i = 0; i < originalVertices.Length; i++)
        {
            newVertices[i] = transformMatrix.MultiplyPoint3x4(originalVertices[i]);
        }

        // Przepisujemy dane do nowego mesha
        newMesh.vertices = newVertices;
        newMesh.triangles = sourceMesh.triangles;
        newMesh.uv = sourceMesh.uv;

        // Przeliczamy normalsy (cienie), ¿eby œwiat³o dobrze pada³o po obrocie
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }
}