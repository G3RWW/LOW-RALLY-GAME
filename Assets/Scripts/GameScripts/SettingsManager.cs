using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    public float masterVolume = 1f;
    public float engineVolume = 1f;
    public float musicVolume = 1f;

    private VCA masterVCA;
    private VCA engineVCA;
    private VCA musicVCA;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            masterVCA = RuntimeManager.GetVCA("vca:/Master");
            engineVCA = RuntimeManager.GetVCA("vca:/Engine");
            musicVCA = RuntimeManager.GetVCA("vca:/Music");
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        masterVCA.setVolume(value);
    }

    public void SetEngineVolume(float value)
    {
        engineVolume = value;
        engineVCA.setVolume(value);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = value;
        musicVCA.setVolume(value);
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

        masterVCA.setVolume(masterVolume);
        engineVCA.setVolume(engineVolume);
        musicVCA.setVolume(musicVolume);
    }
}
