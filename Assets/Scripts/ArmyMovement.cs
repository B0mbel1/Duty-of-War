using UnityEngine;
using System.Collections.Generic;

public class ArmyMovement : MonoBehaviour
{
    [Header("Spawn Scripts References (Auto-detected)")]
    public UnitsSpawn spawnHistory;
    public UnitsSpawnCustom spawnCustom;

    [Header("References")]
    public Terrain terrain;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 8f; // Zwiêkszy³em lekko obrót
    public float stopDistance = 2.0f;

    [Header("Precision Settings")]
    [Tooltip("W£¥CZ TO jeœli jednostki wchodz¹ w tekstury. Raycast jest dok³adniejszy ni¿ SampleHeight, ale kosztowniejszy dla CPU.")]
    public bool usePhysicsRaycast = true;
    [Tooltip("Warstwy, które Raycast uznaje za ziemiê (zazwyczaj Default i Terrain).")]
    public LayerMask groundLayer = 1; // Default

    [Header("Water Settings")]
    [Tooltip("Ile jednostka ma siê zanurzyæ w wodzie.")]
    public float swimmingDepth = 0.2f;

    [Header("Unit Height Offsets (Korekta dla L¥DU)")]
    [Tooltip("Podnosi model do góry, ¿eby nie szura³ œrodkiem po ziemi.")]
    public float offsetInfantry = 0.5f;
    public float offsetArcher = 0.5f;
    public float offsetCavalry = 1.0f;

    [Header("Water Detection")]
    public string waterTag = "Water";
    public bool autoDetectWater = true;

    private Collider waterCollider;
    private float waterSurfaceY;
    private bool waterFound = false;

    // Granice mapy
    private float mapMinX, mapMaxX;
    private float mapMinZ, mapMaxZ;

    // Stan ruchu
    private bool isMoving = false;
    private Vector3 targetDestination;

    // Flaga pomocnicza
    private bool useHistoryMode = false;

    void Start()
    {
        DetectWater();

        if (spawnHistory == null) spawnHistory = GetComponent<UnitsSpawn>();
        if (spawnCustom == null) spawnCustom = GetComponent<UnitsSpawnCustom>();

        if (spawnHistory != null && spawnHistory.enabled) useHistoryMode = true;
        else if (spawnCustom != null && spawnCustom.enabled) useHistoryMode = false;
        else
        {
            if (spawnCustom != null) useHistoryMode = false;
            else
            {
                Debug.LogError("ArmyMovement: Brak skryptów spawnowania!");
                this.enabled = false;
                return;
            }
        }

        if (terrain != null)
        {
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            mapMinX = terrainPos.x;
            mapMaxX = terrainPos.x + terrainSize.x;
            mapMinZ = terrainPos.z;
            mapMaxZ = terrainPos.z + terrainSize.z;
        }
    }

    void DetectWater()
    {
        waterFound = false;
        if (autoDetectWater)
        {
            GameObject waterObj = GameObject.FindGameObjectWithTag(waterTag);
            if (waterObj != null)
            {
                waterCollider = waterObj.GetComponent<Collider>();
                if (waterCollider != null)
                {
                    waterSurfaceY = waterObj.transform.position.y + (waterObj.transform.localScale.y * 0.5f);
                    waterFound = true;
                    Debug.Log($"Woda wykryta. Poziom tafli: {waterSurfaceY}");
                }
            }
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1)) SetTargetFromMouse();

        if (isMoving)
        {
            bool armyA_Moved = false;

            var listInf = useHistoryMode ? spawnHistory.listA_Inf : spawnCustom.listA_Inf;
            var listArc = useHistoryMode ? spawnHistory.listA_Arc : spawnCustom.listA_Arc;
            var listCav = useHistoryMode ? spawnHistory.listA_Cav : spawnCustom.listA_Cav;

            armyA_Moved |= MoveGroup(listInf, offsetInfantry);
            armyA_Moved |= MoveGroup(listArc, offsetArcher);
            armyA_Moved |= MoveGroup(listCav, offsetCavalry);

            if (!armyA_Moved) isMoving = false;

            if (useHistoryMode) spawnHistory.RebuildAllBatches();
            else spawnCustom.RebuildAllBatches();
        }
    }

    void SetTargetFromMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        // Zwiêkszy³em dystans raycasta i doda³em maskê, ¿eby ignorowaæ UI itp.
        if (Physics.Raycast(ray, out hit, 10000f, groundLayer))
        {
            targetDestination = hit.point;
            isMoving = true;
        }
        else if (Physics.Raycast(ray, out hit, 10000f)) // Fallback bez maski
        {
            targetDestination = hit.point;
            isMoving = true;
        }
    }

    bool MoveGroup(List<Matrix4x4> unitList, float landOffset)
    {
        if (unitList == null || unitList.Count == 0) return false;

        float currentUnitDiameter = useHistoryMode ? spawnHistory.unitDiameter : spawnCustom.unitDiameter;

        // 1. Œrodek Grupy
        Vector3 groupCenter = Vector3.zero;
        int step = (unitList.Count > 100) ? 10 : 1;
        int sampleCount = 0;
        for (int i = 0; i < unitList.Count; i += step)
        {
            groupCenter += (Vector3)unitList[i].GetColumn(3);
            sampleCount++;
        }
        if (sampleCount > 0) groupCenter /= sampleCount;

        // 2. Dystans
        Vector3 flatCenter = new Vector3(groupCenter.x, 0, groupCenter.z);
        Vector3 flatTarget = new Vector3(targetDestination.x, 0, targetDestination.z);
        if (Vector3.Distance(flatCenter, flatTarget) < stopDistance) return false;

        // 3. Ruch
        Vector3 direction = (flatTarget - flatCenter).normalized;
        Vector3 moveVector = direction * moveSpeed * Time.deltaTime;

        for (int i = 0; i < unitList.Count; i++)
        {
            Matrix4x4 mat = unitList[i];
            Vector3 currentPos = mat.GetColumn(3);

            // Nowa pozycja X, Z
            Vector3 newPos = currentPos + moveVector;

            // Clamp
            newPos.x = Mathf.Clamp(newPos.x, mapMinX, mapMaxX);
            newPos.z = Mathf.Clamp(newPos.z, mapMinZ, mapMaxZ);

            // --- 4. Obliczanie Wysokoœci (BEZ OPÓNIEÑ) ---
            float groundHeight = 0f;

            if (usePhysicsRaycast)
            {
                // Metoda Raycast (Dok³adniejsza)
                // Strzelamy z góry w dó³, ¿eby znaleŸæ ziemiê w punkcie newPos
                RaycastHit groundHit;
                Vector3 rayOrigin = new Vector3(newPos.x, terrain.transform.position.y + 2000f, newPos.z);

                if (Physics.Raycast(rayOrigin, Vector3.down, out groundHit, 3000f, groundLayer))
                {
                    groundHeight = groundHit.point.y;
                }
                else
                {
                    // Fallback jeœli raycast nie trafi (np. poza map¹), bierzemy z terrain
                    groundHeight = terrain.SampleHeight(newPos) + terrain.transform.position.y;
                }
            }
            else
            {
                // Metoda SampleHeight (Szybsza)
                groundHeight = terrain.SampleHeight(newPos) + terrain.transform.position.y;
            }

            // --- 5. Decyzja: Woda czy L¹d ---
            float finalY = groundHeight;
            bool isInWaterArea = waterFound && IsPointInsideWaterBounds(newPos);

            if (isInWaterArea && waterSurfaceY > groundHeight)
            {
                // WODA: Poziom wody - zanurzenie
                finalY = waterSurfaceY - swimmingDepth;
            }
            else
            {
                // L¥D: Poziom ziemi + offset modelu
                finalY = groundHeight + landOffset;
            }

            // --- 6. PRZYPISANIE (Hard Snap) ---
            float currentY = currentPos.y;

            // Ustawiamy prêdkoœæ dopasowania do terenu (im wy¿sza liczba, tym szybciej)
            float heightSmoothSpeed = 10f;

            // Jeœli ró¿nica jest ogromna (teleportacja), zrób snap, w przeciwnym razie wyg³adŸ
            if (Mathf.Abs(finalY - currentY) > 2.0f)
            {
                newPos.y = finalY; // Hard snap przy du¿ej ró¿nicy (np. spawn)
            }
            else
            {
                // P³ynne przejœcie (Lerp)
                newPos.y = Mathf.Lerp(currentY, finalY, Time.deltaTime * heightSmoothSpeed);
            }

            // Opcjonalnie: Zabezpieczenie, by nie wpad³y pod ziemiê przy szybkim ruchu
            // Jeœli Lerp jest za wolny, a góra stroma -> wymuœ minimum
            if (newPos.y < finalY) newPos.y = finalY;

            // Rotacja
            Quaternion currentRot = mat.rotation;
            Quaternion targetRot = Quaternion.LookRotation(direction);
            Quaternion nextRot = Quaternion.Slerp(currentRot, targetRot, rotationSpeed * Time.deltaTime);

            Vector3 scale = new Vector3(currentUnitDiameter, currentUnitDiameter, currentUnitDiameter);
            unitList[i] = Matrix4x4.TRS(newPos, nextRot, scale);
        }

        return true;
    }

    bool IsPointInsideWaterBounds(Vector3 point)
    {
        if (waterCollider == null) return false;
        Bounds b = waterCollider.bounds;
        return (point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z);
    }
}