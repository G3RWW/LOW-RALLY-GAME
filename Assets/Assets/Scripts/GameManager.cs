using System.Collections.Generic;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    [Header("Car Setup")]
    public GameObject[] carPrefabs;
    public Transform[] spawnPoints;
    public bool[] isAIControlled;
    public AIBehaviorType[] aiBehaviors; // Array of AI behavior types

    [Header("Car Identification")]
    public string carTag = "car";
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
    public bool showRPMBar = true;
    public bool showRPMNeedle = false;

    [Header("RPM UI Objects")]
    public GameObject rpmBarObject;
    public GameObject analogSpeedMeterObject;


    [Header("Lap UI")]
    public UnityEngine.UI.Text lapText;
    public UnityEngine.UI.Text lapTimeText;
    public UnityEngine.UI.Text lastLapTimeText;
    public UnityEngine.UI.Text bestLapTimeText;

    void Start()
    {
        ApplyRPMUIVisibility();
        SpawnCars(); // Keep your original flow
        
    }

    void SpawnCars()
    {
        for (int i = 0; i < carPrefabs.Length && i < spawnPoints.Length; i++)
        {
            GameObject car = Instantiate(carPrefabs[i], spawnPoints[i].position, spawnPoints[i].rotation);
            car.tag = carTag;
            car.layer = LayerMask.NameToLayer(carLayerName);
            car.name = $"Car_{i + 1}";
            spawnedCars.Add(car);

            CarController carController = car.GetComponent<CarController>();
            AICarController aiController = car.GetComponent<AICarController>();
            LapTimer lapTimer = car.GetComponent<LapTimer>();

            bool isAI = isAIControlled.Length > i && isAIControlled[i];

            if (carController != null)
                carController.isAIControlled = isAI;

            if (aiController != null)
            {
                aiController.enabled = isAI;

                if (isAI)
                {
                    aiController.wayPointContainer = wayPointContainer;
                    aiController.jokerLapWaypointContainer = jokerLapWaypointContainer;
                    aiController.jokerLapExitPoint = jokerLapExitPoint;

                    // Create parent GameObjects for organization
                    GameObject normalParent = new GameObject($"{car.name}_Waypoints");
                    GameObject jokerParent = new GameObject($"{car.name}_JokerWaypoints");

                    // ✅ Set AI's carLayer directly from GameManager layer setting
                    aiController.carLayer = LayerMask.GetMask(carLayerName);
                    aiController.obstacleLayers = LayerMask.GetMask(obstacleLayerName);
                    
                    // Deep copy normal waypoints
                    aiController.waypoints = new List<Transform>(wayPointContainer.waypoints.Count);
                    foreach (Transform wp in wayPointContainer.waypoints)
                    {
                        GameObject clone = new GameObject($"{car.name}_WP_{wp.name}");
                        clone.transform.position = wp.position;
                        clone.transform.rotation = wp.rotation;
                        clone.transform.parent = normalParent.transform;
                        aiController.waypoints.Add(clone.transform);
                    }

                    // Deep copy Joker Lap waypoints
                    aiController.jokerWaypoints = new List<Transform>(jokerLapWaypointContainer.waypoints.Count);
                    foreach (Transform jwp in jokerLapWaypointContainer.waypoints)
                    {
                        GameObject clone = new GameObject($"{car.name}_JWP_{jwp.name}");
                        clone.transform.position = jwp.position;
                        clone.transform.rotation = jwp.rotation;
                        clone.transform.parent = jokerParent.transform;
                        aiController.jokerWaypoints.Add(clone.transform);
                    }


                    // ✅ Assign behavior type
                    if (aiBehaviors.Length > i)
                        aiController.aiBehavior = aiBehaviors[i];
                    else
                        aiController.aiBehavior = AIBehaviorType.Careful;

                    Debug.Log($"✅ {car.name} AI Waypoints Assigned → Normal: {aiController.waypoints.Count}, Joker: {aiController.jokerWaypoints.Count}");
                    Debug.Log($"🤖 {car.name} AI Behavior: {aiController.aiBehavior}");
                }
            }

            if (lapTimer != null)
            {
                lapTimer.enabled = true;

                lapTimer.normalWaypoints = new List<Transform>(lapTrackingContainer.waypoints);
                lapTimer.jokerWaypoints = new List<Transform>(jokerLapTrackingContainer.waypoints);
                lapTimer.jokerLapExitPoint = jokerLapExitPoint;

                carLapTimers[car] = lapTimer;

                Debug.Log($"📏 {car.name} LapTimer assigned → Track: {lapTimer.normalWaypoints.Count}, Joker Track: {lapTimer.jokerWaypoints.Count}");
            }
        }

        // ✅ Detect the first non-AI car and assign camera/UI
        for (int j = 0; j < spawnedCars.Count; j++)
        {
            if (j >= isAIControlled.Length || !isAIControlled[j])
            {
                GameObject playerCar = spawnedCars[j];
                CarController carController = playerCar.GetComponent<CarController>();
                LapTimer lapTimer = playerCar.GetComponent<LapTimer>();

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

                    // 🔁 Assign RPMBarController if it exists
                    RPMBarController rpmController = FindFirstObjectByType<RPMBarController>();
                    if (rpmController != null)
                    {
                        rpmController.rpmBar = rpmBar; // ← Assign fill bar
                        rpmController.rpmNeedle = rpmNeedle; // ← Assign needle
                        rpmController.Initialize(carController); // ← Initialize with car reference
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

                break;
            }
        }
    }

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

    void ApplyRPMUIVisibility()
    {
        if (rpmBarObject != null)
            rpmBarObject.SetActive(showRPMBar);

        if (analogSpeedMeterObject != null)
            analogSpeedMeterObject.SetActive(showRPMNeedle);
    }
}
