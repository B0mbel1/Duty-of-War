using UnityEngine;
using TMPro;
using UnityEngine.UI;


public class BattleInfoDisplay : MonoBehaviour
{
    public TMP_Text sideAText;
    public TMP_Text sideBText;
    public TMP_Text battleDescriptionText; // << NOWE POLE NA OPIS


    public Image sideAImage;    // Obiekt Image dla strony A
    public Image sideBImage;    // Obiekt Image dla strony B

    // Tu przypisujesz grafiki w Inspectorze
    public Sprite roma;
    public Sprite goci;
    public Sprite germania;
    public Sprite frankowie;
    public Sprite wegry;
    public Sprite rzeczpol;
    public Sprite teutonic;
    public Sprite normandia;
    public Sprite bizantium;
    public Sprite arabs;
    public Sprite anglosas;
    public Sprite mamelucy;
    public Sprite mongol;

    private Button selectedButton = null;  // zapamiêtany klikniêty kafelek
    public Color selectedColor = Color.blue;     // kolor zaznaczonego kafelka
    public Color defaultColor = Color.white;
    public void ShowBattleInfo(string sideA, string sideB, Button clickedButton, Sprite spriteA, Sprite spriteB, string description)
    {
        sideAText.text = sideA;
        sideBText.text = sideB;
        battleDescriptionText.text = description;


        sideAImage.sprite = spriteA;
        sideBImage.sprite = spriteB;


        // Przywróæ poprzedni przycisk do normalnego koloru
        if (selectedButton != null)
        {
            SetButtonColor(selectedButton, defaultColor);
        }

        // Ustaw nowy aktualnie wybrany przycisk
        selectedButton = clickedButton;
        SetButtonColor(selectedButton, selectedColor);
    }
    private void SetButtonColor(Button btn, Color color)
    {
        // Ustaw kolor t³a obrazka
        btn.image.color = color;

        // Zablokuj podœwietlenie (¿ó³te/ciemne od Unity)
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color;
        cb.pressedColor = color;
        cb.selectedColor = color;
        btn.colors = cb;
    }

    public void Battle_Grunwald(Button btn)
    {
        string opis = "Bitwa pod Grunwaldem (1410 r.)\r\nWojska polsko-litewskie pod wodz¹ W³adys³awa Jagie³³y rozgromi³y Zakon Krzy¿acki. Jedno z najwiêkszych staræ œredniowiecza, które z³ama³o potêgê Krzy¿aków.";
        ShowBattleInfo("Korona Królestwa Polskiego", "Zakon Krzy¿acki", btn, rzeczpol, teutonic, opis);
    }

    public void Battle_Hastings(Button btn)
    {
        string opis = "Bitwa pod Hastings (1066 r.)\r\nNormanowie Wilhelma Zdobywcy pokonali anglosask¹ armiê króla Harolda II. Bitwa zapocz¹tkowa³a normañskie panowanie w Anglii.";
        ShowBattleInfo("Ksiêstwo Normandii", "Anglosasi", btn, normandia, anglosas, opis);
    }

    public void Battle_Jarmuk(Button btn)
    {
        string opis = "Bitwa pod Jarmukiem (636 r.)\r\nDecyduj¹ce zwyciêstwo Arabów nad Bizancjum. Armia kalifatu Raszidun rozgromi³a wojska cesarza Herakliusza, otwieraj¹c drogê do podboju Syrii i Lewantu.";
        ShowBattleInfo("Bizancjum", "Arabowie", btn, bizantium, arabs, opis);
    }

    public void Battle_AjnDzalat(Button btn)
    {
        string opis = "Bitwa pod Ajn D¿alat (1260 r.)\r\nMamelucy pod dowództwem Kutuzza i Bajbarsa pokonali armiê mongolsk¹. By³o to pierwsze zdecydowane zwyciêstwo nad Mongo³ami, które zatrzyma³o ich ekspansjê na Bliski Wschód.";
        ShowBattleInfo("Mamelucy", "Imperium Mongolskie", btn, mamelucy, mongol, opis);
    }

    public void Battle_TeutoburgForest(Button btn)
    {
        string opis = "Bitwa w Lesie Teutoburskim (9 r. n.e.)\r\nZasadzka germañskich plemion dowodzonych przez Arminiusza na trzy rzymskie legiony Warusa. Rzymianie ponieœli druzgoc¹c¹ klêskê, trac¹c kontrolê nad Germani¹.";
        ShowBattleInfo("Germanie", "Cesarstwo Rzymskie", btn, germania, roma, opis);
    }

    public void Battle_LechowePole(Button btn)
    {
        string opis = "Bitwa na Lechowym Polu (955 r.)\r\nKrólestwo Franków pod wodz¹ Ottona I pokona³o najazd Wêgrów. Zwyciêstwo zakoñczy³o wêgierskie najazdy na Europê Zachodni¹ i wzmocni³o w³adzê cesarsk¹.";
        ShowBattleInfo("Pañstwo wschodniofrankijskie", "Wêgry", btn, frankowie, wegry, opis);
    }

    public void Battle_Adrianopol(Button btn)
    {
        string opis = "Bitwa pod Adrianopolem (378 r.)\r\nStarcie Cesarstwa Rzymskiego z Gotami. Rzym poniós³ wielk¹ pora¿kê, zgin¹³ cesarz Walens, a bitwa zapocz¹tkowa³a upadek zachodniego cesarstwa.";
        ShowBattleInfo("Cesarstwo Rzymskie", "Goci", btn, roma, goci, opis);
    }
}
