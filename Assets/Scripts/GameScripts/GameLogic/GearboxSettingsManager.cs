using UnityEngine;

public class GearboxSettingsManager : MonoBehaviour
{
    public static GearboxSettingsManager Instance;

    public bool UseManual { get; private set; } = false;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSetting();
    }

    public void SetManual(bool value)
    {
        UseManual = value;
        PlayerPrefs.SetInt("useManual", value ? 1 : 0);
        PlayerPrefs.Save();

        // Try to apply immediately to existing playerCar (if in menu scene)
        var playerCar = GameObject.FindGameObjectWithTag("playerCar");
        if (playerCar != null)
        {
            ApplyGearboxTo(playerCar);
        }
    }

    public void ApplyGearboxTo(GameObject car)
    {
        if (car == null)
        {
            Debug.LogWarning("🚫 ApplyGearboxTo: car is null.");
            return;
        }

        var controller = car.GetComponent<CarController>();
        if (controller != null)
        {
            controller.isAutomatic = !UseManual;
            Debug.Log($"⚙️ Gearbox mode applied: {(UseManual ? "Manual" : "Automatic")}");
        }
        else
        {
            Debug.LogWarning("🚫 CarController not found on car.");
        }
    }

    private void LoadSetting()
    {
        UseManual = PlayerPrefs.GetInt("useManual", 0) == 1;
    }
}
