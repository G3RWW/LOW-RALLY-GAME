using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class EngineSoundFMOD : MonoBehaviour
{
    [Header("FMOD Settings")]
    public EventReference engineEvent;
    public float minRPM = 800f;
    public float maxRPM = 7000f;

    [Header("Car Reference")]
    public CarController car;
    public Rigidbody carRigidbody; // 👈 assign this manually in Inspector

    private EventInstance engineInstance;
    private bool instanceIsValid = false;

    void Start()
    {
        if (car == null)
        {
            Debug.LogError($"[EngineSoundFMOD] ❌ CarController not assigned on {gameObject.name}");
            return;
        }

        if (carRigidbody == null)
        {
            Debug.LogError($"[EngineSoundFMOD] ❌ Rigidbody not assigned on {gameObject.name}. Refusing to continue to avoid FMOD auto-adding one.");
            return;
        }

        if (engineEvent.IsNull)
        {
            Debug.LogError($"[EngineSoundFMOD] ❌ Engine Event not assigned on {gameObject.name}");
            return;
        }

        // ✅ FMOD will NOT create Rigidbody here since carRigidbody is valid
        engineInstance = RuntimeManager.CreateInstance(engineEvent);
        RuntimeManager.AttachInstanceToGameObject(engineInstance, transform, carRigidbody);
        engineInstance.start();
        instanceIsValid = true;
    }


    void Update()
    {
        if (!instanceIsValid || car == null) return;

        float clampedRPM = Mathf.Clamp(car.currentRPM, minRPM, maxRPM);
        engineInstance.setParameterByName("RPM", clampedRPM);

        if (FMODAudioManager.Instance != null)
            engineInstance.setVolume(FMODAudioManager.Instance.engineVolume);
    }

    void OnDestroy()
    {
        if (instanceIsValid)
        {
            engineInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            engineInstance.release();
        }
    }
}
