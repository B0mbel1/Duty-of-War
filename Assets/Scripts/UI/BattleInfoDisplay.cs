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
        string opis = "Battle of Grunwald (1410)\n\nPolish-Lithuanian forces led by Wladyslaw Jagiello crushed the Teutonic Order. One of the largest battles of the Middle Ages, which broke the power of the Teutonic Knights.";
        string opisterenu = "<color=#663300>Terrain Elevation</color>";
        ShowBattleInfo("Crown of the Kingdom of Poland", "Teutonic Order", btn, rzeczpol, teutonic, opis, Grunwald, opisterenu);
    }

    public void Battle_Hastings(Button btn)
    {
        string opis = "Battle of Hastings (1066)\n\nWilliam the Conqueror's Normans defeated King Harold II's Anglo-Saxon army. The battle marked the beginning of Norman rule in England.";
        string opisterenu = "<color=#009933>Forested Terrain</color>";
        ShowBattleInfo("Duchy of Normandy", "Anglo-Saxons", btn, normandia, anglosas, opis, Hastings, opisterenu);
    }

    public void Battle_Jarmuk(Button btn)
    {
        string opis = "Battle of the Yarmouk (636)\n\nA decisive Arab victory over Byzantium. The Rashidun Caliphate army crushed the forces of Emperor Heraclius, opening the way to the conquest of Syria and the Levant.";
        string opisterenu = "<color=#6d6d6d>Mountainous Terrain</color> | <color=#3399ff>Water Crossing</color>";
        ShowBattleInfo("Byzantium", "Arabs", btn, bizantium, arabs, opis, Jarmuk, opisterenu);
    }

    public void Battle_AjnDzalat(Button btn)
    {
        string opis = "Battle of Ain Jalut (1260)\n\nThe Mamluks under the command of Qutuz and Baibars defeated the Mongol army. It was the first decisive victory over the Mongols, halting their expansion into the Middle East.";
        string opisterenu = "<color=#663300>Terrain Elevation</color>";
        ShowBattleInfo("Mamluks", "Mongol Empire", btn, mamelucy, mongol, opis, AjnDzalut, opisterenu);
    }

    public void Battle_TeutoburgForest(Button btn)
    {
        string opis = "Battle of the Teutoburg Forest (9 AD)\n\nAn ambush by Germanic tribes led by Arminius on Varus's three Roman legions. The Romans suffered a devastating defeat, losing control over Germania.";
        string opisterenu = "<color=#009933>Forested Terrain</color>";
        ShowBattleInfo("Roman Empire","Germanic Tribes", btn, roma, germania, opis, LasTeutoburski, opisterenu);
    }

    public void Battle_LechowePole(Button btn)
    {
        string opis = "Battle of Lechfeld (955)\n\nThe Kingdom of the Franks led by Otto I defeated the Hungarian invasion. The victory ended Hungarian raids into Western Europe and strengthened imperial authority.";
        string opisterenu = "<color=#663300>Terrain Elevation</color> | <color=#3399ff>Water Crossing</color>";
        ShowBattleInfo("East Francia", "Hungary", btn, frankowie, wegry, opis, LechowePole, opisterenu);
    }

    public void Battle_Adrianopol(Button btn)
    {
        string opis = "Battle of Adrianople (378)\n\nA clash between the Roman Empire and the Goths. Rome suffered a major defeat, Emperor Valens was killed, and the battle marked the beginning of the fall of the Western Empire.";
        string opisterenu = "<color=#663300>Terrain Elevation</color> | <color=#3399ff>Water Crossing</color>";
        ShowBattleInfo("Roman Empire", "Goths", btn, roma, goci, opis, Adrianopol, opisterenu);
    }
}