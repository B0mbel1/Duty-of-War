using UnityEngine;
using UnityEngine.UI; // Wymagane dla UI

public class FlagChange : MonoBehaviour
{
    public Image panelLewy;
    public Image panelPrawy;
    public Sprite Flaga1;
    public Sprite Flaga2;

    private void Start()
    {
        ZmienObraz(1, Flaga1);
        ZmienObraz(2, Flaga2);
    }

    public void ZmienObraz(int ktoryPanel, Sprite nowaGrafika)
    {
        if (nowaGrafika == null) return;

        if (ktoryPanel == 1 && panelLewy != null)
        {
            panelLewy.sprite = nowaGrafika;
        }
        else if (ktoryPanel == 2 && panelPrawy != null)
        {
            panelPrawy.sprite = nowaGrafika;
        }
    }
}