using UnityEngine;

public class TerrainFeature : MonoBehaviour
{
    [Header("Dane dla AI")]
    public string featureName; // np. "Forest_North"
    [TextArea] public string description; // np. "Las dający bonus do obrony"

    [Header("Debug (Tylko w Edytorze)")]
    public Color debugColor = Color.green;
    public float radius = 5.0f; // Jak duży jest ten obszar (opcjonalne, dla Twojej wiedzy)

    void OnDrawGizmos()
    {
        // To rysuje się tylko w oknie Scene, żebyś widział gdzie są znaczniki
        Gizmos.color = debugColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        
        // Rysowanie ikony lub kropki w centrum
        Gizmos.DrawSphere(transform.position, 1.0f);
    }
    
    // Opcjonalnie: Wyświetlanie nazwy nad obiektem w edytorze (wymaga UnityEditor)
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, featureName);
    }
#endif
}