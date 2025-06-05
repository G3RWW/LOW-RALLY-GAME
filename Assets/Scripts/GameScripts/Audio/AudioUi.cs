using UnityEngine;
using UnityEngine.UI;

public class FMODAudioUI : MonoBehaviour
{
    public Slider masterSlider;
    public Slider engineSlider;
    public Slider musicSlider;

    void Start()
    {
        var audio = FMODAudioManager.Instance;

        // Set initial UI values
        masterSlider.SetValueWithoutNotify(audio.masterVolume);
        engineSlider.SetValueWithoutNotify(audio.engineVolume);
        musicSlider.SetValueWithoutNotify(audio.musicVolume);

        // Bind UI changes to audio manager
        masterSlider.onValueChanged.AddListener(audio.SetMasterVolume);
        engineSlider.onValueChanged.AddListener(audio.SetEngineVolume);
        musicSlider.onValueChanged.AddListener(audio.SetMusicVolume);
    }

    private void OnDisable()
    {
        // Save settings when panel is closed or destroyed
        FMODAudioManager.Instance?.SaveSettings();
    }
}
