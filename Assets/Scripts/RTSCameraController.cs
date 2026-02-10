using UnityEngine;
using UnityEngine.EventSystems;

public class RTSCameraController : MonoBehaviour
{
    [Header("Ruch kamery")]
    public float moveSpeed = 50f;
    public float shiftMultiplier = 2f;

    [Header("Zoom")]
    public float zoomSpeed = 100f;
    public float minHeight = 25f;
    public float maxHeight = 120f;

    [Header("Rotacja")]
    public float rotationSpeed = 100f;
    public float minPitch = 25f;
    public float maxPitch = 70f;

    [Header("Granice mapy")]
    public Vector2 limitX = new Vector2(0, 1000);
    public Vector2 limitZ = new Vector2(0, 1000);

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("Skrypt musi być na obiekcie z komponentem Camera!");
            enabled = false;
            return;
        }

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, limitX.x, limitX.y);
        pos.z = Mathf.Clamp(pos.z, limitZ.x, limitZ.y);
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
        transform.position = pos;
    }

    void Update()
    {
        if (EventSystem.current.IsPointerOverGameObject())
            return;

        if (float.IsNaN(transform.position.x) ||
            float.IsNaN(transform.position.y) ||
            float.IsNaN(transform.position.z))
        {
            Debug.LogWarning("Kamera miała błędne współrzędne — reset!");
            transform.position = new Vector3(0, 50, 0);
        }

        HandleMovement();
        HandleZoom();
        HandleRotation();
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= shiftMultiplier;

        Vector3 pos = transform.position;
        pos += (forward * moveZ + right * moveX) * speed * Time.deltaTime;

        pos.x = Mathf.Clamp(pos.x, limitX.x, limitX.y);
        pos.z = Mathf.Clamp(pos.z, limitZ.x, limitZ.y);

        transform.position = pos;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        Vector3 pos = transform.position;
        pos.y -= scroll * zoomSpeed * Time.deltaTime * 100f;
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);

        transform.position = pos;
    }

    void HandleRotation()
    {
        if (Input.GetMouseButton(1))
        {
            float yaw = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            float pitchDelta = -Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;

            transform.Rotate(Vector3.up, yaw, Space.World);
            Vector3 localEuler = transform.localEulerAngles;
            float pitch = localEuler.x;
            if (pitch > 180f) pitch -= 360f;

            pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
            transform.localEulerAngles = new Vector3(pitch, transform.localEulerAngles.y, 0f);
        }
    }
}
