using UnityEngine;

public class MusicManager : MonoBehaviour
{
    // Statyczna zmienna, która będzie przechowywać odniesienie do jedynego działającego odtwarzacza
    public static MusicManager instance;

    void Awake()
    {
        // Sprawdzamy, czy instancja już istnieje
        if (instance == null)
        {
            // Jeśli nie, to ten obiekt staje się instancją
            instance = this;
            // I mówimy Unity: "Nie niszcz tego obiektu przy ładowaniu nowej sceny"
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Jeśli instancja już istnieje (np. wróciliśmy do Menu i Unity stworzyło drugi taki sam obiekt),
            // to niszczymy ten nowy, żeby muzyka się nie dublowała.
            Destroy(gameObject);
        }
    }
}