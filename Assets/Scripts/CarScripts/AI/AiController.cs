using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;


    public enum AIBehaviorType
    {
        Careful,
        Aggressive,
        //Slow,     // 🚗 Noob-like behavior
        //Fast      // 🏎️ Pro-like behavior
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

    [Header("Waypoint Navigation")]
    public WayPointContainerScript wayPointContainer;
    public List<Transform> waypoints = new List<Transform>();
    private int currentWaypointIndex = 0;
    public float waypointRange = 0.5f; // AI must be within 0.5m of the waypoint before switching
    private HashSet<Transform> visitedWaypoints = new HashSet<Transform>();


    [Header("Navigation Target")]
    public NavMeshAgent agentFollower; // Reference to the child NavMeshAgent

    [Header("AI Steering & Speed")]
    private float currentGasInput = 0f;
    private float currentBrakeInput = 0f;
    private float currentAngle;
    private float AiGasInput;
    public bool IsInsideBrakeZone = false;
    public float AiMaxAngle = 45f;
    public float brakingLookaheadDistance = 15f;
    private float smoothSteering = 0f;
    private float targetSpeed = 25f; // AI adapts speed based on upcoming turns
    private float minTurnSpeed = 10f; // Minimum speed in sharp turns
    private float maxStraightSpeed = 30f; // Maximum speed on straight roads

    [Header("Obstacle Avoidance")]
    public float baseObstacleRange = 10f;
    public LayerMask obstacleLayers;
    public LayerMask carLayer;

    [Header("Overtaking")]
    private Vector3 overtakeTargetOffset = Vector3.zero;
    private Vector3 overtakeTargetPosition = Vector3.zero;
    private Transform overtakeTargetCar = null;
    private BoxCollider carCollider;
    private float carWidth = 2f; // fallback

    [Header("Surrounding Obstacle Detection")]
    public float detectionRadius = 4f;
    public float minAvoidanceSteer = 0.3f;
    public AIBehaviorType aiBehavior = AIBehaviorType.Careful;
    public float overtakeBoost = 1.3f; // How much faster the car tries to go during overtake
    public float sideOffset = 2f;      // How far it tries to move left/right to overtake

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

    [Header("Recovery Mode")]
    private bool isInRecoveryMode = false;
    private float recoveryTimer = 0f;
    private float maxRecoveryTime = 5f;
    private Vector3 recoveryTarget;

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
    void Start()
    {
        carController = GetComponent<CarController>();
        carCollider = GetComponent<BoxCollider>();

        if (carCollider != null)
        {
            carWidth = carCollider.size.x * transform.localScale.x;
        }

        // Use pre-assigned waypoints (from GameManager), fallback to container only if needed
        if ((waypoints == null || waypoints.Count == 0) && wayPointContainer != null)
        {
            waypoints = wayPointContainer.waypoints;
            Debug.LogWarning($"⚠️ {name} fallback: waypoints pulled from WayPointContainer.");
        }

        if ((jokerWaypoints == null || jokerWaypoints.Count == 0) && jokerLapWaypointContainer != null)
        {
            jokerWaypoints = jokerLapWaypointContainer.waypoints;
            Debug.LogWarning($"⚠️ {name} fallback: joker waypoints pulled from JokerLapWaypointContainer.");
        }

        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogError($"❌ {name} has no waypoints assigned!");
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
                maxStraightSpeed = 30f;
                minTurnSpeed = 12f;
                overtakeBoost = 1.3f;
                AiMaxAngle = 50f;
                break;

            case AIBehaviorType.Careful: // Automobilis 2
                maxStraightSpeed = 25f;
                minTurnSpeed = 10f;
                overtakeBoost = 1.2f;
                AiMaxAngle = 40f;
                break;

            /*
            case AIBehaviorType.Slow:
                maxStraightSpeed = 20f;
                minTurnSpeed = 6f;
                overtakeBoost = 1.1f;
                AiMaxAngle = 35f;
                break;

            case AIBehaviorType.Fast:
                maxStraightSpeed = 38f;
                minTurnSpeed = 15f;
                overtakeBoost = 1.5f;
                AiMaxAngle = 55f;
                break;
            */
            
        }

        Debug.Log($"🚗 {name} AI initialized with {waypoints.Count} waypoints, behavior: {aiBehavior}");
    }
    void Update()
    {
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

        Debug.Log($"AI State: {currentState}");

    }
    void HandleIdleState()
    {
        if (IsOnNavMesh(transform.position))
        {
            currentState = AIState.Driving;
        }
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
        Vector3 forward = transform.forward;

        if (overtakeTargetCar == null)
        {
            // Look for a car ahead to overtake
            if (Physics.Raycast(origin, forward, out RaycastHit hit, detectDistance, carLayer))
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
            overtakeTargetOffset = EvaluateOvertakeSide(forward);

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
            Vector3 tentativeTarget = transform.position + forward * 10f + overtakeTargetOffset;
            overtakeTargetPosition = GetNavMeshEdgeClamp(tentativeTarget, 1f);

            // Predict if the path is safe on NavMesh
            if (PredictFutureTrajectoryOffNavMesh(transform.position, (overtakeTargetPosition - transform.position).normalized, speed, smoothSteering))
            {
                // Path unsafe, brake and cancel overtaking
                carController.SetAISteering(0f);
                carController.SetAIGas(0f);
                carController.SetAIBrake(true);
                Debug.LogWarning($"❌ Overtake path unsafe - braking");
                return;
            }

            // Calculate steering towards overtake target
            Vector3 steerDir = (overtakeTargetPosition - transform.position).normalized;
            float steerAngle = Vector3.SignedAngle(transform.forward, steerDir, Vector3.up);
            float steerAmount = Mathf.Clamp(steerAngle / AiMaxAngle, -1f, 1f);

            // Smooth steering and gas input
            smoothSteering = Mathf.Lerp(smoothSteering, steerAmount, Time.deltaTime * 3f);
            targetSpeed = Mathf.Min(maxStraightSpeed * overtakeBoost, maxStraightSpeed + 15f);
            currentGasInput = Mathf.Lerp(currentGasInput, 1f, Time.deltaTime * 2f);

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
            transform.position = recoveryTarget;
            currentState = AIState.Driving;
        }
    }
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
            Debug.LogWarning($"❌ Missing waypoint {index}.");
            if (isTakingJokerLap) jokerWaypointIndex++;
            else NextWaypoint();
        }
        return target;
    }
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
                    //Debug.Log($"🔁 Off NavMesh - entering recovery mode to {recoveryTarget}");
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
                //Debug.Log("✅ Recovered onto NavMesh.");
                isInRecoveryMode = false;
            }
            else if (recoveryTimer > maxRecoveryTime)
            {
                //Debug.LogWarning("❌ Recovery failed, teleporting to last valid NavMesh position.");
                transform.position = recoveryTarget;
                isInRecoveryMode = false;
            }
        }
        else
        {
            agentFollower.SetDestination(targetWaypoint.position);
        }
    }
    void ApplySteering()
    {
        List<Transform> currentPath = isTakingJokerLap ? jokerWaypoints : waypoints;
        int index = isTakingJokerLap ? jokerWaypointIndex : currentWaypointIndex;

        if (index >= currentPath.Count - 1) return;

        Vector3 start = transform.position;
        Vector3 control = currentPath[index].position;
        Vector3 end = currentPath[index + 1].position;

        // Inner line = tighter turns for aggressive drivers
        float t = (aiBehavior == AIBehaviorType.Aggressive) ? 0.2f : 0.4f;


        Vector3 bezierTarget =
            Mathf.Pow(1 - t, 2) * start +
            2 * (1 - t) * t * control +
            Mathf.Pow(t, 2) * end;

        // ✅ Clamp the Bezier target away from NavMesh edge
        Vector3 clampedTarget = GetNavMeshEdgeClamp(bezierTarget, 1.2f);

        Vector3 steerDirection = (clampedTarget - transform.position).normalized;
        currentAngle = Vector3.SignedAngle(transform.forward, steerDirection, Vector3.up);

        float steerAmount = Mathf.Clamp(currentAngle / AiMaxAngle, -1f, 1f);
        smoothSteering = Mathf.Lerp(smoothSteering, steerAmount, Time.deltaTime * 5f);
        carController.SetAISteering(smoothSteering);

        if (debugDrawStraightLine)
            Debug.DrawLine(control, end, Color.red);

        if (debugDrawBezierCurve)
            Debug.DrawLine(transform.position, clampedTarget, Color.blue);
    }
    void ApplyThrottleAndBrakes()
    {
        float speed = carController._rigidbody.linearVelocity.magnitude;
        float desiredGas = Mathf.Clamp01(targetSpeed / speed);
        float desiredBrake = CalculateBraking();

        // 🔁 Gas transitions slower (smoother acceleration)
        currentGasInput = Mathf.Lerp(currentGasInput, desiredBrake > 0f ? 0f : desiredGas, Time.deltaTime * 2.5f);

        // ⏩ Brake responds faster (for sharp corners)
        currentBrakeInput = Mathf.Lerp(currentBrakeInput, desiredBrake, Time.deltaTime * 8f);

        // 🚨 Hard brake if urgent
        if (desiredBrake > 0.8f)
            currentBrakeInput = desiredBrake;

        carController.SetAIGas(currentGasInput);
        carController.SetAIBrake(currentBrakeInput > 0.05f);
    }
    void CheckProximityToWaypoint()
    {
        List<Transform> currentPath = isTakingJokerLap ? jokerWaypoints : waypoints;
        int index = isTakingJokerLap ? jokerWaypointIndex : currentWaypointIndex;

        if (index >= currentPath.Count) return;

        Transform wp = currentPath[index];
        float distance = Vector3.Distance(transform.position, wp.position);

        // NEW: Check if it's behind the car
        Vector3 toWaypoint = (wp.position - transform.position).normalized;
        float forwardDot = Vector3.Dot(transform.forward, toWaypoint); // forward = 1, behind = -1

        bool isBehind = forwardDot < 0f;
        bool isCloseEnough = distance < waypointRange;

        if (isCloseEnough || isBehind)
        {
            if (isTakingJokerLap)
            {
                // ONLY continue if this is the current expected Joker waypoint
                if (wp == jokerWaypoints[jokerWaypointIndex])
                    NextJokerWaypoint();
            }
            else
            {
                if (visitedWaypoints.Contains(wp)) return;

                // ONLY continue if this is the current expected waypoint
                if (wp == waypoints[currentWaypointIndex])
                    NextWaypoint();
            }
        }
    }
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
    }
    void NextWaypoint()
    {
        currentWaypointIndex++;

        if (currentWaypointIndex >= waypoints.Count)
        {
            currentWaypointIndex = 1; // Skip index 0 to avoid instant lap spam
            lapCount++;

            Debug.Log($"🏁 {name} finished lap {lapCount}");

            if (!hasTakenJokerLap)
            {
                float chance = 0.4f;
                shouldTakeJokerLap = Random.value < chance;
                Debug.Log(shouldTakeJokerLap ? $"🃏 {name} will take Joker Lap!" : $"➡️ {name} will skip Joker Lap.");
            }
        }
    }
    void NextJokerWaypoint()
    {
        jokerWaypointIndex++;
        if (jokerWaypointIndex >= jokerWaypoints.Count)
        {
            Debug.Log("🃏 Joker Lap path complete → reconnecting to normal path at specific rejoin point");

            isTakingJokerLap = false;
            hasTakenJokerLap = true;
            jokerLapStage = JokerLapStage.None;
            jokerWaypointIndex = 0;

            if (jokerLapExitRejoinPoint != null && waypoints.Contains(jokerLapExitRejoinPoint))
            {
                currentWaypointIndex = waypoints.IndexOf(jokerLapExitRejoinPoint);
                Debug.Log($"🔁 Reconnected to normal path at WP {currentWaypointIndex} ({jokerLapExitRejoinPoint.name})");
            }
            else
            {
                Debug.LogWarning("⚠️ JokerLapExitRejoinPoint is not assigned or not found in waypoints list! Falling back.");
                currentWaypointIndex = Mathf.Max(waypoints.Count - 2, 0);
            }

            visitedWaypoints.Clear();
        }
    }
    float CalculateBraking()
    {
        if (isInRecoveryMode) return 0f;
        

        float speed = carController._rigidbody.linearVelocity.magnitude;
        if (speed < 5f) return 0f;

        // Dynamic lookahead based on speed
        int baseLookahead = 2;
        int speedLookahead = Mathf.FloorToInt(speed / 7f); // +1 per 10 speed
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
            if (severity > maxSeverity)
                maxSeverity = severity;

            currentDir = dirToNext;
        }

        float brakeStrength = 0f;

        // More aggressive braking for sharp turns at high speed
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

        // Prediction fallback
        if (PredictFutureTrajectoryOffNavMesh(transform.position, transform.forward, speed, smoothSteering))
        {
            Debug.LogWarning("⚠️ Predictive: braking for safety!");
            brakeStrength = Mathf.Max(brakeStrength, 0.7f);
        }

        // Force brake in special zones
        if (IsInsideBrakeZone)
        {
            brakeStrength = Mathf.Max(brakeStrength, 0.5f);
        }

        return Mathf.Clamp01(brakeStrength);
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
            Debug.DrawRay(simPos, Vector3.up * 2f, Color.green, 0.5f); // ✅ safe

        }

        return false;
    }
    void AdjustSpeedBeforeTurn()
    {
        int nextIndex = (currentWaypointIndex + 1) % waypoints.Count;
        Transform nextWaypoint = waypoints[nextIndex];
        if (!nextWaypoint) return;

        Vector3 nextDirection = (nextWaypoint.position - transform.position).normalized;
        float nextAngle = Vector3.SignedAngle(transform.forward, nextDirection, Vector3.up);
        float angleSeverity = Mathf.Abs(nextAngle);

        if (angleSeverity > 60f)
            targetSpeed = minTurnSpeed;      // Very sharp
        else if (angleSeverity > 40f)
            targetSpeed = 15f;               // Medium
        else if (angleSeverity > 20f)
            targetSpeed = 22f;               // Small bend
        else
            targetSpeed = maxStraightSpeed; // Almost straight
        if(debugSpeedAdjustment)
        {
            Debug.Log($"🌀 Adjusting AI Speed: {targetSpeed:F2} for turn angle: {nextAngle:F2}°");

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
    private void OnTriggerEnter(Collider other)
    {
        // Joker Lap trigger
        if (other.CompareTag("JokerLapTrigger"))
        {
            if (shouldTakeJokerLap && !hasTakenJokerLap)
            {
                Debug.Log("🃏 Joker Lap triggered. Entering Joker Lap path!");
                isTakingJokerLap = true;
                jokerWaypointIndex = 0;
                jokerLapStage = JokerLapStage.TakingJokerLap;
                shouldTakeJokerLap = false;
                // We'll mark hasTakenJokerLap after completing all joker waypoints
            }
            else
            {
                Debug.Log("⏩ Joker Lap trigger entered, but skipping (already taken or not this lap).");
            }
            return;
        }


        // Regular waypoint trigger
        if (!isTakingJokerLap && currentWaypointIndex < waypoints.Count)
        {
            Transform expected = waypoints[currentWaypointIndex];
            if (other.transform == expected)
            {
                Debug.Log($"✅ Reached expected Waypoint {currentWaypointIndex}");
                NextWaypoint();
            }
            else
            {
                Debug.LogWarning($"❌ Wrong waypoint entered. Expected: {expected.name}, got: {other.transform.name}");
            }
        }


        // Joker Lap waypoint trigger
        else if (isTakingJokerLap && jokerWaypointIndex < jokerWaypoints.Count)
        {
            Transform expectedJoker = jokerWaypoints[jokerWaypointIndex];
            if (other.transform == expectedJoker)
            {
                Debug.Log($"🎯 Reached Joker Waypoint {jokerWaypointIndex}");
                NextJokerWaypoint();
            }
            else
            {
                Debug.LogWarning($"❌ Wrong Joker Waypoint entered. Expected: {expectedJoker.name}, got: {other.transform.name}");
            }
        }


        // Joker Lap Exit Trigger
        if (other.transform == jokerLapExitPoint && !hasTakenJokerLap)
        {
            lapCount++;

            Debug.Log($"🏁 Joker Lap completed at exit! Lap {lapCount} counted.");
            return;
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

               

                // Clamp if too close
                if (distToEdge < safeDistance)
                {
                    return hit.position + directionAway * (safeDistance - distToEdge);
                }

                return hit.position;
            }
        }

        return targetPosition;
    }
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
        float sideClearance = carWidth * 1.5f;
        float forwardCheckDistance = 8f; // how far ahead to check from side position

        Vector3 leftPos = transform.position + offsetDir * sideClearance;
        Vector3 rightPos = transform.position - offsetDir * sideClearance;

        bool leftClear = !Physics.CheckBox(leftPos, new Vector3(carWidth * 0.5f, 1f, 3f), transform.rotation, carLayer | obstacleLayers);
        bool rightClear = !Physics.CheckBox(rightPos, new Vector3(carWidth * 0.5f, 1f, 3f), transform.rotation, carLayer | obstacleLayers);

        // Additional forward check from sides
        bool leftAheadClear = !Physics.Raycast(leftPos + Vector3.up * 0.5f, forward, forwardCheckDistance, carLayer | obstacleLayers);
        bool rightAheadClear = !Physics.Raycast(rightPos + Vector3.up * 0.5f, forward, forwardCheckDistance, carLayer | obstacleLayers);

        if (debugObstacleDetection)
        {
            Debug.DrawRay(leftPos + Vector3.up * 0.5f, forward * forwardCheckDistance, Color.blue);
            Debug.DrawRay(rightPos + Vector3.up * 0.5f, forward * forwardCheckDistance, Color.blue);
        }

        if (leftClear && leftAheadClear && !rightClear)
            return offsetDir * sideOffset;
        else if (!leftClear && rightClear && rightAheadClear)
            return -offsetDir * sideOffset;
        else if (leftClear && rightClear && leftAheadClear && rightAheadClear)
            return (Random.value > 0.5f ? 1 : -1) * offsetDir * sideOffset;
        else
            return Vector3.zero; // neither side is safe
    }

}
