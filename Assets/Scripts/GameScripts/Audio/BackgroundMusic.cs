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
    private Stack<EventReference> playedHistory = new Stack<EventReference>();
    private EventInstance currentInstance;
    private EventReference currentTrack;
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

        if (Input.GetKeyDown(KeyCode.M))
        {
            PlayNextTrack(true);
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            PlayPreviousTrack();
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

    void PlayNextTrack(bool manualSkip = false)
    {
        if (manualSkip && !currentTrack.IsNull)
        {
            playedHistory.Push(currentTrack);
        }

        if (shuffleQueue.Count == 0)
            PrepareShuffleQueue();

        StopMusic();

        currentTrack = shuffleQueue.Dequeue();

        if (currentTrack.IsNull)
        {
            Debug.LogWarning("FMODMusicShuffler: Encountered null track. Skipping.");
            return;
        }

        currentInstance = RuntimeManager.CreateInstance(currentTrack);
        currentInstance.start();
        currentInstance.release();
        isPlaying = true;

        Debug.Log($"🎵 Now playing: {currentTrack.Path}");
    }

    void PlayPreviousTrack()
    {
        if (playedHistory.Count == 0)
        {
            Debug.Log("FMODMusicShuffler: No previous track.");
            return;
        }

        StopMusic();

        currentTrack = playedHistory.Pop();
        currentInstance = RuntimeManager.CreateInstance(currentTrack);
        currentInstance.start();
        currentInstance.release();
        isPlaying = true;

        Debug.Log($"⏪ Now playing: {currentTrack.Path}");
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
