using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

    public enum AIBehaviorType
    {
        Careful,
        Aggressive,
        //Slow,     // 🚗 Noob-like behavior
        Fast      // 🏎️ Pro-like behavior
    }

    public enum AIState
    {
        Idle,
        Driving,
        Overtaking,
        Recovery
    }

public class AICarController : MonoBehaviour
{
    [Header("Link to Car Controller")]
    private CarController carController;
    private CarData carData;
    //=================================================================================================================================================================================================================================================
    [Header("Waypoint Navigation")]
    public WayPointContainerScript wayPointContainer;
    public List<Transform> waypoints = new List<Transform>();
    public int currentWaypointIndex = 0;
    public float waypointRange = 0.6f; // AI must be within 0.5m of the waypoint before switching
    private HashSet<Transform> visitedWaypoints = new HashSet<Transform>();
    //=================================================================================================================================================================================================================================================
    [Header("Navigation Target")]
    public NavMeshAgent agentFollower; // Reference to the child NavMeshAgent
    //=================================================================================================================================================================================================================================================
    [Header("AI Steering & Speed")]
    private float currentGasInput = 0f;
    private float currentBrakeInput = 0f;
    private float currentAngle;
    private float AiGasInput;
    public bool IsInsideBrakeZone = false;
    public float AiMaxAngle = 60f;
    public float brakingLookaheadDistance = 15f;
    private float smoothSteering = 0f;
    private float targetSpeed = 25f; // AI adapts speed based on upcoming turns
    private float minTurnSpeed = 10f; // Minimum speed in sharp turns
    private float maxStraightSpeed = 30f; // Maximum speed on straight roads
    //=================================================================================================================================================================================================================================================
    [Header("Obstacle Avoidance")]
    public float baseObstacleRange = 20f;
    public LayerMask obstacleLayers;
    public LayerMask carLayer;
    //=================================================================================================================================================================================================================================================
    [Header("Overtaking")]
    private Vector3 overtakeTargetOffset = Vector3.zero;
    private Vector3 overtakeTargetPosition = Vector3.zero;
    private Transform overtakeTargetCar = null;
    private BoxCollider carCollider;
    private float carWidth = 2f; // fallback
    //=================================================================================================================================================================================================================================================
    [Header("Surrounding Obstacle Detection")]
    public float detectionRadius = 4f;
    public float minAvoidanceSteer = 0.3f;
    public AIBehaviorType aiBehavior = AIBehaviorType.Careful;
    public float overtakeBoost = 1.3f; // How much faster the car tries to go during overtake
    public float sideOffset = 2f;      // How far it tries to move left/right to overtake
    //=================================================================================================================================================================================================================================================
    [Header("Joker Lap Settings")]
    public Transform jokerLapExitPoint; // Exit point for the Joker Lap
    private bool isTakingJokerLap = false;
    private bool hasTakenJokerLap = false; // AI should only take Joker Lap once
    private bool shouldTakeJokerLap = false; // Decides if AI will take it
    public List<Transform> jokerLapEntries = new List<Transform>(); // Joker Lap entry points
    public WayPointContainerScript jokerLapWaypointContainer; // Joker Lap waypoints
    public List<Transform> jokerWaypoints = new List<Transform>();
    private int jokerWaypointIndex = 0;
    private enum JokerLapStage
    {
        None,
        GoingToEntry,
        TakingJokerLap
    }
    private JokerLapStage jokerLapStage = JokerLapStage.None;
    private Transform currentJokerLapEntry;
    [Header("Joker Lap Settings")]
    public Transform jokerLapExitRejoinPoint; // <- drag Waypoint (40) here in Inspector
    [Header("Debugging")]
    private int lapCount = 0;
    private float lapTimer = 0f;
    private void Log(string message)
    {
        Debug.Log($"[{gameObject.name}] {message}");
    }
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[{gameObject.name}] {message}");
    }
    private void LogError(string message)
    {
        Debug.LogError($"[{gameObject.name}] {message}");
    }
    [Header("Recovery Mode")]
    private bool isInRecoveryMode = false;
    private float recoveryTimer = 0f;
    private float maxRecoveryTime = 5f;
    private Vector3 recoveryTarget;
    private int lastValidWaypointIndex = 0;
    //=================================================================================================================================================================================================================================================
    [Header("Debugging Toggles")]
    public bool debugSteeringLine = false;
    public bool debugClampedTarget = false;
    public bool debugWaypointPath = false;
    public bool debugNavMeshEdgeClamp = false;
    public bool debugTrajectoryPrediction = false;
    public bool debugJokerLapPath = false;
    public bool debugNavMeshRecovery = false;
    public bool debugSpeedAdjustment = false;
    public bool debugObstacleDetection = false;
    [SerializeField] private bool debugDrawStraightLine = false;
    [SerializeField] private bool debugDrawBezierCurve = false;
    private AIState currentState = AIState.Idle;
    public float currentSpeedKmh;
    //=================================================================================================================================================================================================================================================
    // Initialization
    // Called when the script instance is being loaded
    void Start()
    {
        carController = GetComponent<CarController>();
        carCollider = GetComponent<BoxCollider>();

        if (carController != null)
        {
            carData = carController.carData;
        }
        else
        {
            Debug.LogWarning("CarControler not found on AI car.");
        }

        if (carCollider != null)
        {
            carWidth = carCollider.size.x * transform.localScale.x;
        }

        // Use pre-assigned waypoints (from GameManager), fallback to container only if needed
        if ((waypoints == null || waypoints.Count == 0) && wayPointContainer != null)
        {
            waypoints = wayPointContainer.waypoints.FindAll(wp => wp.CompareTag("Waypoint"));
            LogWarning($"⚠️ {name} fallback: waypoints pulled from WayPointContainer.");
        }

        if ((jokerWaypoints == null || jokerWaypoints.Count == 0) && jokerLapWaypointContainer != null)
        {
            jokerWaypoints = jokerLapWaypointContainer.waypoints;
            LogWarning($"⚠️ {name} fallback: joker waypoints pulled from JokerLapWaypointContainer.");
        }

        if (waypoints == null || waypoints.Count == 0)
        {
            LogError($"❌ {name} has no waypoints assigned!");
            return;
        }

        carController.currentGear = 2;
        currentWaypointIndex = 0;

        agentFollower.updatePosition = false;
        agentFollower.updateRotation = false;
        agentFollower.isStopped = false;
        agentFollower.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agentFollower.autoBraking = false;

        // Set AI driving behavior
        switch (aiBehavior)
        {
            case AIBehaviorType.Aggressive: // Automobilis 1
                maxStraightSpeed = 80f;
                minTurnSpeed = 24f;
                overtakeBoost = 2.5f;
                AiMaxAngle = 60f;
                break;

            case AIBehaviorType.Careful: // Automobilis 2
                maxStraightSpeed = 50f;
                minTurnSpeed = 10f;
                overtakeBoost = 1.2f;
                AiMaxAngle = 70f;
                break;

            /*
            case AIBehaviorType.Slow:
                maxStraightSpeed = 20f;
                minTurnSpeed = 6f;
                overtakeBoost = 1.1f;
                AiMaxAngle = 35f;
                break;
            */

            case AIBehaviorType.Fast:
                maxStraightSpeed = 100f;
                minTurnSpeed = 30f;
                overtakeBoost = 4.0f;
                AiMaxAngle = 60f;
                break;
            
            
        }

        Log($"🚗 {name} AI initialized with {waypoints.Count} waypoints, behavior: {aiBehavior}");
    }
    void Update()
    {
        currentSpeedKmh = carController != null ? carController._rigidbody.linearVelocity.magnitude * 3.6f : 0f;

        lapTimer += Time.deltaTime;

        switch (currentState)
        {
            case AIState.Idle:
                HandleIdleState();
                break;

            case AIState.Driving:
                HandleDrivingState();
                break;

            case AIState.Overtaking:
                HandleOvertakingState();
                break;

            case AIState.Recovery:
                HandleRecoveryState();
                break;
        }

        //LateUpdateNavAgentSync();

        Log($"AI State: {currentState}");

    }
    void FixedUpdate()
    {
        if (!IsOnNavMesh(transform.position))
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                transform.position = hit.position; // hard clamp
                LogWarning("🚧 Position clamped to NavMesh!");
            }
        }
    }
    void LateUpdate()
    {
        if (agentFollower)
        {
            agentFollower.nextPosition = transform.position;
            agentFollower.velocity = Vector3.zero;
        }
    }
    //=================================================================================================================================================================================================================================================
    // State Handlers
    // Handles the AI's idle state, which can be expanded later
    void HandleIdleState()
    {
        /*
         if (IsOnNavMesh(transform.position))
        {
            currentState = AIState.Driving;
        }
        
        */
       
    }
    void HandleDrivingState()
    {
        // Ensure waypoints are available, else exit
        if (!CheckWaypointAvailability()) return;

        // Check for obstacles or cars ahead; switch to overtaking if found
        if (DetectObstaclesAhead() || DetectCarsAhead())
        {
            currentState = AIState.Overtaking;
            return;
        }

        // If off the NavMesh, switch to recovery mode
        if (!IsOnNavMesh(transform.position))
        {
            currentState = AIState.Recovery;
            return;
        }

        // Get the next waypoint to aim for
        Transform targetWaypoint = GetCurrentTargetWaypoint();
        if (!targetWaypoint) return;

        // Update AI path using NavMeshAgent towards the waypoint
        UpdateNavMeshAgent(targetWaypoint);
        // Calculate and apply steering towards the target
        ApplySteering();
        // Check if the AI is too close to the NavMesh edge
        if (!IsPositionSafeOnNavMesh(transform.position, carWidth * 0.5f + 0.2f))
        {
            LogWarning("⚠️ Car drifted too close to NavMesh edge. Entering recovery.");
            currentState = AIState.Recovery;
            return;
        }

        // Adjust speed if a turn is approaching
        AdjustSpeedBeforeTurn();
        // Apply throttle and brake inputs based on target speed
        ApplyThrottleAndBrakes();
        // Check if close enough to current waypoint to advance to next
        CheckProximityToWaypoint();
        // Optional: draw debug visuals for AI behavior
        DrawDebugLines();
    }
    void HandleOvertakingState()
    {
        float speed = carController._rigidbody.linearVelocity.magnitude;
        float detectDistance = baseObstacleRange + speed * 0.6f; // Dynamic detection range based on speed
        Vector3 origin = transform.position + Vector3.up * 0.5f; // Raise raycast origin slightly
        // Use turning direction if turning, else just forward
        Vector3 detectionDirection = smoothSteering != 0f 
            ? Quaternion.Euler(0f, smoothSteering * AiMaxAngle, 0f) * transform.forward 
            : transform.forward;



        if (overtakeTargetCar == null)
        {
            // Look for a car ahead to overtake
            if (Physics.Raycast(origin, detectionDirection, out RaycastHit hit, detectDistance, carLayer))
            {
                overtakeTargetCar = hit.transform;
            }
            else
            {
                currentState = AIState.Driving; // Nothing ahead, switch to normal driving
                return;
            }
        }

        if (overtakeTargetCar != null)
        {
            // Check if car is now behind or too far away
            Vector3 toCar = overtakeTargetCar.position - transform.position;
            float dotForward = Vector3.Dot(transform.forward, toCar.normalized);

            if (dotForward < 0f || Vector3.Distance(transform.position, overtakeTargetCar.position) > detectDistance * 1.2f)
            {
                // Clear target and exit overtaking
                overtakeTargetCar = null;
                overtakeTargetOffset = Vector3.zero;
                currentState = AIState.Driving;
                return;
            }

            // Evaluate which side to overtake (re-evaluated every frame)
            overtakeTargetOffset = EvaluateOvertakeSide(detectionDirection);

            if (overtakeTargetOffset == Vector3.zero)
            {
                // Both sides blocked, stop and wait
                carController.SetAISteering(0f);
                carController.SetAIGas(0f);
                carController.SetAIBrake(true);

                if (debugObstacleDetection)
                    Debug.DrawRay(transform.position, transform.forward * 5f, Color.red);

                return;
            }

            // Calculate target position for overtaking
            Vector3 tentativeTarget = transform.position + detectionDirection * 10f + overtakeTargetOffset;
            overtakeTargetPosition = GetNavMeshEdgeClamp(tentativeTarget, 1f);

            // Predict if the path is safe on NavMesh
            if (PredictFutureTrajectoryOffNavMesh(transform.position, (overtakeTargetPosition - transform.position).normalized, speed, smoothSteering))
            {
                // Path unsafe, brake and cancel overtaking
                carController.SetAISteering(0f);
                carController.SetAIGas(0f);
                carController.SetAIBrake(true);
                LogWarning($"❌ Overtake path unsafe - braking");
                return;
            }

            // Calculate steering towards overtake target
            Vector3 steerDir = (overtakeTargetPosition - transform.position).normalized;
            float steerAngle = Vector3.SignedAngle(transform.forward, steerDir, Vector3.up);
            float steerAmount = Mathf.Clamp(steerAngle / AiMaxAngle, -1f, 1f);

            // Smooth steering and gas input
            smoothSteering = Mathf.Lerp(smoothSteering, steerAmount, Time.deltaTime * 3f);
            float tightTurnSpeed = Mathf.Max(minTurnSpeed * 0.8f, 8f);
            targetSpeed = Mathf.Lerp(tightTurnSpeed, maxStraightSpeed * overtakeBoost, 0.7f);
            // Apply gas input based on target speed
            currentGasInput = Mathf.Lerp(currentGasInput, 0.7f, Time.deltaTime * 1.5f); // Slower accel


            // Apply AI controls
            carController.SetAISteering(smoothSteering);
            carController.SetAIGas(currentGasInput);
            carController.SetAIBrake(false);

            if (debugObstacleDetection)
                Debug.DrawLine(transform.position, overtakeTargetPosition, Color.cyan);
        }
    }
    void HandleRecoveryState()
    {
        // If back on NavMesh, switch to driving
        if (IsOnNavMesh(transform.position))
        {
            currentState = AIState.Driving;
            return;
        }

        // Keep trying to move towards the target waypoint using NavMeshAgent
        UpdateNavMeshAgent(GetCurrentTargetWaypoint());

        // Apply full brake, no gas while recovering
        carController.SetAIGas(0f);
        carController.SetAIBrake(true);

        // If stuck too long, force teleport to recovery target
        if (recoveryTimer > maxRecoveryTime)
        {
            // Force respawn at last valid waypoint
            List<Transform> currentPath = isTakingJokerLap ? jokerWaypoints : waypoints;
            int index = Mathf.Clamp(lastValidWaypointIndex, 0, currentPath.Count - 2); // prevent out of bounds
            Transform lastWaypoint = currentPath[index];
            Transform nextWaypoint = currentPath[index + 1];

            Vector3 position = lastWaypoint.position;
            Vector3 forward = (nextWaypoint.position - lastWaypoint.position).normalized;


            transform.position = position;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            carController._rigidbody.linearVelocity = Vector3.zero;
            carController._rigidbody.angularVelocity = Vector3.zero;

            LogWarning($"🚨 Teleported to WP {index} for recovery, facing WP {index + 1}");

            isInRecoveryMode = false;
            currentState = AIState.Driving;
        }

    }
    public void ForceStartDriving()
    {
        if (GameManager.raceStarted)
            currentState = AIState.Driving;
    }
    //=================================================================================================================================================================================================================================================
    // AI Behavior Logic
    // Calculates and applies steering towards the next waypoint using Bezier curves
    void ApplySteering()
    {
        List<Transform> path = isTakingJokerLap ? jokerWaypoints : waypoints;
        int i = isTakingJokerLap ? jokerWaypointIndex : currentWaypointIndex;

        if (path.Count < 4) return;

        int count = path.Count;

        Vector3 p0 = GetNavMeshEdgeClamp(path[(i - 1 + count) % count].position, carWidth * 0.5f + 0.3f);
        Vector3 p1 = GetNavMeshEdgeClamp(path[i % count].position, carWidth * 0.5f + 0.3f);
        Vector3 p2 = GetNavMeshEdgeClamp(path[(i + 1) % count].position, carWidth * 0.5f + 0.3f);
        Vector3 p3 = GetNavMeshEdgeClamp(path[(i + 2) % count].position, carWidth * 0.5f + 0.3f);

        float speed = carController._rigidbody.linearVelocity.magnitude;
        float lookaheadDistance = Mathf.Clamp(speed * 0.8f, 3f, 20f);

        Vector3 curveTarget = GetClampedCurvePoint(p0, p1, p2, p3, lookaheadDistance);

        float safeThreshold = carWidth * 0.5f + 0.3f;
        if (!IsPositionSafeOnNavMesh(p0, safeThreshold) ||
            !IsPositionSafeOnNavMesh(p1, safeThreshold) ||
            !IsPositionSafeOnNavMesh(p2, safeThreshold) ||
            !IsPositionSafeOnNavMesh(p3, safeThreshold))
        {
            LogWarning("🧯 Unsafe Catmull-Rom control points! Emergency straight aim.");
            Vector3 emergencyTarget = GetNavMeshEdgeClamp(transform.position + transform.forward * 5f, safeThreshold);
            Vector3 steerDir = (emergencyTarget - transform.position).normalized;
            currentAngle = Vector3.SignedAngle(transform.forward, steerDir, Vector3.up);
        }
        else
        {
            Vector3 steerDirection = (curveTarget - transform.position).normalized;
            currentAngle = Vector3.SignedAngle(transform.forward, steerDirection, Vector3.up);
        }

        float steerAmount = Mathf.Clamp(currentAngle / AiMaxAngle, -1f, 1f);
        smoothSteering = Mathf.Lerp(smoothSteering, steerAmount, Time.deltaTime * 7f);

        carController.SetAISteering(smoothSteering);

        if (debugDrawBezierCurve)
        {
            Debug.DrawLine(transform.position, curveTarget, Color.green);
        }

        if (debugSteeringLine)
        {
            Vector3 steerVec = Quaternion.Euler(0, smoothSteering * AiMaxAngle, 0) * transform.forward;
            Debug.DrawRay(transform.position, steerVec * 5f, Color.blue);
        }
    }
    void ApplyThrottleAndBrakes()
    {
        float speed = carController._rigidbody.linearVelocity.magnitude;
        float desiredGas = Mathf.Clamp01(targetSpeed / speed);
        float desiredBrake = CalculateBraking();

        // 🔁 Gas transitions slower (smoother acceleration)
        currentGasInput = Mathf.Lerp(currentGasInput, 1f, Time.deltaTime * 2.5f); // max power during overtake

        // ⏩ Brake responds faster (for sharp corners)
        currentBrakeInput = Mathf.Lerp(currentBrakeInput, desiredBrake, Time.deltaTime * 8f);

        // 🚨 Hard brake if urgent
        if (desiredBrake > 0.8f)
            currentBrakeInput = desiredBrake;

        carController.SetAIGas(currentGasInput);
        carController.SetAIBrake(currentBrakeInput > 0.05f);
    }
    void AdjustSpeedBeforeTurn()
    {
        int nextIndex = (currentWaypointIndex + 1) % waypoints.Count;
        Transform nextWaypoint = waypoints[nextIndex];
        if (!nextWaypoint) return;

        Vector3 toNext = nextWaypoint.position - transform.position;
        Vector3 nextDirection = toNext.normalized;
        float nextAngle = Vector3.SignedAngle(transform.forward, nextDirection, Vector3.up);
        float angleSeverity = Mathf.Abs(nextAngle);
        float distanceToNext = toNext.magnitude;

        // Default target speed based on angle
        if (angleSeverity > 60f)
            targetSpeed = minTurnSpeed;      // Very sharp
        else if (angleSeverity > 40f)
            targetSpeed = 15f;               // Medium
        else if (angleSeverity > 20f)
            targetSpeed = 22f;               // Small bend
        else
            targetSpeed = maxStraightSpeed; // Almost straight

        // Predict braking distance
        float brakingDistance = CalculateBrakingDistance(currentSpeedKmh);
        if (distanceToNext < brakingDistance)
        {
            targetSpeed = Mathf.Min(targetSpeed, minTurnSpeed);
            LogWarning($"⛔ Not enough braking room! Reducing speed to {targetSpeed} km/h");
        }

        // Predict curve path danger
        if (WillCurveLeaveNavMesh(currentSpeedKmh / 3.6f))
        {
            targetSpeed = Mathf.Min(targetSpeed, minTurnSpeed);
            LogWarning("🚧 Lowering speed due to predicted unsafe curve path.");
        }

        if (debugSpeedAdjustment)
        {
            Log($"🌀 Adjusting AI Speed: {targetSpeed:F2} for turn angle: {nextAngle:F2}°, distance: {distanceToNext:F1}, brakingDist: {brakingDistance:F1}");
        }
    }
    float CalculateBraking()
    {
        if (isInRecoveryMode) return 0f;

        float speed = carController._rigidbody.linearVelocity.magnitude;
        if (speed < 5f) return 0f;

        float brakeStrength = 0f;

        // Dynamic angle-based braking
        int baseLookahead = 2;
        int speedLookahead = Mathf.FloorToInt(speed / 7f);
        int lookaheadWaypoints = Mathf.Clamp(baseLookahead + speedLookahead, 2, 6);

        float maxSeverity = 0f;
        Vector3 currentDir = transform.forward;

        for (int i = 1; i <= lookaheadWaypoints; i++)
        {
            int index = (currentWaypointIndex + i) % waypoints.Count;
            Transform wp = waypoints[index];
            if (!wp) continue;

            Vector3 dirToNext = (wp.position - transform.position).normalized;
            float angle = Vector3.SignedAngle(currentDir, dirToNext, Vector3.up);
            float severity = Mathf.Abs(angle);
            maxSeverity = Mathf.Max(maxSeverity, severity);

            currentDir = dirToNext;
        }

        if (maxSeverity > 60f)
        {
            float angleFactor = Mathf.Clamp01(maxSeverity / 90f);
            float speedFactor = Mathf.Clamp01((speed - 10f) / 30f);
            brakeStrength = Mathf.Clamp(angleFactor * speedFactor * 1.5f, 0f, 1f);
        }
        else if (maxSeverity > 35f)
        {
            float angleFactor = Mathf.Clamp01(maxSeverity / 90f);
            float speedFactor = Mathf.Clamp01((speed - 10f) / 30f);
            brakeStrength = Mathf.Clamp(angleFactor * speedFactor, 0f, 0.6f);
        }

        // 🧠 Predict if braking distance leads off NavMesh
        float brakingDistance = CalculateBrakingDistance(speed * 3.6f);
        Vector3 predictedStop = transform.position + transform.forward * brakingDistance;
        Vector3 clampedStop = GetNavMeshEdgeClamp(predictedStop, carWidth * 0.5f + 0.2f);
        bool stopSafe = IsPositionSafeOnNavMesh(clampedStop, carWidth * 0.5f + 0.2f);

        if (!stopSafe)
        {
            LogWarning("🛑 Braking point too close to NavMesh edge! Forcing strong brake.");
            brakeStrength = Mathf.Max(brakeStrength, 0.9f);
        }

        // 🧠 Curve prediction
        if (WillCurveLeaveNavMesh(speed))
        {
            LogWarning("🚧 Curve path unsafe! Preemptive braking");
            brakeStrength = Mathf.Max(brakeStrength, 0.75f);
        }

        // 🔁 Trajectory prediction
        if (PredictFutureTrajectoryOffNavMesh(transform.position, transform.forward, speed, smoothSteering))
        {
            LogWarning("⚠️ Predictive: braking for safety!");
            brakeStrength = Mathf.Max(brakeStrength, 0.7f);
        }

        // ⚠️ Static brake zone override
        if (IsInsideBrakeZone)
        {
            brakeStrength = Mathf.Max(brakeStrength, 0.5f);
        }

        return Mathf.Clamp01(brakeStrength);
    }
    Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f*p0 - 5f*p1 + 4f*p2 - p3) * t * t +
            (-p0 + 3f*p1 - 3f*p2 + p3) * t * t * t
        );
    }
    Vector3 GetCatmullRomPointAtDistance(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float distance, int samples = 20)
    {
        float totalLength = 0f;
        Vector3 lastPos = p1;
        float[] distances = new float[samples + 1];
        Vector3[] points = new Vector3[samples + 1];

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 pt = GetCatmullRomPosition(t, p0, p1, p2, p3);
            points[i] = pt;

            if (i > 0)
            {
                float segmentLength = Vector3.Distance(lastPos, pt);
                totalLength += segmentLength;
                distances[i] = totalLength;
            }

            lastPos = pt;
        }

        float targetDistance = Mathf.Clamp(distance, 0, totalLength);

        for (int i = 1; i <= samples; i++)
        {
            if (distances[i] >= targetDistance)
            {
                float tBlend = Mathf.InverseLerp(distances[i - 1], distances[i], targetDistance);
                return Vector3.Lerp(points[i - 1], points[i], tBlend);
            }
        }

        return points[samples]; // fallback
    }
    Vector3 GetClampedCurvePoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float distance)
    {
        int samples = 20;
        float totalLength = 0f;
        Vector3 last = p1;
        Vector3 lastSafe = p1;
        float safetyThreshold = carWidth * 0.5f + 0.3f;

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 pt = GetCatmullRomPosition(t, p0, p1, p2, p3);
            Vector3 clamped = GetNavMeshEdgeClamp(pt, safetyThreshold);

            // Check both the point and the path to it
            bool isSafe = IsPositionSafeOnNavMesh(clamped, safetyThreshold);
            bool isPathSafe = IsPathSegmentSafe(last, clamped, safetyThreshold);

            if (debugTrajectoryPrediction)
            {
                Color color = (isSafe && isPathSafe) ? Color.green : Color.red;
                Debug.DrawRay(clamped + Vector3.up * 0.1f, Vector3.up * 0.4f, color, 1f);
            }

            if (!isSafe || !isPathSafe)
            {
                LogWarning($"🧯 Unsafe curve or segment at t={t:F2}. Using safe fallback.");
                Vector3 forwardSafe = GetNavMeshEdgeClamp(transform.position + transform.forward * distance, safetyThreshold);
                return Vector3.Lerp(lastSafe, forwardSafe, 0.5f);
            }

            float segmentLength = Vector3.Distance(last, clamped);
            totalLength += segmentLength;

            if (totalLength >= distance)
                return clamped;

            last = clamped;
            lastSafe = clamped;
        }

        return lastSafe;
    }
    bool WillCurveLeaveNavMesh(float speed)
{
    List<Transform> path = isTakingJokerLap ? jokerWaypoints : waypoints;
    int i = isTakingJokerLap ? jokerWaypointIndex : currentWaypointIndex;

    if (path.Count < 4)
    return false;

    int count = path.Count;

    Vector3 p0 = path[(i - 1 + count) % count].position;
    Vector3 p1 = path[i % count].position;
    Vector3 p2 = path[(i + 1) % count].position;
    Vector3 p3 = path[(i + 2) % count].position;


    float lookaheadDistance = Mathf.Clamp(speed * 0.8f, 3f, 20f);
    int samples = 10;
    float totalLength = 0f;
    Vector3 last = p1;

    for (int j = 1; j <= samples; j++)
    {
        float t = j / (float)samples;
        Vector3 point = GetCatmullRomPosition(t, p0, p1, p2, p3);
        Vector3 clamped = GetNavMeshEdgeClamp(point, 1.2f);
        totalLength += Vector3.Distance(last, clamped);

        if (!IsPositionSafeOnNavMesh(clamped, 0.8f))
        {
            if (debugTrajectoryPrediction)
                Debug.DrawRay(clamped + Vector3.up * 0.3f, Vector3.up * 0.4f, Color.red, 1f);

            return true;
        }

        last = clamped;
    }

    return false;
}
    float CalculateBrakingDistance(float speed)
    {
        if (carData == null || carController == null)
            return Mathf.Infinity;

        float mass = carController.GetComponent<Rigidbody>().mass;
        float brakeTorque = carData.brakeTorque;

        // Convert speed from km/h to m/s
        float speedMS = speed / 3.6f;

        // Simplified physics: d = (m * v²) / (2 * F)
        float brakingForce = brakeTorque; // assuming 1:1 force output, tweak if needed
        float distance = (mass * speedMS * speedMS) / (2f * brakingForce);

        // Optional: add safety margin
        return distance * 1.1f;
    }
    bool IsPathSegmentSafe(Vector3 start, Vector3 end, float checkRadius)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        direction.Normalize();

        int checks = Mathf.CeilToInt(length / 0.5f); // check every 0.5m
        for (int i = 0; i <= checks; i++)
        {
            Vector3 point = start + direction * (length * (i / (float)checks));
            if (!IsPositionSafeOnNavMesh(point, checkRadius))
                return false;
        }

        return true;
    }
    //=================================================================================================================================================================================================================================================
    // Waypoint Navigation
    // Checks if waypoints are available for navigation 
    bool CheckWaypointAvailability() 
    {
        List<Transform> currentPath = isTakingJokerLap ? jokerWaypoints : waypoints;
        return currentPath.Count > 0;
    }
    Transform GetCurrentTargetWaypoint()
    {
        List<Transform> currentPath = isTakingJokerLap ? jokerWaypoints : waypoints;
        int index = isTakingJokerLap ? jokerWaypointIndex : currentWaypointIndex;
        if (index >= currentPath.Count) return null;

        Transform target = currentPath[index];
        if (!target)
        {
            LogWarning($"❌ Missing waypoint {index}.");
            if (isTakingJokerLap) jokerWaypointIndex++;
            else NextWaypoint();
        }
        return target;
    }
    void CheckProximityToWaypoint()
    {
        var currentPath = isTakingJokerLap ? jokerWaypoints : waypoints;
        int index = isTakingJokerLap ? jokerWaypointIndex : currentWaypointIndex;
        if (index >= currentPath.Count) return;

        var wp = currentPath[index];
        float dist = Vector3.Distance(transform.position, wp.position);

        // If AI is within range or passed the waypoint
        if (dist < waypointRange || Vector3.Dot((wp.position - transform.position).normalized, transform.forward) < 0f)
        {
            if (isTakingJokerLap)
            {
                lastValidWaypointIndex = jokerWaypointIndex;
                NextJokerWaypoint();
            }
            else
            {
                lastValidWaypointIndex = currentWaypointIndex;
                NextWaypoint();
            }
        }
    }
    void NextWaypoint()
    {
        currentWaypointIndex++;

        if (currentWaypointIndex >= waypoints.Count)
        {
            // new lap: skip 0, reset visits
            currentWaypointIndex = 1;
            lapCount++;
            visitedWaypoints.Clear();            // ← clear so you can visit all again
            Log($"🏁 {name} finished lap {lapCount}");

            if (!hasTakenJokerLap)
            {
                float chance = 0.4f;
                shouldTakeJokerLap = Random.value < chance;
                Log( shouldTakeJokerLap 
                    ? $"🃏 {name} will take Joker Lap!" 
                    : $"➡️ {name} will skip Joker Lap." );
            }
        }
    }
    void NextJokerWaypoint()
    {
        jokerWaypointIndex++;
        if (jokerWaypointIndex >= jokerWaypoints.Count)
        {
            Log("🃏 Joker Lap path complete → reconnecting to normal path at specific rejoin point");

            isTakingJokerLap = false;
            hasTakenJokerLap = true;
            jokerLapStage = JokerLapStage.None;
            jokerWaypointIndex = 0;

            if (jokerLapExitRejoinPoint != null && waypoints.Contains(jokerLapExitRejoinPoint))
            {
                currentWaypointIndex = waypoints.IndexOf(jokerLapExitRejoinPoint);
                Log($"🔁 Reconnected to normal path at WP {currentWaypointIndex} ({jokerLapExitRejoinPoint.name})");
            }
            else
            {
                LogWarning("⚠️ JokerLapExitRejoinPoint is not assigned or not found in waypoints list! Falling back.");
                currentWaypointIndex = Mathf.Max(waypoints.Count - 2, 0);
            }

            visitedWaypoints.Clear();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        // Joker Lap trigger
        if (other.CompareTag("JokerLapTrigger"))
        {
            if (shouldTakeJokerLap && !hasTakenJokerLap)
            {
                Log("🃏 Joker Lap triggered. Entering Joker Lap path!");
                isTakingJokerLap = true;
                jokerWaypointIndex = 0;
                jokerLapStage = JokerLapStage.TakingJokerLap;
                shouldTakeJokerLap = false;
            }
            else
            {
                Log("⏩ Joker Lap trigger entered, but skipping.");
            }
            return;
        }

        // Just log regular waypoints — no progression here!
        if (!isTakingJokerLap && currentWaypointIndex < waypoints.Count)
        {
            Transform expected = waypoints[currentWaypointIndex];
            if (other.transform == expected)
                Log($"✅ Entered Waypoint {currentWaypointIndex}");
            else
                LogWarning($"❌ Wrong WP: Expected {expected.name}, got {other.transform.name}");
        }

        if (isTakingJokerLap && jokerWaypointIndex < jokerWaypoints.Count)
        {
            Transform expectedJoker = jokerWaypoints[jokerWaypointIndex];
            if (other.transform == expectedJoker)
                Log($"🎯 Entered Joker WP {jokerWaypointIndex}");
            else
                LogWarning($"❌ Wrong Joker WP: Expected {expectedJoker.name}, got {other.transform.name}");
        }

        // Joker exit logic
        if (other.transform == jokerLapExitPoint && !hasTakenJokerLap)
        {
            lapCount++;
            Log($"🏁 Joker Lap completed! Lap {lapCount}");
        }
    }
    Transform FindClosestJokerLapEntry()
    {
        float minDist = Mathf.Infinity;
        Transform closest = null;

        foreach (var entry in jokerLapEntries)
        {
            if (!entry) continue;
            float dist = Vector3.Distance(transform.position, entry.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = entry;
            }
        }

        return closest;
    }
    //=================================================================================================================================================================================================================================================
    // NavMesh Handling
    // Updates the NavMeshAgent's destination and handles recovery if off NavMesh
    void UpdateNavMeshAgent(Transform targetWaypoint)
    {
        if (!IsOnNavMesh(transform.position))
        {
            if (!isInRecoveryMode)
            {
                isInRecoveryMode = true;
                recoveryTimer = 0f;

                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    recoveryTarget = hit.position;
                    //Log($"🔁 Off NavMesh - entering recovery mode to {recoveryTarget}");
                }
            }
        }

        if (isInRecoveryMode)
        {
            recoveryTimer += Time.deltaTime;
            agentFollower.SetDestination(recoveryTarget);
            // ⚪ White: Recovery path line
            if (debugNavMeshRecovery)
                Debug.DrawLine(transform.position, recoveryTarget, Color.white);


            if (IsOnNavMesh(transform.position))
            {
                //Log("✅ Recovered onto NavMesh.");
                isInRecoveryMode = false;
            }
            else if (recoveryTimer > maxRecoveryTime)
            {
                //LogWarning("❌ Recovery failed, teleporting to last valid NavMesh position.");
                transform.position = recoveryTarget;
                isInRecoveryMode = false;
            }
        }
        else
        {
            Vector3 safeTarget = GetNavMeshEdgeClamp(targetWaypoint.position, carWidth * 0.5f + 0.2f);
            agentFollower.SetDestination(safeTarget);

            if (!IsPositionSafeOnNavMesh(safeTarget, carWidth * 0.5f + 0.2f))
            {
                Vector3 forwardSafe = GetNavMeshEdgeClamp(transform.position + transform.forward * 3f, 1.2f);
                agentFollower.SetDestination(forwardSafe);
                LogWarning("⚠️ Target not safe, using forward-safe point instead.");
                return;
            }
        }
    }
    private bool IsPositionSafeOnNavMesh(Vector3 position, float edgeThreshold = 0.5f)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
            {
                float distanceToEdge = Vector3.Distance(hit.position, edgeHit.position);
                Debug.DrawLine(hit.position, edgeHit.position, new Color(1f, 0.5f, 0f)); // 🟠 Orange: Distance to edge

                return distanceToEdge >= edgeThreshold;
            }
        }
        return false; // Not on NavMesh or too close to edge
    }
    private bool PredictFutureTrajectoryOffNavMesh(Vector3 position, Vector3 direction, float speed, float steeringInput)
    {
        // Predict further at higher speeds (e.g., 1.5s at 30 speed → 3s at 60 speed)
        float basePredictionTime = 1.5f;
        float predictionDuration = basePredictionTime + (speed / 20f); // Scales with speed
        int steps = 10;
        float timeStep = predictionDuration / steps;

        Vector3 simPos = position;
        Vector3 simDir = direction.normalized;

        for (int i = 0; i < steps; i++)
        {
            float turnAmount = steeringInput * AiMaxAngle;
            simDir = Quaternion.Euler(0f, turnAmount * timeStep, 0f) * simDir;
            simPos += simDir * speed * timeStep;

            if (!IsPositionSafeOnNavMesh(simPos, 1f))
            {
                if (debugTrajectoryPrediction)
                Debug.DrawRay(simPos, Vector3.up * 2f, Color.red, 1f); // 🔴 unsafe

                return true;
            }

            if (debugTrajectoryPrediction)
            {
                Debug.DrawRay(simPos, Vector3.up * 2f, Color.green, 0.5f); // ✅ safe
                Debug.DrawRay(simPos + Vector3.up * 0.2f, Vector3.up * 0.5f, Color.red, 1f); // unsafe point
            }
        }
        return false;
    }
    private bool IsOnNavMesh(Vector3 position, float maxDistance = 1.0f)
    {
        return NavMesh.SamplePosition(position, out _, maxDistance, NavMesh.AllAreas);
    }
    Vector3 GetNavMeshEdgeClamp(Vector3 targetPosition, float safeDistance = 1f)
    {
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
            {
                float distToEdge = Vector3.Distance(hit.position, edgeHit.position);

                

                
                Vector3 directionAway = (hit.position - edgeHit.position).normalized;
                if (debugNavMeshEdgeClamp)
                {
                    Debug.DrawLine(
                        hit.position + Vector3.up * 0.5f,
                        edgeHit.position + Vector3.up * 0.5f,
                        Color.red,
                        1f
                    );

                    Debug.DrawRay(
                        hit.position + Vector3.up * 0.5f,
                        directionAway * 2f,
                        Color.magenta,
                        1f
                    );
                }

                float requiredClearance = Mathf.Max(carWidth * 0.5f + 0.3f, safeDistance);


                // Clamp if too close
                if (distToEdge < requiredClearance)
                {
                    return hit.position + directionAway * (safeDistance - distToEdge);
                }

                return hit.position;
            }
        }

        return targetPosition;
    }
    //=================================================================================================================================================================================================================================================
    // Obstacle Detection
    // Detects static obstacles and cars ahead using raycasts
    bool DetectObstaclesAhead()
    {
        float speed = carController._rigidbody.linearVelocity.magnitude;
        float dynamicRange = baseObstacleRange + speed * 0.8f;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        int coneRayCount = 5;
        float coneAngle = 45f;
        float angleStep = coneAngle / (coneRayCount - 1);

        for (int i = 0; i < coneRayCount; i++)
        {
            float angleOffset = -coneAngle / 2f + i * angleStep;
            Vector3 dir = Quaternion.Euler(0, angleOffset, 0) * transform.forward;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dynamicRange, obstacleLayers))
            {
                // Skip cars here – this is for static objects only
                if (((1 << hit.collider.gameObject.layer) & carLayer) != 0)
                    continue;

                if (debugObstacleDetection)
                    Debug.DrawRay(origin, dir * hit.distance, Color.red);

                return true;
            }

            if (debugObstacleDetection)
                Debug.DrawRay(origin, dir * dynamicRange, Color.gray);
        }

        return false;
    }
    bool DetectCarsAhead()
    {
        float speed = carController._rigidbody.linearVelocity.magnitude;
        float dynamicRange = baseObstacleRange + speed * 0.8f;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // Cone Raycast (same as before, but wider angle now)
        int coneRayCount = 7;
        float coneAngle = 60f;
        float angleStep = coneAngle / (coneRayCount - 1);

        for (int i = 0; i < coneRayCount; i++)
        {
            float angleOffset = -coneAngle / 2f + i * angleStep;
            Vector3 dir = Quaternion.Euler(0, angleOffset, 0) * transform.forward;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dynamicRange, carLayer))
            {
                if (debugObstacleDetection)
                    Debug.DrawRay(origin, dir * hit.distance, Color.magenta);

                return true;
            }
        }

        // NEW fallback: SphereCast straight forward (detect slow cars on curves)
        if (Physics.SphereCast(origin, 1.5f, transform.forward, out RaycastHit sphereHit, dynamicRange, carLayer))
        {
            if (debugObstacleDetection)
                Debug.DrawRay(origin, transform.forward * sphereHit.distance, Color.yellow);

            return true;
        }

        return false;
    }
    Vector3 EvaluateOvertakeSide(Vector3 forward)
    {
        Vector3 offsetDir = Vector3.Cross(Vector3.up, forward).normalized;
        float checkOffset = carWidth * 2f;
        float sideLength = 4f;

        Vector3 leftOrigin = transform.position + offsetDir * checkOffset;
        Vector3 rightOrigin = transform.position - offsetDir * checkOffset;

        bool leftBlocked = Physics.CheckBox(leftOrigin, new Vector3(1f, 1f, sideLength), transform.rotation, obstacleLayers | carLayer);
        bool rightBlocked = Physics.CheckBox(rightOrigin, new Vector3(1f, 1f, sideLength), transform.rotation, obstacleLayers | carLayer);

        if (!leftBlocked && rightBlocked) return offsetDir * sideOffset;
        if (leftBlocked && !rightBlocked) return -offsetDir * sideOffset;
        if (!leftBlocked && !rightBlocked)
        {
            // Go to wider side (measured by raycast)
            float leftClear = Physics.Raycast(leftOrigin + Vector3.up, forward, out RaycastHit hitLeft, 8f, obstacleLayers | carLayer) ? hitLeft.distance : 8f;
            float rightClear = Physics.Raycast(rightOrigin + Vector3.up, forward, out RaycastHit hitRight, 8f, obstacleLayers | carLayer) ? hitRight.distance : 8f;
            return (leftClear > rightClear) ? offsetDir * sideOffset : -offsetDir * sideOffset;
        }

        return Vector3.zero;
    }
    //=================================================================================================================================================================================================================================================
    // Debugging
    // Logs with color-coded prefixes
    void DrawDebugLines()
    {
        if (debugWaypointPath)
        {
            Vector3 dir = (agentFollower.destination - transform.position).normalized;
            Debug.DrawRay(transform.position, dir * 5f, Color.cyan);
            Debug.DrawRay(transform.position, transform.forward * 5f, Color.yellow);
        }

        if (isTakingJokerLap && debugJokerLapPath)
        {
            Debug.DrawRay(
                transform.position + Vector3.up * 1f,
                (jokerWaypoints[jokerWaypointIndex].position - transform.position).normalized * 5f,
                Color.magenta
            );
        }

        if (debugDrawBezierCurve)
        {
            List<Transform> path = isTakingJokerLap ? jokerWaypoints : waypoints;
            int i = isTakingJokerLap ? jokerWaypointIndex : currentWaypointIndex;

            if (i >= 1 && i <= path.Count - 3)
            {
                Vector3 p0 = path[i - 1].position;
                Vector3 p1 = path[i].position;
                Vector3 p2 = path[i + 1].position;
                Vector3 p3 = path[i + 2].position;

                int samples = 20;
                Vector3 last = p1;

                for (int j = 1; j <= samples; j++)
                {
                    float t = j / (float)samples;
                    Vector3 pt = GetCatmullRomPosition(t, p0, p1, p2, p3);
                    Vector3 clamped = GetNavMeshEdgeClamp(pt, 1.2f);

                    Debug.DrawLine(last + Vector3.up * 0.1f, clamped + Vector3.up * 0.1f, Color.green);
                    last = clamped;
                }
            }
        }
    }
}
