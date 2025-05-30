using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Rigidbody))]
public class EngineSoundFMOD : MonoBehaviour
{
    [Header("FMOD Settings")]
    public EventReference engineEvent;
    public float minRPM = 800f;
    public float maxRPM = 7000f;

    public float engineVolume = 1f; // 🎚️ Inspector slider 
    [Range(0f, 1f)]

    private Bus engineBus; // 🎚️ Inspector slider

    [Header("Car Reference")]
    public CarController car;

    [Header("Manual Volume Control")]
    [Range(0f, 1f)] public float volume = 1f; // 🎚️ Inspector slider

    private EventInstance engineInstance;
    private bool instanceIsValid = false;

    void Awake()
    {
        if (car == null)
        {
            car = GetComponent<CarController>();
        }

        engineBus = RuntimeManager.GetBus("bus:/Engine"); // 🎚️ Inspector slider
    }

    void Start()
    {
        if (!engineEvent.IsNull)
        {
            engineInstance = RuntimeManager.CreateInstance(engineEvent);
            RuntimeManager.AttachInstanceToGameObject(engineInstance, transform, GetComponent<Rigidbody>());
            engineInstance.start();
            instanceIsValid = true;
        }
        else
        {
            Debug.LogError("[EngineSoundFMOD] Engine event not assigned!");
        }
    }

    void Update()
    {
        if (!instanceIsValid || car == null) return;

        float clampedRPM = Mathf.Clamp(car.currentRPM, minRPM, maxRPM);
        engineInstance.setParameterByName("RPM", clampedRPM);

        engineInstance.setVolume(volume);// 🎚️ Inspector slider

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
