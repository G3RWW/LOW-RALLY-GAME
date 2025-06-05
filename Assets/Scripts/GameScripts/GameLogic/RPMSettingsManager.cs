using UnityEngine;

public class RPMSettingsManager : MonoBehaviour
{
    public static RPMSettingsManager Instance;

    public bool ShowRPMBar { get; private set; } = true;
    public bool ShowRPMNeedle { get; private set; } = false;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    public void SetShowRPMBar(bool value)
    {
        ShowRPMBar = value;
        PlayerPrefs.SetInt("showRPMBar", value ? 1 : 0);
    }

    public void SetShowRPMNeedle(bool value)
    {
        ShowRPMNeedle = value;
        PlayerPrefs.SetInt("showRPMNeedle", value ? 1 : 0);
    }

    void LoadSettings()
    {
        ShowRPMBar = PlayerPrefs.GetInt("showRPMBar", 1) == 1;
        ShowRPMNeedle = PlayerPrefs.GetInt("showRPMNeedle", 0) == 1;
    }
}
