using UnityEngine;
using System.Collections.Generic;

public class UnitsSpawn : MonoBehaviour
{
    public Mesh sphereMesh;
    public Material sphereMaterial; // Materia³ dla jednostek
    public Material enemyMaterial; // Materia³ dla przeciwników
    public int count = 10000; // Ca³kowita liczba jednostek
    public GameObject cubeObject; // Referencja do Cube1 (spawn dla frakcji A)
    public GameObject cube2Object; // Referencja do Cube2 (spawn dla frakcji B)
    public GameObject cube3Object; // Referencja do Cube3 (spawn dla frakcji B, opcjonalny)
    private Terrain terrain; // Referencja do terenu w scenie

    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private List<Matrix4x4> enemyMatrices = new List<Matrix4x4>(); // Macierz przeciwników
    private const int batchSize = 1023; // limit Unity
    private List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
    private List<Matrix4x4[]> enemyBatches = new List<Matrix4x4[]>(); // Batching przeciwników

    public float sphereRadius = 1f; // Promieñ sfery
    public int enemyCount = 100; // Liczba przeciwników
    public float enemyRadius = 1f; // Promieñ przeciwników
    public float minDistanceBetweenUnits = 2f; // Minimalna odleg³oœæ miêdzy jednostkami

    private HashSet<Vector3> occupiedPositions = new HashSet<Vector3>(); // Zbiór zajêtych pozycji

    void Start()
    {
        if (!sphereMaterial.enableInstancing)
            sphereMaterial.enableInstancing = true;

        if (!enemyMaterial.enableInstancing)
            enemyMaterial.enableInstancing = true;

        // Znalezienie terenu w scenie
        terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("No terrain found in the scene!");
            return; // Zatrzymujemy dalsze wykonywanie kodu, je¿eli nie znaleziono terenu
        }

        // Ukrycie Cube (niewidzialny obiekt)
        var cubeRenderer = cubeObject.GetComponent<Renderer>();
        if (cubeRenderer != null)
        {
            cubeRenderer.enabled = false; // Ukrywanie Cube1
        }

        // Ukrycie Cube2 (niewidzialny obiekt dla przeciwników)
        var cube2Renderer = cube2Object.GetComponent<Renderer>();
        if (cube2Renderer != null)
        {
            cube2Renderer.enabled = false; // Ukrywanie Cube2
        }

        // Ukrycie Cube3 (je¿eli jest, dla dodatkowego punktu spawnu)
        if (cube3Object != null)
        {
            var cube3Renderer = cube3Object.GetComponent<Renderer>();
            if (cube3Renderer != null)
            {
                cube3Renderer.enabled = false; // Ukrywanie Cube3
            }
        }

        // Uzyskanie rozmiaru Cube
        Vector3 cubeSize = cubeObject.transform.localScale;
        Vector3 cubePosition = cubeObject.transform.position;

        // Liczba jednostek
        int factionACount = count; // Frakcja A u¿ywa Cube1
        int factionBCount = enemyCount; // Frakcja B u¿ywa Cube2 i Cube3

        // Jednostki dla frakcji A - spawnowane tylko w Cube1
        SpawnUnits(cubeObject, cubeSize, cubePosition, factionACount);

        // Sprawdzamy, czy mamy Cube3 (dodatkowy punkt spawnu)
        if (cube3Object != null)
        {
            // Jednostki dla frakcji B - spawnowane w Cube2 i Cube3
            int halfFactionB = factionBCount / 2;
            SpawnUnits(cube2Object, cube2Object.transform.localScale, cube2Object.transform.position, halfFactionB);
            SpawnUnits(cube3Object, cube3Object.transform.localScale, cube3Object.transform.position, halfFactionB);
        }
        else
        {
            // Jednostki dla frakcji B - spawnowane tylko w Cube2
            SpawnUnits(cube2Object, cube2Object.transform.localScale, cube2Object.transform.position, factionBCount);
        }

        // Batchowanie do DrawMeshInstanced (jednostki A)
        BatchUnits(matrices, batches);

        // Batchowanie do DrawMeshInstanced (przeciwnicy B)
        BatchUnits(enemyMatrices, enemyBatches);
    }

    void SpawnUnits(GameObject cube, Vector3 cubeSize, Vector3 cubePosition, int spawnCount)
    {
        var rand = new System.Random();
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 position = Vector3.zero;
            bool validPosition = false;
            int attempts = 0;

            // Próby znalezienia wa¿nej pozycji dla jednostki
            while (!validPosition && attempts < 100)
            {
                Vector3 spawnPosition = GetRandomSpawnPosition(cubePosition, cubeSize, rand);
                float y = terrain.SampleHeight(new Vector3(spawnPosition.x, 0f, spawnPosition.z)) + terrain.transform.position.y;

                // Korygowanie wysokoœci, aby dolna czêœæ sfery dotyka³a terenu
                y += sphereRadius;  // Przemieszczamy o promieñ sfery, aby dolna czêœæ dotyka³a terenu

                position = new Vector3(spawnPosition.x, y, spawnPosition.z);

                // Sprawdzamy, czy pozycja jest zajêta i czy jednostka ma odpowiedni¹ odleg³oœæ
                if (!occupiedPositions.Contains(position) && IsFarEnoughFromOtherUnits(position))
                {
                    validPosition = true;
                }
                else
                {
                    attempts++;
                }
            }

            if (!validPosition) continue; // Jeœli nie znaleŸliœmy wa¿nej pozycji po 100 próbach, pomijamy jednostkê

            occupiedPositions.Add(position);
            var matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            if (cube == cubeObject)
                matrices.Add(matrix); // Dodajemy jednostkê do frakcji A
            else
                enemyMatrices.Add(matrix); // Dodajemy jednostkê do frakcji B
        }
    }

    // Funkcja do batchowania jednostek
    void BatchUnits(List<Matrix4x4> matrices, List<Matrix4x4[]> batches)
    {
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
        // Rysowanie jednostek
        foreach (var batch in batches)
            Graphics.DrawMeshInstanced(sphereMesh, 0, sphereMaterial, batch);

        // Rysowanie przeciwników
        foreach (var enemyBatch in enemyBatches)
            Graphics.DrawMeshInstanced(sphereMesh, 0, enemyMaterial, enemyBatch);
    }

    // Funkcja do sprawdzania, czy jednostka jest wystarczaj¹co oddalona od innych
    bool IsFarEnoughFromOtherUnits(Vector3 position)
    {
        foreach (var occupiedPosition in occupiedPositions)
        {
            if (Vector3.Distance(position, occupiedPosition) < minDistanceBetweenUnits)
            {
                return false;
            }
        }
        return true;
    }

    // Funkcja do generowania losowej pozycji w obrêbie danego szeœcianu
    Vector3 GetRandomSpawnPosition(Vector3 cubePosition, Vector3 cubeSize, System.Random rand)
    {
        float x = cubePosition.x + (float)(rand.NextDouble() - 0.5f) * cubeSize.x;
        float z = cubePosition.z + (float)(rand.NextDouble() - 0.5f) * cubeSize.z;

        return new Vector3(x, 0f, z);
    }
}
