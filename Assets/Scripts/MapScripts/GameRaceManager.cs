using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManagerRace : MonoBehaviour
{
    public static GameManagerRace Instance;

    [Header("Race Settings")]
    public int targetLapCount = 3;
    public string playerCarTag = "playerCar"; // Use tag to identify player's car

    private List<LapTimer> allCars = new();
    private LapTimer playerLapTimer;
    private bool raceFinished = false;

    [Header("UI")]
    public GameObject leaderboardPanel;
    public Transform leaderboardContainer; // parent where entries go
    public GameObject leaderboardEntryPrefab; // prefab for row (name + lap + time)


    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        FindAllCars();
    }

    void Update()
    {
        if (raceFinished) return;

        if (playerLapTimer != null && playerLapTimer.completedLaps >= targetLapCount)
        {
            raceFinished = true;
            Debug.Log("🏁 Race Finished!");
            ShowFinalLeaderboard();
        }
    }

    void FindAllCars()
    {
        allCars = FindObjectsOfType<LapTimer>().ToList();

        foreach (var car in allCars)
        {
            if (car.CompareTag(playerCarTag))
                playerLapTimer = car;
        }

        if (playerLapTimer == null)
            Debug.LogError("🚨 Player car not found! Set tag correctly.");
    }

    void ShowFinalLeaderboard()
    {
        Time.timeScale = 0f; // ✅ Pause game

        var sorted = allCars
            .OrderByDescending(c => c.completedLaps)
            .ThenBy(c => c.GetTotalTime())
            .ToList();

        Debug.Log("=== 🏆 FINAL LEADERBOARD ===");

        foreach (Transform child in leaderboardContainer)
        {
            Destroy(child.gameObject); // clear previous entries
        }

        for (int i = 0; i < sorted.Count; i++)
        {
            var entry = Instantiate(leaderboardEntryPrefab, leaderboardContainer);
            var name = sorted[i].gameObject.name;
            var laps = sorted[i].completedLaps;
            var time = sorted[i].GetTotalTime();

            entry.GetComponentInChildren<UnityEngine.UI.Text>().text =
                $"{i + 1}. {name} - Laps: {laps}, Time: {time:F2}s";
        }

        leaderboardPanel.SetActive(true);
    }


    public List<LapTimer> GetLiveLeaderboard()
    {
        return allCars
            .OrderByDescending(c => c.completedLaps)
            .ThenBy(c => c.GetTotalTime())
            .ToList();
    }
    
    public void RestartRace()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

}
