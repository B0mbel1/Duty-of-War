using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro; // Niezbędne do obsługi Dropdowna

public class SettingsScript : MonoBehaviour
{
    public AudioMixer audioMixer;
    public Slider volumeSlider;
    public TMP_Dropdown qualityDropdown; // Pamiętaj, żeby przypisać to w Inspectorze!

    void OnEnable()
    {
        // --- WCZYTYWANIE GŁOŚNOŚCI ---
        float savedVolume = PlayerPrefs.GetFloat("savedVolume", 1f);
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
        }
        SetVolume(savedVolume);

        // --- WCZYTYWANIE JAKOŚCI GRAFIKI ---
        // 1. Pobieramy zapisaną liczbę (0, 1 lub 2). 
        // Liczba '1' na końcu to wartość domyślna (jeśli gracz włącza grę pierwszy raz, ustawi się Średnia)
        int savedQuality = PlayerPrefs.GetInt("qualityIndex", 1);

        // 2. Ustawiamy wizualnie Dropdown na odpowiednią pozycję
        if (qualityDropdown != null)
        {
            qualityDropdown.value = savedQuality;
            qualityDropdown.RefreshShownValue();
        }

        // 3. Ustawiamy faktyczną jakość w silniku Unity (bez zapisywania ponownego)
        QualitySettings.SetQualityLevel(savedQuality);
    }

    public void SetVolume(float volume)
    {
        if (volume <= 0)
        {
            audioMixer.SetFloat("MasterVolume", -80f);
        }
        else
        {
            audioMixer.SetFloat("MasterVolume", Mathf.Log10(volume) * 20);
        }

        // Zapisujemy głośność
        PlayerPrefs.SetFloat("savedVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetQuality(int qualityIndex)
    {
        // Ustawiamy jakość w silniku
        QualitySettings.SetQualityLevel(qualityIndex);

        // --- ZAPISYWANIE JAKOŚCI ---
        // Zapamiętujemy wybraną liczbę (np. 0 dla Niskiej, 2 dla Wysokiej)
        PlayerPrefs.SetInt("qualityIndex", qualityIndex);

        // Zmuszamy dysk do zapisu
        PlayerPrefs.Save();
    }
}