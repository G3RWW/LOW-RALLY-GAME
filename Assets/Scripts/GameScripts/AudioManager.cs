using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections.Generic;

public class FMODBackgroundMusic : MonoBehaviour
{
    [Header("Music Tracks (for shuffling)")]
    public List<EventReference> musicTracks = new();

    private EventInstance musicInstance;
    private EventReference currentTrack;

    void Start()
    {
        PlayRandomTrack();
    }

    public void PlayRandomTrack()
    {
        if (musicTracks.Count == 0)
        {
            Debug.LogWarning("No music tracks assigned.");
            return;
        }

        StopMusic(); // In case something was playing

        currentTrack = musicTracks[Random.Range(0, musicTracks.Count)];
        musicInstance = RuntimeManager.CreateInstance(currentTrack);
        musicInstance.start();
    }

    public void StopMusic(bool fadeOut = true)
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(fadeOut ?  FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
            musicInstance.release();
        }
    }

    public void SetVolume(float volume)
    {
        if (musicInstance.isValid())
            musicInstance.setVolume(Mathf.Clamp01(volume));
    }

    void OnDestroy()
    {
        StopMusic(false);
    }
}
