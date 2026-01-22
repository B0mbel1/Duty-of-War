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

        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout == null) layout = gameObject.AddComponent<LayoutElement>();

        if (backgroundImage != null)
        {
            backgroundImage.preserveAspect = false;

            // --- ZMIANA LOGIKI WYGLĄDU ---
            if (group.isMergedGroup)
            {
                // Jeśli to grupa gracza (złączona) -> Prostokąt i wyświetl nazwę
                layout.preferredWidth = 250f; // Szeroki prostokąt
                layout.preferredHeight = 60f;

                // Jeśli masz Sprite prostokąta, przypisz go tutaj, 
                // jeśli nie - Unity użyje domyślnego kwadratu rozciągniętego na prostokąt.
                // backgroundImage.sprite = null; // Opcjonalnie reset do domyślnego UI
            }
            else
            {
                // Stara logika dla zwykłych oddziałów
                layout.preferredWidth = 80f;
                layout.preferredHeight = 80f;

                if (group.groupName.Contains("Infantry") && shapeInfantry != null) backgroundImage.sprite = shapeInfantry;
                else if (group.groupName.Contains("Archer") && shapeArcher != null) backgroundImage.sprite = shapeArcher;
                else if (group.groupName.Contains("Cavalry") && shapeCavalry != null)
                {
                    backgroundImage.sprite = shapeCavalry;
                    backgroundImage.preserveAspect = true;
                    layout.preferredWidth = 140f;
                    layout.preferredHeight = 60f;
                }
            }
        }

        // --- TEKST ---
        if (groupNameText != null)
        {
            // Jeśli grupa ma własną nazwę, wyświetl ją. Jeśli nie - pokaż liczbę jednostek.
            if (!string.IsNullOrEmpty(group.customName))
            {
                groupNameText.text = group.customName + "\n(" + group.unitMatrices.Count + ")";
                groupNameText.fontSize = 14; // Trochę mniejsza czcionka dla dłuższej nazwy
            }
            else
            {
                groupNameText.text = group.unitMatrices.Count.ToString();
            }
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