using UnityEngine;

public class TerrainFeature : MonoBehaviour
{
    [Header("Dane dla AI")]
    public string featureName; 
    [TextArea] public string description; 

    [Header("Ustawienia Linii Frontu")]
    public float lineLength = 20.0f; // Długość linii (szerokość formacji)
    public Vector3 lineOrientation = Vector3.right; // W którą stronę biegnie linia (Right = poziomo, Forward = pionowo)

    [Header("Debug")]
    public Color debugColor = Color.green;
    public float radius = 2.0f; 

    void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        
        // Rysujemy sferę w punkcie centralnym (punkt zaczepienia)
        Gizmos.DrawWireSphere(transform.position, radius);
        
        // Rysujemy LINIĘ FRONTU
        // Obliczamy początek i koniec linii względem środka
        Vector3 start = transform.position - (lineOrientation * lineLength * 0.5f);
        Vector3 end = transform.position + (lineOrientation * lineLength * 0.5f);
        
        // Gruba linia wyznaczająca front
        Gizmos.DrawLine(start, end);
        
        // Dodatkowe "wąsy" na końcach, żeby było widać granice w edytorze
        Gizmos.DrawLine(start, start + Vector3.up * 5);
        Gizmos.DrawLine(end, end + Vector3.up * 5);
    }
    
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(transform.position + Vector3.up * 4, featureName + $"\n(Szer: {lineLength:F0}m)");
    }
#endif
}