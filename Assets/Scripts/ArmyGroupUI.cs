using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ArmyGroupUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI groupNameText; // To teraz bêdzie wyœwietlaæ liczbê
    public Image backgroundImage;
    public Button myButton;

    private TacticalGroup myGroup;
    private ArmyHierarchyManager manager;

    // Funkcja inicjalizuj¹ca (wywo³ywana przez Manager przy tworzeniu)
    public void Setup(TacticalGroup group, ArmyHierarchyManager mgr)
    {
        myGroup = group;
        manager = mgr;

        // --- ZMIANA TUTAJ ---
        // Zamiast nazwy grupy, wyœwietlamy iloœæ jednostek w liœcie
        if (groupNameText != null)
        {
            // Opcja 1: Sama liczba (np. "150") - czytelne na ma³ych przyciskach
            groupNameText.text = group.unitMatrices.Count.ToString();

            // Opcja 2: Jeœli wolisz format Nazwa + Liczba (np. "Inf: 150"), odkomentuj to:
            // groupNameText.text = $"{GetShortName(group.groupName)}\n{group.unitMatrices.Count}";
        }
        // --------------------

        // Ustawienie koloru w zale¿noœci od frakcji
        if (group.groupName.StartsWith("A")) backgroundImage.color = new Color(0.6f, 0.8f, 1f); // Niebieskawy
        else backgroundImage.color = new Color(1f, 0.6f, 0.6f); // Czerwonawy

        // Podpiêcie klikniêcia
        myButton.onClick.AddListener(OnClicked);
    }

    // Helper do skracania nazw (tylko jeœli u¿ywasz Opcji 2)
    string GetShortName(string fullName)
    {
        if (fullName.Contains("Infantry")) return "Piec.";
        if (fullName.Contains("Archer")) return "£ucz.";
        if (fullName.Contains("Cavalry")) return "Kawa.";
        return "Oddz.";
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
            // Reset koloru
            if (myGroup.groupName.StartsWith("A")) backgroundImage.color = new Color(0.6f, 0.8f, 1f);
            else backgroundImage.color = new Color(1f, 0.6f, 0.6f);
        }
    }

    // --- DODATEK: AKTUALIZACJA LICZBY ---
    // Tê metodê mo¿esz wywo³aæ, gdy ¿o³nierze zgin¹, aby odœwie¿yæ liczbê na przycisku
    public void UpdateUnitCount()
    {
        if (myGroup != null && groupNameText != null)
        {
            groupNameText.text = myGroup.unitMatrices.Count.ToString();

            // Jeœli oddzia³ wybity do zera, mo¿na np. wy³¹czyæ przycisk
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