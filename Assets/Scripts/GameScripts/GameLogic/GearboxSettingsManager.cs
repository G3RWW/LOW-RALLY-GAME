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

        // 🔥 Update the player's gearbox mode immediately
        var playerCar = GameObject.FindGameObjectWithTag("playerCar");
        if (playerCar != null)
        {
            var controller = playerCar.GetComponent<CarController>();
            if (controller != null)
            {
                controller.isAutomatic = !value;
                Debug.Log($"⚙️ Gearbox mode updated: {(value ? "Manual" : "Automatic")}");
            }
            else
            {
                Debug.LogWarning("🚫 CarController not found on playerCar.");
            }
        }
        else
        {
            Debug.LogWarning("🚫 playerCar GameObject not found.");
        }
    }

    private void LoadSetting()
    {
        UseManual = PlayerPrefs.GetInt("useManual", 0) == 1;
    }
}
