using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    public Slider masterSlider;
    public Slider engineSlider;
    public Slider musicSlider;

    [Header("UI References")]
    public GameObject previousPanel;  // <- Set this in the inspector

    void OnEnable()
    {
        if (SettingsManager.Instance == null) return;

        masterSlider.value = SettingsManager.Instance.masterVolume;
        engineSlider.value = SettingsManager.Instance.engineVolume;
        musicSlider.value = SettingsManager.Instance.musicVolume;
    }

    public void OnMasterChanged(float value)
    {
        SettingsManager.Instance.SetMasterVolume(value);
    }

    public void OnEngineChanged(float value)
    {
        SettingsManager.Instance.SetEngineVolume(value);
    }

    public void OnMusicChanged(float value)
    {
        SettingsManager.Instance.SetMusicVolume(value);
    }

    void OnDisable()
    {
        SettingsManager.Instance.SaveSettings(); // Auto-save on panel close
    }

    void OnApplicationQuit()
    {
        SettingsManager.Instance.SaveSettings(); // Auto-save on panel close
    }


    // 🔙 Call this from your Return Button
    public void OnReturnPressed()
    {
        gameObject.SetActive(false);          // Hide settings panel
        if (previousPanel != null)
            previousPanel.SetActive(true);    // Show previous panel (pause or main menu)
    }
}
