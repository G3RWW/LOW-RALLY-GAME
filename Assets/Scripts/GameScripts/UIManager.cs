using UnityEngine;
using FMODUnity;

public class UISettingsManager : MonoBehaviour
{
    public static UISettingsManager Instance;
    private FMOD.Studio.Bus masterBus;
    private FMOD.Studio.Bus engineBus;
    private FMOD.Studio.Bus musicBus;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float engineVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load buses
        masterBus = RuntimeManager.GetBus("bus:/");
        engineBus = RuntimeManager.GetBus("bus:/Engine");
        musicBus = RuntimeManager.GetBus("bus:/Music");

        // Log if bus not found
        Debug.Log($"🔊 Master Bus valid: {masterBus.isValid()}");
        Debug.Log($"🔊 Engine Bus valid: {engineBus.isValid()}");
        Debug.Log($"🔊 Music Bus valid: {musicBus.isValid()}");

        LoadSettings();
        StartCoroutine(ApplyAfterDelay());
    }

    void Start()
    {
        UISettingsManager.Instance.ApplyAllSettings(); // ← re-applies volumes + reconnects UI
    }


    private System.Collections.IEnumerator ApplyAfterDelay()
    {
        yield return new WaitForSeconds(0.1f); // ensure FMOD is ready
        ApplyAllSettings();
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        if (masterBus.isValid()) masterBus.setVolume(value);
    }

    public void SetEngineVolume(float value)
    {
        engineVolume = value;
        if (engineBus.isValid()) engineBus.setVolume(value);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = value;
        if (musicBus.isValid()) musicBus.setVolume(value);
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("EngineVolume", engineVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.Save();
    }

    public void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        engineVolume = PlayerPrefs.GetFloat("EngineVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
    }

    public void ApplyAllSettings()
    {
        SetMasterVolume(masterVolume);
        SetEngineVolume(engineVolume);
        SetMusicVolume(musicVolume);
    }


    void OnDisable()
    {
        SaveSettings();
    }

    void OnApplicationQuit()
    {
        SaveSettings();
    }
}
