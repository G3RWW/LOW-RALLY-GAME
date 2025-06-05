using UnityEngine;
using UnityEngine.UI;

public class FMODAudioUI : MonoBehaviour
{
    [Header("Audio Sliders")]
    public Slider masterSlider;
    public Slider engineSlider;
    public Slider musicSlider;

    [Header("RPM Display Toggles")]
    public Toggle rpmBarToggle;
    public Toggle rpmNeedleToggle;

    [Header("Gearbox Mode Toggles")]
    public Toggle manualToggle;
    public Toggle autoToggle;

    void Start()
    {
        var audio = FMODAudioManager.Instance;

        // Set initial slider values
        masterSlider.SetValueWithoutNotify(audio.masterVolume);
        engineSlider.SetValueWithoutNotify(audio.engineVolume);
        musicSlider.SetValueWithoutNotify(audio.musicVolume);

        // Bind sliders
        masterSlider.onValueChanged.AddListener(audio.SetMasterVolume);
        engineSlider.onValueChanged.AddListener(audio.SetEngineVolume);
        musicSlider.onValueChanged.AddListener(audio.SetMusicVolume);

        // Set initial RPM toggle states
        rpmBarToggle.SetIsOnWithoutNotify(RPMSettingsManager.Instance.ShowRPMBar);
        rpmNeedleToggle.SetIsOnWithoutNotify(RPMSettingsManager.Instance.ShowRPMNeedle);

        // RPM display logic (mutually exclusive)
        rpmBarToggle.onValueChanged.AddListener(value =>
        {
            if (value)
            {
                rpmNeedleToggle.SetIsOnWithoutNotify(false);
                OnRPMBarToggleChanged(true);
                OnRPMNeedleToggleChanged(false);
            }
            else if (!rpmNeedleToggle.isOn)
            {
                rpmBarToggle.SetIsOnWithoutNotify(true); // Prevent both off
            }
        });

        rpmNeedleToggle.onValueChanged.AddListener(value =>
        {
            if (value)
            {
                rpmBarToggle.SetIsOnWithoutNotify(false);
                OnRPMBarToggleChanged(false);
                OnRPMNeedleToggleChanged(true);
            }
            else if (!rpmBarToggle.isOn)
            {
                rpmNeedleToggle.SetIsOnWithoutNotify(true); // Prevent both off
            }
        });

        // Set initial gearbox toggle states
        manualToggle.SetIsOnWithoutNotify(GearboxSettingsManager.Instance.UseManual);
        autoToggle.SetIsOnWithoutNotify(!GearboxSettingsManager.Instance.UseManual);

        // Gearbox toggle logic (mutually exclusive)
        manualToggle.onValueChanged.AddListener(value =>
        {
            if (value)
            {
                autoToggle.SetIsOnWithoutNotify(false);
                OnGearboxToggleChanged(true);
            }
            else if (!autoToggle.isOn)
            {
                manualToggle.SetIsOnWithoutNotify(true); // Prevent both off
            }
        });

        autoToggle.onValueChanged.AddListener(value =>
        {
            if (value)
            {
                manualToggle.SetIsOnWithoutNotify(false);
                OnGearboxToggleChanged(false);
            }
            else if (!manualToggle.isOn)
            {
                autoToggle.SetIsOnWithoutNotify(true); // Prevent both off
            }
        });
    }

    void OnRPMBarToggleChanged(bool value)
    {
        RPMSettingsManager.Instance.SetShowRPMBar(value);

        var gm = FindObjectOfType<GameManager>();
        if (gm != null) gm.SetShowRPMBar(value);
    }

    void OnRPMNeedleToggleChanged(bool value)
    {
        RPMSettingsManager.Instance.SetShowRPMNeedle(value);

        var gm = FindObjectOfType<GameManager>();
        if (gm != null) gm.SetShowRPMNeedle(value);
    }

    void OnGearboxToggleChanged(bool useManual)
    {
        GearboxSettingsManager.Instance.SetManual(useManual);
    }

    private void OnDisable()
    {
        FMODAudioManager.Instance?.SaveSettings();
    }
}
