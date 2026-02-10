using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class SettingsScript : MonoBehaviour
{
    public AudioMixer audioMixer;
    public Slider volumeSlider;
    public TMP_Dropdown qualityDropdown;

    void OnEnable()
    {
        float savedVolume = PlayerPrefs.GetFloat("savedVolume", 1f);
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
        }
        SetVolume(savedVolume);

        int savedQuality = PlayerPrefs.GetInt("qualityIndex", 1);

        if (qualityDropdown != null)
        {
            qualityDropdown.value = savedQuality;
            qualityDropdown.RefreshShownValue();
        }

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

        PlayerPrefs.SetFloat("savedVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
        PlayerPrefs.SetInt("qualityIndex", qualityIndex);
        PlayerPrefs.Save();
    }
}