using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LapTimer : MonoBehaviour
{
    [Header("Waypoints")]
    public List<Transform> normalWaypoints;               // Trigger points for normal lap route
    public List<Transform> jokerWaypoints;                // Trigger points for joker lap route
    public Transform jokerLapExitPoint;                   // Where Joker Lap ends

    [Header("UI Elements")]
    public Text lapText;
    public Text lapTimeText;
    public Text lastLapTimeText;
    public Text bestLapTimeText;

    [Header("Tracking")]
    public float totalTime = 0f;
    public int completedLaps = 0;
    public bool hasUsedJokerLap = false;

    private int currentNormalIndex = 0;
    private int currentJokerIndex = 0;
    private bool isInJokerLap = false;

    private float currentLapTime = 0f;
    private float lastLapTime = 0f;
    public float bestLapTime = Mathf.Infinity;

    private bool hasStarted = false;

    void Update()
    {
        if (hasStarted)
        {
            totalTime += Time.deltaTime;
            currentLapTime += Time.deltaTime;
        }

        UpdateUI();
    }

    void OnTriggerEnter(Collider other)
    {
        // Joker Lap logic
        if (isInJokerLap && jokerWaypoints.Count > 0 && other.transform == jokerWaypoints[currentJokerIndex])
        {
            currentJokerIndex++;

            if (currentJokerIndex >= jokerWaypoints.Count)
            {
                isInJokerLap = false;
                hasUsedJokerLap = true;
                currentJokerIndex = 0;
                currentNormalIndex = 0;
                completedLaps++;

                lastLapTime = currentLapTime;
                if (lastLapTime < bestLapTime)
                    bestLapTime = lastLapTime;

                currentLapTime = 0f;
                Debug.Log($"🃏 {gameObject.name} completed Joker Lap");
            }
        }
        else if (!hasUsedJokerLap && other.transform == jokerWaypoints[0])
        {
            isInJokerLap = true;
            currentJokerIndex = 0;
            Debug.Log($"🛣️ {gameObject.name} ENTERED Joker Lap");
        }
        // Normal waypoint logic
        else if (!isInJokerLap && normalWaypoints.Count > 0 && other.transform == normalWaypoints[currentNormalIndex])
        {
            // First trigger starts the timer but does not count as lap
            if (!hasStarted && currentNormalIndex == 0)
            {
                hasStarted = true;
                Debug.Log($"⏱️ {gameObject.name} started lap timer");
                return;
            }

            currentNormalIndex++;

            if (currentNormalIndex >= normalWaypoints.Count)
            {
                currentNormalIndex = 0;
                completedLaps++;

                lastLapTime = currentLapTime;
                if (lastLapTime < bestLapTime)
                    bestLapTime = lastLapTime;

                currentLapTime = 0f;

                Debug.Log($"🏁 {gameObject.name} completed lap {completedLaps} in {FormatTime(lastLapTime)}");
            }
        }
    }

    void UpdateUI()
    {
        if (lapText != null)
            lapText.text = $"Lap: {completedLaps + 1}";

        if (lapTimeText != null)
            lapTimeText.text = $"LAPT: {FormatTime(currentLapTime)}";

        if (lastLapTimeText != null)
            lastLapTimeText.text = $"Last: {FormatTime(lastLapTime)}";

        if (bestLapTimeText != null && bestLapTime < Mathf.Infinity)
            bestLapTimeText.text = $"Best: {FormatTime(bestLapTime)}";
    }

    public float GetTotalTime()
    {
        return totalTime;
    }

    public bool HasUsedJoker()
    {
        return hasUsedJokerLap;
    }

    public void ResetLapData()
    {
        totalTime = 0f;
        completedLaps = 0;
        currentNormalIndex = 0;
        currentJokerIndex = 0;
        hasUsedJokerLap = false;
        isInJokerLap = false;
        hasStarted = false;

        currentLapTime = 0f;
        lastLapTime = 0f;
        bestLapTime = Mathf.Infinity;
    }

    string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int milliseconds = Mathf.FloorToInt((time * 1000f) % 1000f);
        return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }
}
