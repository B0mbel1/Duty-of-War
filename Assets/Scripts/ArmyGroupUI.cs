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

    // Funkcja inicjalizuj¹ca
    public void Setup(TacticalGroup group, ArmyHierarchyManager mgr)
    {
        myGroup = group;
        manager = mgr;

        // 1. Dodaj lub pobierz komponent LayoutElement (dla FlowLayout)
        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout == null) layout = gameObject.AddComponent<LayoutElement>();

        // 2. Logika Rozmiaru i Obrazka
        if (backgroundImage != null)
        {
            backgroundImage.preserveAspect = false;

            // Domyœlny rozmiar (kwadrat dla piechoty/³uczników)
            layout.preferredWidth = 80f;
            layout.preferredHeight = 80f;

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
                // KAWALERIA
                backgroundImage.sprite = shapeCavalry;
                backgroundImage.preserveAspect = true;

                // Szerszy przycisk dla kawalerii
                layout.preferredWidth = 140f;
                layout.preferredHeight = 60f;
            }
        }

        // 3. Tekst
        if (groupNameText != null)
        {
            groupNameText.text = group.unitMatrices.Count.ToString();
        }

        // --- ZMIANA: USUNIÊTO KOLOROWANIE FRAKCJI ---
        // Ustawiamy kolor na neutralny (Bia³y = brak tintu)
        backgroundImage.color = Color.white;
        // --------------------------------------------

        myButton.onClick.RemoveAllListeners();
        myButton.onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        manager.SelectGroup(myGroup);
    }

    public void SetSelectionState(bool isSelected)
    {
        if (myGroup.unitMatrices.Count == 0) return; // Jeœli martwy, nie zmieniaj kolorów

        if (isSelected)
            backgroundImage.color = Color.green; // Wybrany = Zielony
        else
            backgroundImage.color = Color.white; // Odznaczony = Neutralny (Bia³y)
    }

    public void UpdateUnitCount()
    {
        if (myGroup != null && groupNameText != null)
        {
            groupNameText.text = myGroup.unitMatrices.Count.ToString();

            // Jeœli oddzia³ wybity -> zrób szary i nieaktywny
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