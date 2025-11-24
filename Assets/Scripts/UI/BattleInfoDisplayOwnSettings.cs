using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BattleInfoDisplayOwnSettings : MonoBehaviour
{
    public TMP_Text terrainDescription;

    [Header("Sprite'y map")]
    public Sprite WybrzezePalGalur;
    public Sprite WzgorzaZamieci;
    public Sprite OrleGniazdo;

    [Header("Przyciski scenariuszy")]
    public Button WybrzezePalGalurButton;
    public Button WzgorzaZamieciButton;
    public Button OrleGniazdoButton;

    private Button selectedButton = null;
    private Sprite selectedMap = null;

    public Color selectedColor = Color.blue;
    public Color defaultColor = Color.white;

    [Header("Obraz mapy")]
    public Image imageMap;

    [Header("Przycisk Graj")]
    public Button playButton;

    private void Start()
    {
        if (playButton != null)
            playButton.onClick.AddListener(PlaySelectedMap);
    }

    // -----------------------------
    // Metody wywo³ywane w OnClick() przycisków map
    public void SelectWybrzezePalGalur()
    {
        SelectMap(WybrzezePalGalur, WybrzezePalGalurButton);
        string opisterenu = "Wzniesienia terenu | Teren górzysty | Przeprawa przez wodê";
        terrainDescription.text = opisterenu;
    }

    public void SelectWzgorzaZamieci()
    {
        SelectMap(WzgorzaZamieci, WzgorzaZamieciButton);
        string opisterenu = "Wzniesienia terenu | Teren górzysty | Teren zalesiony";
        terrainDescription.text = opisterenu;
    }

    public void SelectOrleGniazdo()
    {
        SelectMap(OrleGniazdo, OrleGniazdoButton);
        string opisterenu = "Du¿e wziesienie terenu";
        terrainDescription.text = opisterenu;
    }

    // -----------------------------
    private void SelectMap(Sprite map, Button btn)
    {
        selectedMap = map;

        // Ustaw obraz w UI
        imageMap.sprite = selectedMap;

        // Zmieñ kolor przycisku
        if (selectedButton != null)
            SetButtonColor(selectedButton, defaultColor);

        selectedButton = btn;
        SetButtonColor(selectedButton, selectedColor);
    }

    // -----------------------------
    private void PlaySelectedMap()
    {
        if (selectedMap == null)
        {
            Debug.LogWarning("Nie wybrano mapy!");
            return;
        }

        // Tutaj musisz wpisaæ nazwê sceny dla ka¿dej mapy
        string sceneName = "";

        if (selectedMap == WybrzezePalGalur)
            sceneName = "Wybrze¿e Pal Galur";
        else if (selectedMap == WzgorzaZamieci)
            sceneName = "Wzgórza Zamieci";
        else if (selectedMap == OrleGniazdo)
            sceneName = "Orle Gniazdo";

        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
        else
            Debug.LogError("Nie ustawiono nazwy sceny dla tej mapy!");
    }

    // -----------------------------
    private void SetButtonColor(Button btn, Color color)
    {
        btn.image.color = color;
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color;
        cb.pressedColor = color;
        cb.selectedColor = color;
        btn.colors = cb;
    }
}
