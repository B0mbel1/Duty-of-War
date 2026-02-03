using UnityEngine;

public class TerrainFeature : MonoBehaviour
{
    [Header("Dane dla AI")]
    public string featureName; 
    [TextArea] public string description; 

    [Header("Ustawienia Linii")]
    public float lineLength = 20.0f; 
    public Vector3 lineOrientation = Vector3.right; 
    
    [Header("Ustawienia Rzeczne")]
    public bool isRiver = false; // Czy to jest rzeka?
    public float riverBankOffset = 10.0f; // Jak daleko od środka rzeki jest brzeg (bezpieczna strefa)

    [Header("Debug")]
    public Color debugColor = Color.green;
    public float radius = 2.0f;

    void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        
        Vector3 start = transform.position - (lineOrientation * lineLength * 0.5f);
        Vector3 end = transform.position + (lineOrientation * lineLength * 0.5f);
        
        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(start, start + Vector3.up * 5);
        Gizmos.DrawLine(end, end + Vector3.up * 5);

        // Wizualizacja brzegów (tylko dla rzek)
        if (isRiver)
        {
            Gizmos.color = Color.blue;
            Vector3 perp = Vector3.Cross(Vector3.up, lineOrientation).normalized * riverBankOffset;
            Gizmos.DrawLine(start + perp, end + perp); // Brzeg A
            Gizmos.DrawLine(start - perp, end - perp); // Brzeg B
        }
    }
    
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(transform.position + Vector3.up * 4, featureName + $"\n(Szer: {lineLength:F0}m)");
    }
#endif
}