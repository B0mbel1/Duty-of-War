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

    public void Setup(TacticalGroup group, ArmyHierarchyManager mgr)
    {
        myGroup = group;
        manager = mgr;

        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
        if (group.isMergedGroup)
        {
            layout.minWidth = 100f;
            layout.preferredWidth = 100f;

            layout.minHeight = 40f; 
            layout.preferredHeight = 40f;
        }
        else
        {
            layout.minWidth = 50f;
            layout.preferredWidth = 50f;

            layout.minHeight = 40f;
            layout.preferredHeight = 40f;
        }

        if (backgroundImage != null)
        {
            backgroundImage.preserveAspect = false;

            if (group.isMergedGroup)
            {
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
        if (myGroup.unitMatrices.Count == 0) return; 

        if (isSelected)
            backgroundImage.color = Color.green;
        else
            backgroundImage.color = Color.white;
    }

    public void UpdateUnitCount()
    {
        if (myGroup != null && groupNameText != null)
        {
            if (!string.IsNullOrEmpty(myGroup.customName))
                groupNameText.text = myGroup.customName + "\n(" + myGroup.unitMatrices.Count + ")";
            else
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