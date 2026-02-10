using UnityEngine;
using UnityEngine.UI;

public class CompassUI : MonoBehaviour
{
    [Header("Przypisz w Inspectorze")]
    public RectTransform compassRoot; 
    public Transform cameraTransform;

    void Start()
    {
        if (cameraTransform == null) 
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        if (cameraTransform == null || compassRoot == null) return;

        float camY = cameraTransform.eulerAngles.y;
        compassRoot.localEulerAngles = new Vector3(0, 0, camY);
    }
}