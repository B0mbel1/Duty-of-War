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
    public Sprite shapeInfantry;
    public Sprite shapeArcher;
    public Sprite shapeCavalry;

    private TacticalGroup myGroup;
    private ArmyHierarchyManager manager;

    // Funkcja inicjalizuj�ca
    public void Setup(TacticalGroup group, ArmyHierarchyManager mgr)
    {
        myGroup = group;
        manager = mgr;

        // 1. Dodajemy/Pobieramy LayoutElement - to on mówi "Horizontal Layout Group" ile miejsca potrzebujemy
        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout == null) layout = gameObject.AddComponent<LayoutElement>();

        // 2. Logika rozmiaru
        if (group.isMergedGroup)
        {
            // --- DLA CUSTOMOWYCH (DUŻYCH) ---
            // Ustawiamy szerszy element (np. 100 pikseli zamiast 50)
            layout.minWidth = 100f;
            layout.preferredWidth = 100f;

            layout.minHeight = 40f;  // Wysokość taka sama jak reszty (żeby pasowało do paska)
            layout.preferredHeight = 40f;
        }
        else
        {
            // --- DLA ZWYKŁYCH (STANDARDOWE 50x40) ---
            // To odpowiada Twojemu staremu Cell Size
            layout.minWidth = 50f;
            layout.preferredWidth = 50f;

            layout.minHeight = 40f;
            layout.preferredHeight = 40f;
        }

        // 3. Reszta logiki (obrazki i tekst) bez zmian
        if (backgroundImage != null)
        {
            backgroundImage.preserveAspect = false;

            if (group.isMergedGroup)
            {
                // Tutaj np. brak obrazka lub specjalna ramka dla połączonych
                // backgroundImage.sprite = null; 
            }
            else
            {
                if (group.groupName.Contains("Infantry") && shapeInfantry != null)
                    backgroundImage.sprite = shapeInfantry;
                else if (group.groupName.Contains("Archer") && shapeArcher != null)
                    backgroundImage.sprite = shapeArcher;
                else if (group.groupName.Contains("Cavalry") && shapeCavalry != null)
                {
                    backgroundImage.sprite = shapeCavalry;
                    backgroundImage.preserveAspect = true;
                }
            }
        }

        if (groupNameText != null)
        {
            if (!string.IsNullOrEmpty(group.customName))
                groupNameText.text = group.customName + "\n(" + group.unitMatrices.Count + ")";
            else
                groupNameText.text = group.unitMatrices.Count.ToString();
        }

        backgroundImage.color = Color.white;
        myButton.onClick.RemoveAllListeners();
        myButton.onClick.AddListener(OnClicked);
    }
    void OnClicked()
    {
        manager.SelectGroup(myGroup);
    }

    public void SetSelectionState(bool isSelected)
    {
        if (myGroup.unitMatrices.Count == 0) return; // Je�li martwy, nie zmieniaj kolor�w

        if (isSelected)
            backgroundImage.color = Color.green; // Wybrany = Zielony
        else
            backgroundImage.color = Color.white; // Odznaczony = Neutralny (Bia�y)
    }

    public void UpdateUnitCount()
    {
        if (myGroup != null && groupNameText != null)
        {
            if (!string.IsNullOrEmpty(myGroup.customName))
                groupNameText.text = myGroup.customName + "\n(" + myGroup.unitMatrices.Count + ")";
            else
                groupNameText.text = myGroup.unitMatrices.Count.ToString();

            // Je�li oddzia� wybity -> zr�b szary i nieaktywny
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