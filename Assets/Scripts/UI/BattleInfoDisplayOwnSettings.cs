using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BattleInfoDisplayOwnSettings : MonoBehaviour
{
    // --- DANE STATYCZNE (Liczebnoœæ armii) ---
    public static int redInfantryCount = 0;
    public static int redRangeCount = 0;
    public static int redCavalryCount = 0;

    public static int blueInfantryCount = 0;
    public static int blueRangeCount = 0;
    public static int blueCavalryCount = 0;
    // -----------------------------------------

    [Header("Opis Terenu")]
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

    [Header("Ustawienia Armii (Input Fields)")]
    // Pola tekstowe Czerwonych (Przeciwnik)
    public TMP_InputField inputRedInfantry;
    public TMP_InputField inputRedRange;
    public TMP_InputField inputRedCavalry;

    // Pola tekstowe Niebieskich (Gracz)
    public TMP_InputField inputBlueInfantry;
    public TMP_InputField inputBlueRange;
    public TMP_InputField inputBlueCavalry;

    private void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(PlaySelectedMap);
            // Przycisk nieaktywny dopóki nie wybierzesz mapy
            playButton.interactable = false;
        }

        SetDefaultValues();
    }

    private void SetDefaultValues()
    {
        // Domyœlne wartoœci wpisane w pola
        if (inputRedInfantry) inputRedInfantry.text = "0";
        if (inputRedRange) inputRedRange.text = "0";
        if (inputRedCavalry) inputRedCavalry.text = "0";

        if (inputBlueInfantry) inputBlueInfantry.text = "0";
        if (inputBlueRange) inputBlueRange.text = "0";
        if (inputBlueCavalry) inputBlueCavalry.text = "0";
    }

    // -----------------------------
    // WYBÓR MAPY
    // -----------------------------

    public void SelectWybrzezePalGalur()
    {
        SelectMap(WybrzezePalGalur, WybrzezePalGalurButton);
        if (terrainDescription) terrainDescription.text = "Wzniesienia terenu | Teren górzysty | Przeprawa przez wodê";
    }

    public void SelectWzgorzaZamieci()
    {
        SelectMap(WzgorzaZamieci, WzgorzaZamieciButton);
        if (terrainDescription) terrainDescription.text = "Wzniesienia terenu | Teren górzysty | Teren zalesiony";
    }

    public void SelectOrleGniazdo()
    {
        SelectMap(OrleGniazdo, OrleGniazdoButton);
        if (terrainDescription) terrainDescription.text = "Du¿e wzniesienie terenu";
    }

    private void SelectMap(Sprite map, Button btn)
    {
        selectedMap = map;

        if (imageMap != null) imageMap.sprite = selectedMap;

        if (selectedButton != null)
            SetButtonColor(selectedButton, defaultColor);

        selectedButton = btn;
        SetButtonColor(selectedButton, selectedColor);

        // Po wybraniu mapy od razu mo¿na graæ
        if (playButton != null)
            playButton.interactable = true;
    }

    // -----------------------------
    // ROZPOCZÊCIE GRY
    // -----------------------------

    private void PlaySelectedMap()
    {
        if (selectedMap == null) return;

        // 1. Zapisz liczebnoœæ armii
        SaveUnitSettings();

        // 2. WYMUSZENIE STRONY NIEBIESKIEJ (SideB)
        // Gracz zawsze steruje stron¹ B
        PlayerPrefs.SetString("PlayerSide", "SideB");
        PlayerPrefs.Save();

        // 3. Za³aduj scenê
        string sceneName = "";
        if (selectedMap == WybrzezePalGalur) sceneName = "Wybrze¿e Pal Galur";
        else if (selectedMap == WzgorzaZamieci) sceneName = "Wzgórza Zamieci";
        else if (selectedMap == OrleGniazdo) sceneName = "Orle Gniazdo";

        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
        else
            Debug.LogError("Nie ustawiono nazwy sceny dla tej mapy!");
    }

    private void SaveUnitSettings()
    {
        // Parsowanie Czerwonych
        if (inputRedInfantry) int.TryParse(inputRedInfantry.text, out redInfantryCount);
        if (inputRedRange) int.TryParse(inputRedRange.text, out redRangeCount);
        if (inputRedCavalry) int.TryParse(inputRedCavalry.text, out redCavalryCount);

        // Parsowanie Niebieskich
        if (inputBlueInfantry) int.TryParse(inputBlueInfantry.text, out blueInfantryCount);
        if (inputBlueRange) int.TryParse(inputBlueRange.text, out blueRangeCount);
        if (inputBlueCavalry) int.TryParse(inputBlueCavalry.text, out blueCavalryCount);
    }

    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        btn.image.color = color;
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color;
        cb.pressedColor = color;
        cb.selectedColor = color;
        btn.colors = cb;
    }
}