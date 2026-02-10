using UnityEngine;

public class TerrainFeature : MonoBehaviour
{
    [Header("Dane dla AI")]
    public string featureName;
    [TextArea] public string description;

    [Header("Ustawienia Linii")]
    public float lineLength = 20.0f;
    public Vector3 lineOrientation = Vector3.right; 

    [Header("Typ Terenu")]
    public bool isHill = false;
    public float widthOffset = 15.0f;


    [Header("Ustawienia Rzeczne")]
    public bool isRiver = false; 
    public float riverBankOffset = 10.0f;

    [Header("Debug")]
    public Color debugColor = Color.green;
    public float radius = 2.0f;

    public Vector3 GetWorldDirection()
    {
        return transform.TransformDirection(lineOrientation).normalized;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        Vector3 worldDir = GetWorldDirection();
        Vector3 start = transform.position - (worldDir * lineLength * 0.5f);
        Vector3 end = transform.position + (worldDir * lineLength * 0.5f);

        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(start, start + Vector3.up * 5);
        Gizmos.DrawLine(end, end + Vector3.up * 5);

        if (isRiver || isHill)
        {
            Gizmos.color = isRiver ? Color.blue : Color.yellow;
            Vector3 perp = Vector3.Cross(Vector3.up, worldDir) * widthOffset;
            Gizmos.DrawLine(start + perp, end + perp);
            Gizmos.DrawLine(start - perp, end - perp);

            if (isHill)
            {
                Gizmos.color = new Color(1, 0.92f, 0.016f, 0.3f);
                Gizmos.DrawLine(transform.position, transform.position + perp);
                Gizmos.DrawLine(transform.position, transform.position - perp);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        string type = isRiver ? "Rzeka" : (isHill ? "Wzgórze" : "Obszar");
        UnityEditor.Handles.Label(transform.position + Vector3.up * 5, $"{featureName}\nTyp: {type}\nDł: {lineLength:F0}m");
    }
#endif
}