using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ArmyGroupUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI groupNameText;
    public Image backgroundImage;
    public Button myButton;

    [Header("Unit Shapes (Przypisz Sprites w Inspectorze)")]
    public Sprite shapeInfantry; // Tu przypisz Kó³ko (np. 'Knob')
    public Sprite shapeArcher;   // Tu przypisz Kwadrat (np. 'UISprite')
    public Sprite shapeCavalry;  // Tu przypisz Cylinder/Kapsu³kê

    private TacticalGroup myGroup;
    private ArmyHierarchyManager manager;

    // Funkcja inicjalizuj¹ca
    public void Setup(TacticalGroup group, ArmyHierarchyManager mgr)
    {
        myGroup = group;
        manager = mgr;

        // 1. Ustawienie Liczby
        if (groupNameText != null)
        {
            groupNameText.text = group.unitMatrices.Count.ToString();
        }

        // 2. ZMIANA KSZTA£TU (Nowoœæ)
        if (backgroundImage != null)
        {
            // Sprawdzamy nazwê grupy i zmieniamy obrazek
            if (group.groupName.Contains("Infantry") && shapeInfantry != null)
            {
                backgroundImage.sprite = shapeInfantry;
            }
            else if (group.groupName.Contains("Archer") && shapeArcher != null)
            {
                backgroundImage.sprite = shapeArcher;
            }
            else if (group.groupName.Contains("Cavalry") && shapeCavalry != null)
            {
                backgroundImage.sprite = shapeCavalry;
            }

            // Opcjonalnie: Zachowaj proporcje obrazka, ¿eby cylinder nie by³ rozci¹gniêty
            // backgroundImage.preserveAspect = true; 
        }

        // 3. Ustawienie Koloru Frakcji
        Color baseColor;
        if (group.groupName.StartsWith("A")) baseColor = new Color(0.6f, 0.8f, 1f); // Niebieski
        else baseColor = new Color(1f, 0.6f, 0.6f); // Czerwony

        backgroundImage.color = baseColor;

        // Podpiêcie klikniêcia
        myButton.onClick.RemoveAllListeners();
        myButton.onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        manager.SelectGroup(myGroup);
    }

    public void SetSelectionState(bool isSelected)
    {
        if (isSelected)
            backgroundImage.color = Color.green; // Wybrany
        else
        {
            // Przywróæ kolor frakcji
            if (myGroup.groupName.StartsWith("A")) backgroundImage.color = new Color(0.6f, 0.8f, 1f);
            else backgroundImage.color = new Color(1f, 0.6f, 0.6f);
        }
    }

    public void UpdateUnitCount()
    {
        if (myGroup != null && groupNameText != null)
        {
            groupNameText.text = myGroup.unitMatrices.Count.ToString();

            if (myGroup.unitMatrices.Count == 0)
            {
                myButton.interactable = false;
                backgroundImage.color = Color.gray;
            }
        }
    }

    public TacticalGroup GetGroup()
    {
        return myGroup;
    }
}