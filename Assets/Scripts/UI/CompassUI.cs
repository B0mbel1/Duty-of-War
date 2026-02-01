using UnityEngine;
using UnityEngine.UI;

public class CompassUI : MonoBehaviour
{
    [Header("Przypisz w Inspectorze")]
    public RectTransform compassRoot; // To jest ten element, który ma się kręcić (Igła lub cała Róża)
    public Transform cameraTransform; // Twoja Main Camera

    void Start()
    {
        // Auto-wykrywanie kamery, jeśli zapomnisz przypisać
        if (cameraTransform == null) 
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        if (cameraTransform == null || compassRoot == null) return;

        // Pobieramy obrót kamery wokół osi Y (lewo-prawo)
        float camY = cameraTransform.eulerAngles.y;

        // W Unity UI obrót dodatni jest przeciwny do wskazówek zegara.
        // Jeśli kamera patrzy w prawo (90 stopni), Północ jest po naszej lewej (-90 stopni na ekranie).
        // Dlatego po prostu przypisujemy ten sam kąt do osi Z w UI.
        
        compassRoot.localEulerAngles = new Vector3(0, 0, camY);
    }
}