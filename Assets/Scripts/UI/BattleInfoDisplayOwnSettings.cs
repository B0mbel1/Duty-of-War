using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BattleInfoDisplayOwnSettings : MonoBehaviour
{

    [Header("Sprite'y map")]
    public Sprite WybrzezePalGalur;
    public Sprite WzgorzaZamieci;
    public Sprite OrleGniazdo;


    public Image imageMap;
    public void ShowBattleInfo(Sprite image)
    {
        imageMap.sprite = image;
    }
}
