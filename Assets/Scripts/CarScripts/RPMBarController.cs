using UnityEngine;
using UnityEngine.UI;

public class RPMBarController : MonoBehaviour
{
    public CarController carController; // Reference to CarController
    public Image rpmBar; // Assign the RPM bar UI image (jei nori spalvinti)
    public RectTransform rpmNeedle; // <-- Rodyklės (Pointer) RectTransform

    public Color lowRPMColor = Color.green;
    public Color midRPMColor = Color.yellow;
    public Color highRPMColor = Color.red;
    public float flashSpeed = 1.5f; // Speed of flashing when over redline
    public float flashIntensity = 0.6f; // Controls how much it dims (0 = full red, 1 = white)

    private float maxRPM;
    private float redlineRPM;
    private float idleRPM;
    private float targetFillAmount;
    private bool isFlashing = false;
    private float flashTimer = 0f;

    // Rodyklės sukimui
    public float minNeedleAngle = 120f;  // kai RPM mažas (0 RPM) (DEŠINĖJE)
    public float maxNeedleAngle = -120f; // kai RPM didelis (MAX RPM) (KAIRĖJE)

    public void Initialize(CarController car)
    {
        carController = car;

        if (carController == null || carController.carData == null)
        {
            Debug.LogError("🚨 RPMBarController: Missing CarController or CarData reference!");
            enabled = false;
            return;
        }

        // Get RPM settings from CarData
        maxRPM = carController.carData.redline;
        redlineRPM = carController.carData.redline;
        idleRPM = carController.carData.idleRPM;
        Debug.Log($"📊 RPMBarController initialized for: {car.name}");
    }


    void Update()
    {
        if (carController == null || carController.carData == null)
            return; // Prevent errors if components are missing

        float currentRPM = carController.currentRPM + 100; // Pridėtas mažas RPM poslinkis

        // Normalize RPM (0 = idle, 1 = max RPM)
        float normalizedRPM = Mathf.InverseLerp(idleRPM, maxRPM, currentRPM);
        targetFillAmount = normalizedRPM;

        // --- ⬇⬇⬇ RODYKLĖS SUKIMAS (VISADA) ⬇⬇⬇ ---
        if (rpmNeedle != null)
        {
            float needleAngle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, normalizedRPM);
            rpmNeedle.localRotation = Quaternion.Euler(0, 0, needleAngle);
        }

        // --- ⬇⬇⬇ RPM BAR SPALVINIMAS (TIK JEI RPM BAR YRA PRISKIRTAS) ⬇⬇⬇ ---
        if (rpmBar != null)
        {
            // Smooth fill animation
            rpmBar.fillAmount = Mathf.Lerp(rpmBar.fillAmount, targetFillAmount, Time.deltaTime * 10f);

            // Determine color based on RPM range
            if (normalizedRPM < 0.5f)
                rpmBar.color = Color.Lerp(lowRPMColor, midRPMColor, normalizedRPM * 2);
            else
                rpmBar.color = Color.Lerp(midRPMColor, highRPMColor, (normalizedRPM - 0.5f) * 2);

            // Handle smooth flashing effect if RPM exceeds redline
            if (currentRPM > redlineRPM)
            {
                if (!isFlashing)
                {
                    isFlashing = true;
                    flashTimer = 0f; // Reset flash timer
                }

                // Smooth fade effect using PingPong (creates a soft pulsing effect)
                flashTimer += Time.deltaTime * flashSpeed;
                float fade = Mathf.PingPong(flashTimer, flashIntensity);
                rpmBar.color = Color.Lerp(highRPMColor, Color.white, fade);
            }
            else
            {
                if (isFlashing)
                {
                    isFlashing = false;
                    rpmBar.color = highRPMColor; // Ensure solid red after flashing
                }
            }
        }
    }
}