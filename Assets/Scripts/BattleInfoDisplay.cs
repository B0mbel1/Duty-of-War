using UnityEngine;
using TMPro;
using UnityEngine.UI;


public class BattleInfoDisplay : MonoBehaviour
{
    public TMP_Text sideAText; 
    public TMP_Text sideBText;

    private Button selectedButton = null;  // zapamiêtany klikniêty kafelek
    public Color selectedColor = Color.blue;     // kolor zaznaczonego kafelka
    public Color defaultColor = Color.white;
    public void ShowBattleInfo(string sideA, string sideB, Button clickedButton)
    {
        sideAText.text = sideA;
        sideBText.text = sideB;

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
        ShowBattleInfo("Rzeczpospolita", "Zakon Krzy¿acki",btn);
    }

    public void Battle_Hastings(Button btn)
    {
        ShowBattleInfo("Ksiêstwo Normandii","Anglosasi",btn);
    }

    public void Battle_Jarmuk(Button btn)
    {
        ShowBattleInfo("Bizancjum","Arabowie",btn);
    }

    public void Battle_AjnDzalat(Button btn)
    {
        ShowBattleInfo("Mamelucy", "Imperium Mongolskie",btn);
    }

    public void Battle_TeutoburgForest(Button btn)
    {
        ShowBattleInfo("Germanie","Cesarstwo Rzymskie",btn);
    }

    public void Battle_LechowePole(Button btn)
    {
        ShowBattleInfo("Królestwo Franków", "Wêgry",btn);
    }

    public void Battle_Adrianopol(Button btn)
    {
        ShowBattleInfo("Cesarstwo Rzysmkie", "Goci", btn);
    }
}
