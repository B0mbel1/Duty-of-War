using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BattleInfoDisplay : MonoBehaviour
{
    [Header("Teksty")]
    public TMP_Text sideAText;
    public TMP_Text sideBText;
    public TMP_Text battleDescriptionText;

    [Header("Flagi")]
    public Image sideAImage;
    public Image sideBImage;

    [Header("Ramki pod flagami")]
    public GameObject sideABorder;
    public GameObject sideBBorder;
    private GameObject selectedBorder = null;

    [Header("Przyciski scenariuszy")]
    private Button selectedButton = null;
    public Color selectedColor = Color.blue;
    public Color defaultColor = Color.white;

    [Header("Sprite'y frakcji")]
    public Sprite roma, goci, germania, frankowie, wegry, rzeczpol, teutonic, normandia, bizantium, arabs, anglosas, mamelucy, mongol;

    // -----------------------------
    // Zaznaczanie strony A
    public void SelectSideA() { SetBorder(sideABorder); }

    // Zaznaczanie strony B
    public void SelectSideB() { SetBorder(sideBBorder); }

    // W³¹cz/wy³¹cz ramkê pod flag¹
    private void SetBorder(GameObject border)
    {
        if (selectedBorder != null)
            selectedBorder.SetActive(false);

        selectedBorder = border;
        selectedBorder.SetActive(true);
    }
    private void Awake()
    {
        // Ukryj ramki
        if (sideABorder != null) sideABorder.SetActive(false);
        if (sideBBorder != null) sideBBorder.SetActive(false);

        // Wyczyœæ flagi i ustaw kolor na przezroczysty
        if (sideAImage != null)
        {
            sideAImage.sprite = null;
            sideAImage.color = new Color(1f, 1f, 1f, 0f);
        }
        if (sideBImage != null)
        {
            sideBImage.sprite = null;
            sideBImage.color = new Color(1f, 1f, 1f, 0f);
        }

        // Wyczyœæ teksty
        if (sideAText != null) sideAText.text = "";
        if (sideBText != null) sideBText.text = "";
        if (battleDescriptionText != null) battleDescriptionText.text = "";
    }


    // -----------------------------
    // Pokazuje info o bitwie
    public void ShowBattleInfo(string sideA, string sideB, Button clickedButton, Sprite spriteA, Sprite spriteB, string description)
    {
        sideAText.text = sideA;
        sideBText.text = sideB;
        battleDescriptionText.text = description;

        // Flagi i pe³na widocznoœæ
        sideAImage.sprite = spriteA;
        sideAImage.color = Color.white;

        sideBImage.sprite = spriteB;
        sideBImage.color = Color.white;

        // Czyœæ zaznaczenie frakcji (ramki)
        ClearFactionSelection();

        // Kolor przycisku scenariusza
        if (selectedButton != null)
            SetButtonColor(selectedButton, defaultColor);

        selectedButton = clickedButton;
        SetButtonColor(selectedButton, selectedColor);
    }

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

    private void ClearFactionSelection()
    {
        if (selectedBorder != null)
            selectedBorder.SetActive(false);

        selectedBorder = null;
    }

    // -----------------------------
    // Metody bitew
    public void Battle_Grunwald(Button btn)
    {
        string opis = "Bitwa pod Grunwaldem (1410 r.)\n\nWojska polsko-litewskie pod wodz¹ W³adys³awa Jagie³³y rozgromi³y Zakon Krzy¿acki. Jedno z najwiêkszych staræ œredniowiecza, które z³ama³o potêgê Krzy¿aków.";
        ShowBattleInfo("Korona Królestwa Polskiego", "Zakon Krzy¿acki", btn, rzeczpol, teutonic, opis);
    }

    public void Battle_Hastings(Button btn)
    {
        string opis = "Bitwa pod Hastings (1066 r.)\n\nNormanowie Wilhelma Zdobywcy pokonali anglosask¹ armiê króla Harolda II. Bitwa zapocz¹tkowa³a normañskie panowanie w Anglii.";
        ShowBattleInfo("Ksiêstwo Normandii", "Anglosasi", btn, normandia, anglosas, opis);
    }

    public void Battle_Jarmuk(Button btn)
    {
        string opis = "Bitwa pod Jarmukiem (636 r.)\n\nDecyduj¹ce zwyciêstwo Arabów nad Bizancjum. Armia kalifatu Raszidun rozgromi³a wojska cesarza Herakliusza, otwieraj¹c drogê do podboju Syrii i Lewantu.";
        ShowBattleInfo("Bizancjum", "Arabowie", btn, bizantium, arabs, opis);
    }

    public void Battle_AjnDzalat(Button btn)
    {
        string opis = "Bitwa pod Ajn D¿alat (1260 r.)\n\nMamelucy pod dowództwem Kutuzza i Bajbarsa pokonali armiê mongolsk¹. By³o to pierwsze zdecydowane zwyciêstwo nad Mongo³ami, które zatrzyma³o ich ekspansjê na Bliski Wschód.";
        ShowBattleInfo("Mamelucy", "Imperium Mongolskie", btn, mamelucy, mongol, opis);
    }

    public void Battle_TeutoburgForest(Button btn)
    {
        string opis = "Bitwa w Lesie Teutoburskim (9 r. n.e.)\n\nZasadzka germañskich plemion dowodzonych przez Arminiusza na trzy rzymskie legiony Warusa. Rzymianie ponieœli druzgoc¹c¹ klêskê, trac¹c kontrolê nad Germani¹.";
        ShowBattleInfo("Germanie", "Cesarstwo Rzymskie", btn, germania, roma, opis);
    }

    public void Battle_LechowePole(Button btn)
    {
        string opis = "Bitwa na Lechowym Polu (955 r.)\n\nKrólestwo Franków pod wodz¹ Ottona I pokona³o najazd Wêgrów. Zwyciêstwo zakoñczy³o wêgierskie najazdy na Europê Zachodni¹ i wzmocni³o w³adzê cesarsk¹.";
        ShowBattleInfo("Pañstwo wschodniofrankijskie", "Wêgry", btn, frankowie, wegry, opis);
    }

    public void Battle_Adrianopol(Button btn)
    {
        string opis = "Bitwa pod Adrianopolem (378 r.)\n\nStarcie Cesarstwa Rzymskiego z Gotami. Rzym poniós³ wielk¹ pora¿kê, zgin¹³ cesarz Walens, a bitwa zapocz¹tkowa³a upadek zachodniego cesarstwa.";
        ShowBattleInfo("Cesarstwo Rzymskie", "Goci", btn, roma, goci, opis);
    }
}
