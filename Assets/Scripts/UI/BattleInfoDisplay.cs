using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BattleInfoDisplay : MonoBehaviour
{
    [Header("Teksty")]
    public TMP_Text sideAText;
    public TMP_Text sideBText;
    public TMP_Text battleDescriptionText;
    public TMP_Text terrainDescription;

    [Header("Flagi")]
    public Image sideAImage;
    public Image sideBImage;

    [Header("Ramki pod flagami")]
    public GameObject sideABorder;
    public GameObject sideBBorder;
    private GameObject selectedBorder = null;
    private string selectedSideKey = "";

    [Header("Przyciski scenariuszy")]
    private Button selectedButton = null;
    public Color selectedColor = Color.blue;
    public Color defaultColor = Color.white;

    [Header("Sprite'y frakcji")]
    public Sprite roma, goci, germania, frankowie, wegry, rzeczpol, teutonic, normandia, bizantium, arabs, anglosas, mamelucy, mongol;

    [Header("Sprite'y map")]
    public Sprite LasTeutoburski;
    public Sprite Adrianopol;
    public Sprite Jarmuk;
    public Sprite LechowePole;
    public Sprite Hastings;
    public Sprite AjnDzalut;
    public Sprite Grunwald;

    public Image imageMap;

    private Sprite selectedMap = null;

    [Header("Przycisk Graj")]
    public Button playButton;

    public void SelectSideA()
    {
        SetBorder(sideABorder);
        selectedSideKey = "SideA";
    }

    public void SelectSideB()
    {
        SetBorder(sideBBorder);
        selectedSideKey = "SideB"; 
    }

    private void SetBorder(GameObject border)
    {
        if (selectedBorder != null)
            selectedBorder.SetActive(false);

        selectedBorder = border;
        selectedBorder.SetActive(true);

        if (playButton != null)
            playButton.interactable = true;
    }

    private void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(PlaySelectedMap);
            playButton.interactable = false;
        }
    }

    private void PlaySelectedMap()
    {
        if (selectedMap == null)
        {
            Debug.LogWarning("Nie wybrano mapy!");
            return;
        }

        if (selectedBorder == null || string.IsNullOrEmpty(selectedSideKey))
        {
            Debug.LogWarning("Nie wybrano strony konfliktu!");
            return;
        }

        PlayerPrefs.SetString("PlayerSide", selectedSideKey);
        PlayerPrefs.Save();

        string sceneName = "";

        if (selectedMap == LasTeutoburski)
            sceneName = "TeutoburgForest";
        else if (selectedMap == Adrianopol)
            sceneName = "Adrianopol";
        else if (selectedMap == Jarmuk)
            sceneName = "Jarmuk";
        else if (selectedMap == LechowePole)
            sceneName = "LechowePole";
        else if (selectedMap == Hastings)
            sceneName = "Hastings";
        else if (selectedMap == AjnDzalut)
            sceneName = "AjnDzalut";
        else if (selectedMap == Grunwald)
            sceneName = "Grunwald";


        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
        else
            Debug.LogError("Nie ustawiono nazwy sceny dla tej mapy!");
    }

    private void Awake()
    {
        if (sideABorder != null) sideABorder.SetActive(false);
        if (sideBBorder != null) sideBBorder.SetActive(false);
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

        if (sideAText != null) sideAText.text = "";
        if (sideBText != null) sideBText.text = "";
        if (battleDescriptionText != null) battleDescriptionText.text = "";
        if (terrainDescription != null) terrainDescription.text = "";
    }

    public void ShowBattleInfo(string sideA, string sideB, Button clickedButton, Sprite spriteA, Sprite spriteB, string description, Sprite image, string descriptionTerrain)
    {
        sideAText.text = sideA;
        sideBText.text = sideB;
        battleDescriptionText.text = description;
        terrainDescription.text = descriptionTerrain;
        sideAImage.sprite = spriteA;
        sideAImage.color = Color.white;
        sideBImage.sprite = spriteB;
        sideBImage.color = Color.white;

        ClearFactionSelection();

        if (selectedButton != null)
            SetButtonColor(selectedButton, defaultColor);

        selectedButton = clickedButton;
        SetButtonColor(selectedButton, selectedColor);

        selectedMap = image;
        imageMap.sprite = image;
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
        selectedSideKey = "";

        if (playButton != null)
            playButton.interactable = false;
    }

    public void Battle_Grunwald(Button btn)
    {
        string opis = "Bitwa pod Grunwaldem (1410 r.)\n\nWojska polsko-litewskie pod wodz� W�adys�awa Jagie��y rozgromi�y Zakon Krzy�acki. Jedno z najwi�kszych star� �redniowiecza, kt�re z�ama�o pot�g� Krzy�ak�w.";
        string opisterenu = "<color=#663300>Wzniesienia terenu</color>";
        ShowBattleInfo("Korona Kr�lestwa Polskiego", "Zakon Krzy�acki", btn, rzeczpol, teutonic, opis, Grunwald, opisterenu);
    }

    public void Battle_Hastings(Button btn)
    {
        string opis = "Bitwa pod Hastings (1066 r.)\n\nNormanowie Wilhelma Zdobywcy pokonali anglosask� armi� kr�la Harolda II. Bitwa zapocz�tkowa�a norma�skie panowanie w Anglii.";
        string opisterenu = "<color=#009933>Teren zalesiony</color>";
        ShowBattleInfo("Ksi�stwo Normandii", "Anglosasi", btn, normandia, anglosas, opis, Hastings, opisterenu);
    }

    public void Battle_Jarmuk(Button btn)
    {
        string opis = "Bitwa pod Jarmukiem (636 r.)\n\nDecyduj�ce zwyci�stwo Arab�w nad Bizancjum. Armia kalifatu Raszidun rozgromi�a wojska cesarza Herakliusza, otwieraj�c drog� do podboju Syrii i Lewantu.";
        string opisterenu = "<color=#6d6d6d>Teren g�rzysty</color> | <color=#3399ff>Przeprawa przez wod�</color>";
        ShowBattleInfo("Bizancjum", "Arabowie", btn, bizantium, arabs, opis, Jarmuk, opisterenu);
    }

    public void Battle_AjnDzalat(Button btn)
    {
        string opis = "Bitwa pod Ajn D�alat (1260 r.)\n\nMamelucy pod dow�dztwem Kutuzza i Bajbarsa pokonali armi� mongolsk�. By�o to pierwsze zdecydowane zwyci�stwo nad Mongo�ami, kt�re zatrzyma�o ich ekspansj� na Bliski Wsch�d.";
        string opisterenu = "<color=#663300>Wzniesienia terenu</color>";
        ShowBattleInfo("Mamelucy", "Imperium Mongolskie", btn, mamelucy, mongol, opis, AjnDzalut, opisterenu);
    }

    public void Battle_TeutoburgForest(Button btn)
    {
        string opis = "Bitwa w Lesie Teutoburskim (9 r. n.e.)\n\nZasadzka germa�skich plemion dowodzonych przez Arminiusza na trzy rzymskie legiony Warusa. Rzymianie ponie�li druzgoc�c� kl�sk�, trac�c kontrol� nad Germani�.";
        string opisterenu = "<color=#009933>Teren zalesiony</color>";
        ShowBattleInfo("Cesarstwo Rzymskie","Germanie", btn, roma, germania, opis, LasTeutoburski, opisterenu);
    }

    public void Battle_LechowePole(Button btn)
    {
        string opis = "Bitwa na Lechowym Polu (955 r.)\n\nKr�lestwo Frank�w pod wodz� Ottona I pokona�o najazd W�gr�w. Zwyci�stwo zako�czy�o w�gierskie najazdy na Europ� Zachodni� i wzmocni�o w�adz� cesarsk�.";
        string opisterenu = "<color=#663300>Wzniesienia terenu</color> | <color=#3399ff>Przeprawa przez wod�</color>";
        ShowBattleInfo("Pa�stwo wschodniofrankijskie", "W�gry", btn, frankowie, wegry, opis, LechowePole, opisterenu);
    }

    public void Battle_Adrianopol(Button btn)
    {
        string opis = "Bitwa pod Adrianopolem (378 r.)\n\nStarcie Cesarstwa Rzymskiego z Gotami. Rzym poni�s� wielk� pora�k�, zgin�� cesarz Walens, a bitwa zapocz�tkowa�a upadek zachodniego cesarstwa.";
        string opisterenu = "<color=#663300>Wzniesienia terenu</color> | <color=#3399ff>Przeprawa przez wod�</color>";
        ShowBattleInfo("Cesarstwo Rzymskie", "Goci", btn, roma, goci, opis, Adrianopol, opisterenu);
    }
}