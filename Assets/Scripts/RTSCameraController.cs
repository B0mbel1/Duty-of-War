using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Ruch kamery")]
    public float moveSpeed = 50f;
    public float edgeScrollSize = 10f;
    public bool useEdgeScrolling = true;

    [Header("Zoom")]
    public float zoomSpeed = 100f;
    public float minHeight = 25f;
    public float maxHeight = 120f;

    [Header("Rotacja")]
    public float rotationSpeed = 100f;
    public float minPitch = 25f;
    public float maxPitch = 70f;

    [Header("Granice mapy")]
    public Vector2 limitX = new Vector2(-200, 200);
    public Vector2 limitZ = new Vector2(-200, 200);

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("❌ Skrypt musi być na obiekcie z komponentem Camera!");
            enabled = false;
            return;
        }

        // Awaryjne zabezpieczenie przed złymi pozycjami
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, limitX.x, limitX.y);
        pos.z = Mathf.Clamp(pos.z, limitZ.x, limitZ.y);
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
        transform.position = pos;
    }

    void Update()
    {
        // Bezpiecznik przed dziwnymi danymi
        if (float.IsNaN(transform.position.x) || float.IsNaN(transform.position.y) || float.IsNaN(transform.position.z))
        {
            Debug.LogWarning("⚠️ Kamera miała błędne współrzędne — resetuję pozycję!");
            transform.position = Vector3.zero + Vector3.up * 50f;
        }

        SafeHandleMovement();
        SafeHandleZoom();
        SafeHandleRotation();
    }

    void SafeHandleMovement()
    {
        float moveX = 0f;
        float moveZ = 0f;

        try
        {
            moveX = Input.GetAxis("Horizontal");
            moveZ = Input.GetAxis("Vertical");
        }
        catch
        {
            // jeśli nowy Input System jest aktywny
        }

        if (useEdgeScrolling)
        {
            Vector3 mousePos = Input.mousePosition;
            if (mousePos.x >= Screen.width - edgeScrollSize) moveX = 1;
            else if (mousePos.x <= edgeScrollSize) moveX = -1;
            if (mousePos.y >= Screen.height - edgeScrollSize) moveZ = 1;
            else if (mousePos.y <= edgeScrollSize) moveZ = -1;
        }

        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;
        Vector3 pos = transform.position;

        pos += (forward * moveZ + right * moveX) * moveSpeed * Time.deltaTime;

        pos.x = Mathf.Clamp(pos.x, limitX.x, limitX.y);
        pos.z = Mathf.Clamp(pos.z, limitZ.x, limitZ.y);

        transform.position = pos;
    }

    void SafeHandleZoom()
    {
        float scroll = 0f;
        try
        {
            scroll = Input.GetAxis("Mouse ScrollWheel");
        }
        catch { }

        Vector3 pos = transform.position;
        pos.y -= scroll * zoomSpeed * Time.deltaTime * 100f;
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
        transform.position = pos;
    }

    void SafeHandleRotation()
    {
        if (Input.GetMouseButton(1))
        {
            float rotX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, rotX, Space.World);
        }

        if (Input.GetKey(KeyCode.LeftAlt))
        {
            float rotY = -Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
            Vector3 euler = transform.eulerAngles;
            euler.x = Mathf.Clamp(euler.x + rotY, minPitch, maxPitch);
            transform.eulerAngles = euler;
        }
    }
}
