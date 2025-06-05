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

    private Bus engineBus;

    [Header("Car Reference")]
    public CarController car;

    private EventInstance engineInstance;
    private bool instanceIsValid = false;

    void Awake()
    {
        if (car == null)
        {
            car = GetComponent<CarController>();
        }

        engineBus = RuntimeManager.GetBus("bus:/Engine");
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

        // 🔊 Set volume from global audio manager
        if (FMODAudioManager.Instance != null)
        {
            engineInstance.setVolume(FMODAudioManager.Instance.engineVolume);
        }
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
