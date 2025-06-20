using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Car Setup")]
    public GameObject[] carPrefabs;
    public Transform[] spawnPoints;
    public bool[] isAIControlled;
    public AIBehaviorType[] aiBehaviors; // Array of AI behavior types
    private List<GameObject> loadedPrefabs;
    private GameObject playerCarPrefab;

    [Header("Car Identification")]
    public string carTag = "car";
    public string PlayercarTag = "playerCar";
    public string carLayerName = "AICar";
    public string obstacleLayerName = "Obstacle";
    private List<GameObject> spawnedCars = new List<GameObject>();
    private Dictionary<GameObject, LapTimer> carLapTimers = new Dictionary<GameObject, LapTimer>();

    [Header("Waypoints (AI Navigation)")]
    public WayPointContainerScript wayPointContainer;
    public WayPointContainerScript jokerLapWaypointContainer;
    public Transform jokerLapExitPoint;

    [Header("Waypoints (Lap Tracking)")]
    public WayPointContainerScript lapTrackingContainer;
    public WayPointContainerScript jokerLapTrackingContainer;

    [Header("Lap Management")]
    public int totalLaps = 3;
    public static bool raceStarted = false;


    [Header("Camera Setup")]
    public ArtOfRallyCamera rallyCamera;
    public int playerCarIndex = 0;

    [Header("UI Setup")]
    public UnityEngine.UI.Text speedText;
    public UnityEngine.UI.Text rpmText;
    public UnityEngine.UI.Text gearText;
    public UnityEngine.UI.Image rpmBar;
    public RectTransform rpmNeedle;

    [Header("RPM UI Toggles")]
    public bool showRPMBar;
    public bool showRPMNeedle;

    [Header("RPM UI Objects")]
    public GameObject rpmBarObject;
    public GameObject analogSpeedMeterObject;
    [Header("Race Start Timer")]
    public float countdownTime = 3f;
    public UnityEngine.UI.Text countdownText;

    [Header("Car Names")]
    public List<string> possibleCarNames = new List<string>
    {
        "ShadowFox", "WildViper", "BlazeRunner", "SteelStorm", "NightWolf",
        "RallyKing", "DustHunter", "DriftQueen", "RedComet", "SkyBreaker",
        "Takumi", "Danielius.Regesas", "Kanya West", "TUNG TUNG TUNG TUNG Sahur", "Razor",
    };
    private List<string> usedCarNames = new List<string>();


    [Header("Lap UI")]
    public UnityEngine.UI.Text lapText;
    public UnityEngine.UI.Text lapTimeText;
    public UnityEngine.UI.Text lastLapTimeText;
    public UnityEngine.UI.Text bestLapTimeText;
//===============================================================================================================================
// Initialization
    void Start()
    {
        GameObject[] allPrefabs = Resources.LoadAll<GameObject>("carPrefabs/new");
        loadedPrefabs = new List<GameObject>(allPrefabs);

        // Find and assign player car
        playerCarPrefab = loadedPrefabs.Find(p => p.name == PlayerSelection.selectedCarName);

        if (playerCarPrefab == null)
        {
            Debug.LogError($"❌ Could not find selected car prefab: {PlayerSelection.selectedCarName}");
            return;

        }

        // Filter out AI-incompatible car(s)
        loadedPrefabs.RemoveAll(p => p.name == "vw_polo_wrc_2013_(ver_1)"); // <- 👈 block AI from using this one

        showRPMBar = RPMSettingsManager.Instance.ShowRPMBar;
        showRPMNeedle = RPMSettingsManager.Instance.ShowRPMNeedle;
        ApplyRPMUIVisibility();


        SpawnCars(); // Keep your original flow
        StartCoroutine(RaceStartCountdown());
    }
    private IEnumerator RaceStartCountdown()
    {
        countdownText.gameObject.SetActive(true);
        float timeLeft = countdownTime;

        while (timeLeft > 0)
        {
            countdownText.text = Mathf.Ceil(timeLeft).ToString("F0");
            yield return new WaitForSeconds(1f);
            timeLeft--;
        }

        countdownText.text = "GO!";
        yield return new WaitForSeconds(1f);
        countdownText.gameObject.SetActive(false);

        // ✅ UNLOCK input for all cars
        GameManager.raceStarted = true;

        foreach (var car in spawnedCars)
        {
            var ai = car.GetComponent<AICarController>();
            if (ai != null)
                ai.ForceStartDriving();
        }

    }
//===============================================================================================================================
// Spawning Cars
    void SpawnCars()
    {
        // Step 1: Prepare list of spawn indices
        List<int> spawnIndices = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++)
            spawnIndices.Add(i);

        // Step 2: Pick random index for player car
        playerCarIndex = Random.Range(0, spawnIndices.Count);
        int playerSpawnIndex = spawnIndices[playerCarIndex];

        // Step 3: Instantiate cars at each spawn point
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int spawnIndex = spawnIndices[i];
            GameObject prefabToUse;

            if (i == playerCarIndex)
            {
                prefabToUse = playerCarPrefab;
            }
            else
            {
                // Get AI prefab (skip player car prefab)
                List<GameObject> validAIPrefabs = loadedPrefabs.FindAll(p => p != playerCarPrefab);
                if (validAIPrefabs.Count == 0)
                {
                    Debug.LogError("❌ No valid AI car prefabs available.");
                    continue;
                }

                int randomIndex = Random.Range(0, validAIPrefabs.Count);
                prefabToUse = validAIPrefabs[randomIndex];
            }

            GameObject car = Instantiate(prefabToUse, spawnPoints[spawnIndex].position, spawnPoints[spawnIndex].rotation);

            if (i == playerCarIndex)
            {
                car.name = "Player";
            }
            else
            {
                car.name = GetUniqueRandomName();
            }

            bool isAI = (i != playerCarIndex);
            SetupCar(car, isAI, i);
        }

        AssignPlayerCameraAndUI();
    }
    List<Transform> CloneWaypoints(List<Transform> source, Transform parent, string prefix)
    {
        var list = new List<Transform>();
        foreach (Transform wp in source)
        {
            GameObject clone = new($"{prefix}_{wp.name}");
            clone.transform.position = wp.position;
            clone.transform.rotation = wp.rotation;
            clone.transform.parent = parent;

            // Copy WaypointSideBias if it exists
            WaypointSideBias originalBias = wp.GetComponent<WaypointSideBias>();
            if (originalBias != null)
            {
                WaypointSideBias newBias = clone.AddComponent<WaypointSideBias>();
                newBias.preferredSide = originalBias.preferredSide;
            }

            list.Add(clone.transform);
        }
        return list;
    }
    void SetupCar(GameObject car, bool isAI, int index)
    {
        car.tag = isAI ? carTag : PlayercarTag;
        car.layer = LayerMask.NameToLayer(carLayerName);
        spawnedCars.Add(car);

        var carController = car.GetComponent<CarController>();
        var aiController = car.GetComponent<AICarController>();
        var lapTimer = car.GetComponent<LapTimer>();

        if (carController != null) carController.isAIControlled = isAI;

        // 👇 Apply gearbox setting to player car
        if (!isAI)
        {
            carController.isAutomatic = !GearboxSettingsManager.Instance.UseManual;
            Debug.Log($"⚙️ Gearbox mode applied on spawn: {(GearboxSettingsManager.Instance.UseManual ? "Manual" : "Automatic")}");
        }


        if (aiController != null)
        {
            if (isAI)
            {
                aiController.enabled = true;
                aiController.aiBehavior = aiBehaviors.Length > index ? aiBehaviors[index] : AIBehaviorType.Careful;
                aiController.carLayer = LayerMask.GetMask(carLayerName);
                aiController.obstacleLayers = LayerMask.GetMask(obstacleLayerName);
                aiController.wayPointContainer = wayPointContainer;
                aiController.jokerLapWaypointContainer = jokerLapWaypointContainer;
                aiController.jokerLapExitPoint = jokerLapExitPoint;

                GameObject normalParent = new($"{car.name}_Waypoints");
                aiController.waypoints = CloneWaypoints(wayPointContainer.waypoints, normalParent.transform, car.name + "_WP");

                GameObject jokerParent = new($"{car.name}_JokerWaypoints");
                aiController.jokerWaypoints = CloneWaypoints(jokerLapWaypointContainer.waypoints, jokerParent.transform, car.name + "_JWP");
            }
            else
            {
                aiController.enabled = false; // ✅ Disable AI on player car
            }
        }


        if (lapTimer != null)
        {
            lapTimer.enabled = true;
            lapTimer.normalWaypoints = new List<Transform>(lapTrackingContainer.waypoints);
            lapTimer.jokerWaypoints = new List<Transform>(jokerLapTrackingContainer.waypoints);
            lapTimer.jokerLapExitPoint = jokerLapExitPoint;
            carLapTimers[car] = lapTimer;
        }
    }
    private string GetUniqueRandomName()
    {
        if (possibleCarNames.Count == 0)
            return "UnknownCar";

        List<string> available = new List<string>(possibleCarNames);
        available.RemoveAll(name => usedCarNames.Contains(name));

        if (available.Count == 0)
            return "Car_" + Random.Range(1000, 9999); // fallback

        string picked = available[Random.Range(0, available.Count)];
        usedCarNames.Add(picked);
        return picked;
    }
//===============================================================================================================================
// Lap Management
    public void OnCarLapCompleted(GameObject car)
    {
        if (!carLapTimers.ContainsKey(car)) return;

        LapTimer timer = carLapTimers[car];
        if (timer.completedLaps >= totalLaps)
        {
            Debug.Log($"🏁 {car.name} finished the race! Total Time: {timer.GetTotalTime():F2}s");
        }
    }
    public int GetCarLap(GameObject car)
    {
        return carLapTimers.TryGetValue(car, out LapTimer timer) ? timer.completedLaps : 0;
    }
    public float GetCarTime(GameObject car)
    {
        return carLapTimers.TryGetValue(car, out LapTimer timer)
            ? timer.GetTotalTime()
            : 9999f;
    }
    public int GetPosition(GameObject car)
    {
        int position = 1;
        int carLap = GetCarLap(car);
        float carTime = GetCarTime(car);

        foreach (var other in spawnedCars)
        {
            if (other == car) continue;

            int otherLap = GetCarLap(other);
            float otherTime = GetCarTime(other);

            if (otherLap > carLap || (otherLap == carLap && otherTime < carTime))
            {
                position++;
            }
        }

        return position;
    }
//===============================================================================================================================
// UI Management
    void ApplyRPMUIVisibility()
    {
        if (rpmBarObject != null)
            rpmBarObject.SetActive(showRPMBar);

        if (analogSpeedMeterObject != null)
            analogSpeedMeterObject.SetActive(showRPMNeedle);
    }
    void AssignPlayerCameraAndUI()
    {
        if (playerCarIndex >= spawnedCars.Count)
        {
            Debug.LogError("⚠️ Player car index is out of range!");
            return;
        }

        GameObject playerCar = spawnedCars[playerCarIndex];

        var carController = playerCar.GetComponent<CarController>();
        var lapTimer = playerCar.GetComponent<LapTimer>();


        if (rallyCamera != null)
        {
            rallyCamera.target = playerCar.transform;
            Debug.Log($"🎥 Camera assigned to: {playerCar.name}");
        }

        if (carController != null)
        {
            carController.speedText = speedText;
            carController.rpmText = rpmText;
            carController.gearText = gearText;
            carController.rpmBar = rpmBar;
            Debug.Log($"🧾 UI assigned to: {playerCar.name}");

            var rpmController = FindFirstObjectByType<RPMBarController>();
            if (rpmController != null)
            {
                rpmController.rpmBar = rpmBar;
                rpmController.rpmNeedle = rpmNeedle;
                rpmController.Initialize(carController);
            }
            else
            {
                Debug.LogWarning("⚠️ RPMBarController not found in scene!");
            }
        }

        if (lapTimer != null)
        {
            lapTimer.lapText = lapText;
            lapTimer.lapTimeText = lapTimeText;
            lapTimer.lastLapTimeText = lastLapTimeText;
            lapTimer.bestLapTimeText = bestLapTimeText;
            Debug.Log($"🕒 Lap UI assigned to: {playerCar.name}");
        }
    }
    public void SetShowRPMBar(bool value)
    {
        showRPMBar = value;
        if (rpmBarObject != null) rpmBarObject.SetActive(value);
    }
    public void SetShowRPMNeedle(bool value)
    {
        showRPMNeedle = value;
        if (analogSpeedMeterObject != null) analogSpeedMeterObject.SetActive(value);
    }
}
