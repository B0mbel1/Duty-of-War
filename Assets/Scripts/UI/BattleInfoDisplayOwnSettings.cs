using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class BattleInfoDisplayOwnSettings : MonoBehaviour
{
    public static int redInfantryCount = 0;
    public static int redRangeCount = 0;
    public static int redCavalryCount = 0;
    public static int blueInfantryCount = 0;
    public static int blueRangeCount = 0;
    public static int blueCavalryCount = 0;

    [Header("UI - Powiadomienia")]
    public GameObject errorTextObject;

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
    public TMP_InputField inputRedInfantry;
    public TMP_InputField inputRedRange;
    public TMP_InputField inputRedCavalry;

    public TMP_InputField inputBlueInfantry;
    public TMP_InputField inputBlueRange;
    public TMP_InputField inputBlueCavalry;

    private void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(PlaySelectedMap);
            playButton.interactable = false;
        }
        if (errorTextObject != null) errorTextObject.SetActive(false);
        SetDefaultValues();
        AddValidationListeners();
    }

    private void SetDefaultValues()
    {
        if (inputRedInfantry) inputRedInfantry.text = "0";
        if (inputRedRange) inputRedRange.text = "0";
        if (inputRedCavalry) inputRedCavalry.text = "0";

        if (inputBlueInfantry) inputBlueInfantry.text = "0";
        if (inputBlueRange) inputBlueRange.text = "0";
        if (inputBlueCavalry) inputBlueCavalry.text = "0";
    }

    private void AddValidationListeners()
    {
        if (inputRedInfantry) inputRedInfantry.onEndEdit.AddListener(val => ValidateInput(val, inputRedInfantry));
        if (inputRedRange) inputRedRange.onEndEdit.AddListener(val => ValidateInput(val, inputRedRange));
        if (inputRedCavalry) inputRedCavalry.onEndEdit.AddListener(val => ValidateInput(val, inputRedCavalry));

        if (inputBlueInfantry) inputBlueInfantry.onEndEdit.AddListener(val => ValidateInput(val, inputBlueInfantry));
        if (inputBlueRange) inputBlueRange.onEndEdit.AddListener(val => ValidateInput(val, inputBlueRange));
        if (inputBlueCavalry) inputBlueCavalry.onEndEdit.AddListener(val => ValidateInput(val, inputBlueCavalry));
    }

    private void ValidateInput(string value, TMP_InputField field)
    {
        if (string.IsNullOrEmpty(value))
        {
            field.text = "0";
            return;
        }
        bool isNumber = int.TryParse(value, out int result);
        bool hasLeadingZeros = (value.Length > 1 && value.StartsWith("0"));
        bool isNegative = (result < 0);

        if (!isNumber || hasLeadingZeros || isNegative)
        {
            ShowErrorNotification("NIEDOZWOLONY FORMAT!");

            if (isNumber && hasLeadingZeros)
                field.text = result.ToString();
            else
                field.text = "0";
        }
    }

    private void ShowErrorNotification(string message)
    {
        if (errorTextObject != null)
        {
            TMP_Text txt = errorTextObject.GetComponent<TMP_Text>();
            if (txt != null) txt.text = message;

            errorTextObject.SetActive(true);

            StopAllCoroutines();
            StartCoroutine(HideErrorAfterDelay(3.0f));
        }
        Debug.LogWarning(message);
    }

    private IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (errorTextObject != null)
            errorTextObject.SetActive(false);
    }

    public void SelectWybrzezePalGalur()
    {
        SelectMap(WybrzezePalGalur, WybrzezePalGalurButton);
        if (terrainDescription) terrainDescription.text = "<color=#663300>Wzniesienia terenu</color> | <color=#6d6d6d>Teren g�rzysty</color> | <color=#3399ff>Przeprawa przez wod�</color>";
    }

    public void SelectWzgorzaZamieci()
    {
        SelectMap(WzgorzaZamieci, WzgorzaZamieciButton);
        if (terrainDescription) terrainDescription.text = "<color=#663300>Wzniesienia terenu</color> | <color=#6d6d6d>Teren g�rzysty</color> | <color=#009933>Teren zalesiony</color>";
    }

    public void SelectOrleGniazdo()
    {
        SelectMap(OrleGniazdo, OrleGniazdoButton);
        if (terrainDescription) terrainDescription.text = "<color=#663300>Du�e wzniesienie terenu</color>";
    }

    private void SelectMap(Sprite map, Button btn)
    {
        selectedMap = map;

        if (imageMap != null) imageMap.sprite = selectedMap;

        if (selectedButton != null)
            SetButtonColor(selectedButton, defaultColor);

        selectedButton = btn;
        SetButtonColor(selectedButton, selectedColor);

        if (playButton != null)
            playButton.interactable = true;
    }

    private void PlaySelectedMap()
    {
        if (selectedMap == null) return;

        SaveUnitSettings();
        int totalRed = redInfantryCount + redRangeCount + redCavalryCount;
        int totalBlue = blueInfantryCount + blueRangeCount + blueCavalryCount;

        if (totalBlue <= 0)
        {
            ShowErrorNotification("GRACZ MUSI MIEĆ WOJSKO!");
            return;
        }

        if (totalRed <= 0)
        {
            ShowErrorNotification("PRZECIWNIK MUSI MIEĆ WOJSKO!");
            return; 
        }
        PlayerPrefs.SetString("PlayerSide", "SideA");
        PlayerPrefs.Save();

        string sceneName = "";
        if (selectedMap == WybrzezePalGalur) sceneName = "Wybrzeze Pal Galur";
        else if (selectedMap == WzgorzaZamieci) sceneName = "Wzgorza Zamieci";
        else if (selectedMap == OrleGniazdo) sceneName = "Orle Gniazdo";

        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
        else
            Debug.LogError("Nie ustawiono nazwy sceny dla tej mapy!");
    }

    private void SaveUnitSettings()
    {
        if (inputRedInfantry) int.TryParse(inputRedInfantry.text, out redInfantryCount);
        if (inputRedRange) int.TryParse(inputRedRange.text, out redRangeCount);
        if (inputRedCavalry) int.TryParse(inputRedCavalry.text, out redCavalryCount);

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