using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections.Generic;

public class FMODMusicShuffler : MonoBehaviour
{
    public static FMODMusicShuffler Instance;

    [Header("FMOD Music Tracks")]
    public List<EventReference> musicTracks;

    private Queue<EventReference> shuffleQueue = new Queue<EventReference>();
    private EventInstance currentInstance;
    private bool isPlaying = false;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PrepareShuffleQueue();
        PlayNextTrack();
    }

    void Update()
    {
        if (!isPlaying) return;

        currentInstance.getPlaybackState(out PLAYBACK_STATE state);
        if (state == PLAYBACK_STATE.STOPPED)
        {
            PlayNextTrack();
        }
    }

    void PrepareShuffleQueue()
    {
        List<EventReference> shuffled = new List<EventReference>(musicTracks);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int j = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        shuffleQueue = new Queue<EventReference>(shuffled);
    }

    void PlayNextTrack()
    {
        if (shuffleQueue.Count == 0)
            PrepareShuffleQueue();

        EventReference nextTrack = shuffleQueue.Dequeue();

        if (nextTrack.IsNull)
        {
            Debug.LogWarning("FMODMusicShuffler: Encountered null track. Skipping.");
            return;
        }

        currentInstance = RuntimeManager.CreateInstance(nextTrack);
        currentInstance.start();
        currentInstance.release(); // Let FMOD clean up
        isPlaying = true;

        Debug.Log($"🎵 Now playing: {nextTrack.Path}");
    }

    public void StopMusic()
    {
        if (!isPlaying) return;

        currentInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        isPlaying = false;
    }

    private void OnDestroy()
    {
        StopMusic();
    }
}
