using UnityEngine;
using UnityEngine.UI;

public class RPMSettingsUI : MonoBehaviour
{
    public Toggle rpmBarToggle;
    public Toggle rpmNeedleToggle;

    void Start()
    {
        rpmBarToggle.isOn = RPMSettingsManager.Instance.ShowRPMBar;
        rpmNeedleToggle.isOn = RPMSettingsManager.Instance.ShowRPMNeedle;

        rpmBarToggle.onValueChanged.AddListener(OnRPMBarToggleChanged);
        rpmNeedleToggle.onValueChanged.AddListener(OnRPMNeedleToggleChanged);
    }

    void OnRPMBarToggleChanged(bool value)
    {
        RPMSettingsManager.Instance.SetShowRPMBar(value);
    }

    void OnRPMNeedleToggleChanged(bool value)
    {
        RPMSettingsManager.Instance.SetShowRPMNeedle(value);
    }
}
