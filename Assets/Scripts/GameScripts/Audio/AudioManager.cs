using UnityEngine;
using FMODUnity;

public class FMODAudioManager : MonoBehaviour
{
    public static FMODAudioManager Instance;

    private FMOD.Studio.Bus masterBus; // bus:/       → {f57a5233-4c1a-48bb-8ca3-e258cf994dda}
    private FMOD.Studio.Bus engineBus; // bus:/Engine → {b316cbc1-e18c-4b59-b572-fbc8ffeb01dc}
    private FMOD.Studio.Bus musicBus;  // bus:/SONGS → {c1f0b2d3-4c5e-4f6a-8b7c-9d8e0f1a2b3c}
    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float engineVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;

    private void Start()
    {
        // Load correct bus paths (case-sensitive!)
        masterBus = RuntimeManager.GetBus("bus:/");
        engineBus = RuntimeManager.GetBus("bus:/Engine");
        musicBus = RuntimeManager.GetBus("bus:/Music");
        Debug.Log($"Master Bus Valid: {masterBus.isValid()}");
        Debug.Log($"Engine Bus Valid: {engineBus.isValid()}");
        Debug.Log($"Music Bus Valid: {musicBus.isValid()}");
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
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

    public void SetEngineMuted(bool muted)
    {
        if (engineBus.isValid())
        {
            engineBus.setMute(muted);
        }
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

    private void OnDisable() => SaveSettings();
    private void OnApplicationQuit() => SaveSettings();
}
