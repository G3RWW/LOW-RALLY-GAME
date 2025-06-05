using UnityEngine;
using UnityEngine.UI; // Add UI namespace
using System.IO;
using System.Text;
using JetBrains.Annotations;
using UnityEngine.AI;

public enum GearState
{
    Neutral,
    Running,
    CheckingChange,
    Changing
};

public class CarController : MonoBehaviour
{
    [Header("Car Data")]
    public CarData carData; // Reference to ScriptableObject
    public EngineSoundFMOD engineSound; // Reference to EngineSound script

    [Header("UI Elements")]
    public Text speedText;
    public Text rpmText;
    public Text gearText;
    public Image rpmBar;

    [Header("Wheel Colliders & Visuals")]
    public Transform centerOfMass; // Assign this in Unity
    public WheelCollider wheelColliderFl, wheelColliderFr, wheelColliderRl, wheelColliderRr;
    public Transform wheelFl, wheelFr, wheelRl, wheelRr;

    [Header("Input & Output")]
    public float gasInput;
    private bool isEngineRunning = true; // Assuming the engine starts running
    public Rigidbody _rigidbody;
    public bool isHandbraking, isBraking, isClutchEngaged;
    public int currentGear = 1;
    public bool isAutomatic = true;
    public bool isClutchAuto = true;

    [Header("Transmission & RPM")]
    public float currentRPM = 1000f;
    private float clutchValue = 1f;
    private float shiftCooldown = 0.5f; // Time in seconds between shifts
    private float lastShiftTime = 0f;   // Tracks when the last shift occurred

    [Header("RPM & Gear Scaling")]
    public float gearRatioMultiplier = 4.29f;

    [Header("Brake Light Setup")]
    public Renderer monoBrakeLight;  // For cars with a single brake light (e.g., Porsche 911)
    public Renderer leftBrakeLight;  // For cars with separate brake lights
    public Renderer rightBrakeLight; // For cars with separate brake lights
    public Color brakeLightColor = Color.red;
    private Material monoBrakeMaterial, leftBrakeMaterial, rightBrakeMaterial;
    private Color baseEmissionColor;
    public float brakeEmissionIntensity = 5f;
    
    [Header("Skid Marks")]
    public GameObject skidPrefab; // Assign skid mark prefab in Unity
    private TrailRenderer[] skidTrails = new TrailRenderer[4]; // Array to hold skid trails
    private GameObject[] activeSkidInstances = new GameObject[4]; // Track active skid prefabs
    private bool[] isSkidding = new bool[4]; // Track skid state per wheel

    private float[] skidTimers = new float[4]; // One timer per wheel
    public float skidDelayThreshold = 0.5f; // Time before smoke appears when sliding on asphalt

    [Header("AI Control")]
    public bool isAIControlled = false; // If true, blocks player input

    [Header("FX")]
    public GameObject dirtParticlePrefab;
    private GameObject[] activeDirtParticles = new GameObject[4];
    [Header("Smoke FX")]
    public GameObject smokeParticlePrefab;
    private GameObject[] activeSmokeParticles = new GameObject[4];
    [Header("Smoke Materials")]
    public Material asphaltSmokeMaterial;
    public Material dirtSmokeMaterial;
    public Material mudSmokeMaterial;
    public Material gravelSmokeMaterial;

    public Color GetColorByNavMeshArea(int area)
    {
        switch (area)
        {
            case 2: return new Color(0.75f, 0.6f, 0.4f); // Dirt → light brown
            case 3: return new Color(0.3f, 0.2f, 0.1f);  // Mud → dark brown
            case 4: return new Color(0.6f, 0.6f, 0.6f);  // Gravel → gray
            default: return new Color(0.5f, 0.5f, 0.5f); // Default smoke gray
        }
    }
    [Header("Debug Log")]
    private StringBuilder logData = new StringBuilder(); // Stores logs in memory
    private string logFilePath;
    // Debugging wheel collider anomalies
    private string lastKnownAreaType = "";

    [Header("Debug Toggles")]
    public bool debugGripChange = false;
    public bool debugGripAreaChange = false;
    public bool debugSkidMarks = false;
    public bool debugJumpDetection = false;
    public bool debugTwitchDetection = false;
    public bool debugRPMandTorque = false;
    public bool debugManualGearShift = false;
    public bool debugAutomaticGearShift = false;
    public bool debugBrakeLights = false;

//=============================================================================================
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();

        if (centerOfMass != null)
        {
            _rigidbody.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }

        // ✅ Generate unique log filename with local timestamp
        //string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        //logFilePath = Path.Combine(Application.persistentDataPath, $"CarDebugLog_{timestamp}.txt");
        //logData.AppendLine($"=== Car Debug Log Started at {System.DateTime.Now} ===\n");

        ApplyWheelSettings();

        if (monoBrakeLight != null)
        {
            monoBrakeMaterial = monoBrakeLight.material;
            baseEmissionColor = monoBrakeMaterial.GetColor("_EmissionColor");
            monoBrakeMaterial.SetColor("_EmissionColor", baseEmissionColor * 0f);
        }

        if (leftBrakeLight != null)
        {
            leftBrakeMaterial = leftBrakeLight.material;
            leftBrakeMaterial.SetColor("_EmissionColor", brakeLightColor * 0f);
        }

        if (rightBrakeLight != null)
        {
            rightBrakeMaterial = rightBrakeLight.material;
            rightBrakeMaterial.SetColor("_EmissionColor", brakeLightColor * 0f);
        }

        // Initialize skid marks for each wheel
        skidTrails[0] = CreateSkidTrail();
        skidTrails[1] = CreateSkidTrail();
        skidTrails[2] = CreateSkidTrail();
        skidTrails[3] = CreateSkidTrail();
    }
    void FixedUpdate()
    {
        ApplyGripFromNavMesh();

        if (!isAIControlled) // Player-controlled mode
        {
            gasInput = Input.GetAxis("Vertical");
            if (Mathf.Abs(gasInput) < 0.05f) gasInput = 0f; // Dead zone

            float steer = Input.GetAxis("Horizontal") * carData.maxSteerAngle;
            isBraking = Input.GetKey(KeyCode.S);
            isHandbraking = Input.GetKey(KeyCode.Space);
            isAutomatic = carData.isAutomatic;
            isClutchAuto = carData.isClutchAuto;

            HandleSteering(steer);
        }

        // Common behavior for AI and Player
        HandleBraking();
        HandleTransmission(CalculateTorque());
        ApplyNaturalDeceleration();
        HandleWheelRolling();
        UpdateBrakeLights();
        UpdateUI();

        // Check for skidding and apply skids
        HandleSkidMarks();
        // HandleSkidParticles(); // Handle skid particles
        HandleDirtParticles();
        HandleSmoketParticles(); // Handle smoke particles
        
        /*
        DebugGripChanges(); // 👈 Call it here
        // Detect anomalies in physics
        DetectWheelColliderAnomalies();
        DetectCarJumping();
        */

    }
    void Update()
    {
        // Manual Clutch Engagement
        isClutchEngaged = !isClutchAuto && Input.GetKey(KeyCode.LeftShift);
        clutchValue = isClutchEngaged ? 0f : 1f;
    }
    void UpdateUI()
    {
        if (speedText != null)
            speedText.text = $"{_rigidbody.linearVelocity.magnitude * 3.6f:F0} km/h";

        if (rpmText != null)
            rpmText.text = $"RPM: {currentRPM:F0}";

        if (gearText != null)
        {
            string gearDisplay;
            if (currentGear == 0) gearDisplay = "R"; // Reverse
            else if (currentGear == 1) gearDisplay = "N"; // Neutral
            else gearDisplay = (currentGear - 1).ToString(); // Adjust gear number

            gearText.text = $"{gearDisplay}";
        }

        if (rpmBar != null)
        {
            // Normalize the RPM value (idle RPM is minimum, redline is max)
            float normalizedRPM = Mathf.InverseLerp(carData.idleRPM, carData.redline, currentRPM);
            rpmBar.fillAmount = normalizedRPM;
        }
    }
//=============================================================================================
    int GetNavMeshAreaAtPosition(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return hit.mask; // ✅ Correct area type ID
        }
        return -1;
    }
    string GetAreaNameByIndex(int index)
    {
        switch (index)
        {
            case 0: return "Walkable";
            case 1: return "Not Walkable";
            case 2: return "Dirt";
            case 3: return "Mud";
            case 4: return "Gravel";
            default: return $"Area {index}";
        }
    }
    void ApplyWheelSettings()
    {
        // Get default friction settings
        WheelFrictionCurve frontForwardFriction = wheelColliderFl.forwardFriction;
        WheelFrictionCurve frontSidewaysFriction = wheelColliderFl.sidewaysFriction;
        WheelFrictionCurve rearForwardFriction = wheelColliderRl.forwardFriction;
        WheelFrictionCurve rearSidewaysFriction = wheelColliderRl.sidewaysFriction;

        // Base values
        float frontForwardGrip = carData.forwardFrictionStiffness;
        float frontSidewaysGrip = 0.8f;
        float rearForwardGrip = carData.forwardFrictionStiffness;
        float rearSidewaysGrip = 0.8f;

        // Adjust grip and torque application based on drivetrain
        switch (carData.drivetrain)
        {
            case DrivetrainType.FWD:
                frontForwardGrip *= 1.2f; // More front grip
                frontSidewaysGrip *= 1.1f; // Slightly better turn-in
                rearSidewaysGrip *= 0.85f; // Reduce rear grip → understeer
                break;

            case DrivetrainType.RWD:
                rearForwardGrip *= 1.8f; // More grip at rear for acceleration
                rearSidewaysGrip *= 0.9f; // Reduce rear grip → oversteer
                frontSidewaysGrip *= 1.3f; // Keep front grip normal
                break;

            case DrivetrainType.AWD:
                frontForwardGrip *= 1.1f;
                rearForwardGrip *= 1.1f;
                frontSidewaysGrip *= 1.0f;
                rearSidewaysGrip *= 1.0f; // Balanced AWD handling
                break;
        }

        // Apply new friction values
        frontForwardFriction.stiffness = frontForwardGrip;
        frontSidewaysFriction.stiffness = frontSidewaysGrip;
        rearForwardFriction.stiffness = rearForwardGrip;
        rearSidewaysFriction.stiffness = rearSidewaysGrip;

        // Apply to front wheels
        wheelColliderFl.forwardFriction = frontForwardFriction;
        wheelColliderFl.sidewaysFriction = frontSidewaysFriction;
        wheelColliderFr.forwardFriction = frontForwardFriction;
        wheelColliderFr.sidewaysFriction = frontSidewaysFriction;

        // Apply to rear wheels
        wheelColliderRl.forwardFriction = rearForwardFriction;
        wheelColliderRl.sidewaysFriction = rearSidewaysFriction;
        wheelColliderRr.forwardFriction = rearForwardFriction;
        wheelColliderRr.sidewaysFriction = rearSidewaysFriction;
    }
    void ApplyGripFromNavMesh()
    {
        int areaMask = GetNavMeshAreaAtPosition(transform.position);
        string currentArea = "Unknown";
        float areaCost = 1f; // Default (Asphalt)

        if (areaMask == -1)
        {
            currentArea = "No Area";
            areaCost = 1f;
        }
        else
        {
            for (int i = 0; i < 32; i++)
            {
                if ((areaMask & (1 << i)) != 0)
                {
                    currentArea = GetAreaNameByIndex(i);
                    areaCost = NavMesh.GetAreaCost(i);
                    break;
                }
            }
        }

        // Invert cost to create grip multiplier (higher cost = less grip)
        float gripMultiplier = Mathf.Clamp(1f / areaCost, 0.2f, 1f);

        // Apply grip settings to wheels
        WheelFrictionCurve ff = wheelColliderFl.forwardFriction;
        ff.stiffness = carData.forwardFrictionStiffness * gripMultiplier;
        wheelColliderFl.forwardFriction = ff;
        wheelColliderFr.forwardFriction = ff;
        wheelColliderRl.forwardFriction = ff;
        wheelColliderRr.forwardFriction = ff;

        WheelFrictionCurve sf = wheelColliderFl.sidewaysFriction;
        sf.stiffness = 0.8f * gripMultiplier;
        wheelColliderFl.sidewaysFriction = sf;
        wheelColliderFr.sidewaysFriction = sf;
        wheelColliderRl.sidewaysFriction = sf;
        wheelColliderRr.sidewaysFriction = sf;

        if (debugGripAreaChange)
        {
            if (currentArea != lastKnownAreaType)
            {
                Debug.Log($"🛞 Surface Changed → {currentArea} (Cost: {areaCost:F1}) → Grip x{gripMultiplier:F2}");
                logData.AppendLine($"[Surface Change] {currentArea} | Cost: {areaCost:F1} | Grip: x{gripMultiplier:F2} @ {Time.time:F2}s");
                lastKnownAreaType = currentArea;
            }
            else
            {
                Debug.Log($"🛞 Current Surface: {currentArea} | Cost: {areaCost:F1} | Grip: x{gripMultiplier:F2}");
            }
        }
    }
    public void ApplyTorqueToWheels(float torque)
    {
        if (isHandbraking) torque = 0f; // Disable torque when handbraking

        // Prevent over-revving limit enforcement
        if (currentRPM >= carData.redline - 100 && currentGear > 1)
        {
            float rpmExcess = (currentRPM - (carData.redline - 100)) / 100f;
            torque *= Mathf.Clamp01(1f - rpmExcess * 0.7f); 
            _rigidbody.linearDamping = 0.5f + (rpmExcess * 1.0f); 
        }
        else
        {
            _rigidbody.linearDamping = 0.02f; 
        }

        float torqueDirection = (currentGear == 0) ? -1f : 1f; // Reverse or Forward

        switch (carData.drivetrain)
        {
            case DrivetrainType.FWD:
                wheelColliderFl.motorTorque = torque * torqueDirection;
                wheelColliderFr.motorTorque = torque * torqueDirection;
                break;
            case DrivetrainType.RWD:
                wheelColliderRl.motorTorque = torque * torqueDirection;
                wheelColliderRr.motorTorque = torque * torqueDirection;
                break;
            case DrivetrainType.AWD:
                float splitTorque = (torque * torqueDirection) * 0.5f;
                wheelColliderFl.motorTorque = splitTorque;
                wheelColliderFr.motorTorque = splitTorque;
                wheelColliderRl.motorTorque = splitTorque;
                wheelColliderRr.motorTorque = splitTorque;
                break;
        }
        if (debugRPMandTorque)
        {
            // ✅ DEBUG LOGS - Check Values in Console
            Debug.Log($"Torque Applied to Wheels: {torque:F2} | Direction: {torqueDirection} | Current Gear: {currentGear} | RPM: {currentRPM:F0}");
            logData.AppendLine($"Torque Applied to Wheels: {torque:F2} | Direction: {torqueDirection} | Current Gear: {currentGear} | RPM: {currentRPM:F0}");
            Debug.Log($"Torque Applied to Wheels: {torque:F2}");
        }
    }   
    void HandleWheelRolling()
    {
        float speed = _rigidbody.linearVelocity.magnitude;
        float wheelCircumference = 2f * Mathf.PI * wheelColliderFl.radius;
        float rotationSpeed = (speed / wheelCircumference) * 360f * Time.fixedDeltaTime;

        wheelFl.Rotate(Vector3.right, rotationSpeed);
        wheelFr.Rotate(Vector3.right, rotationSpeed);
        if (!isHandbraking)
        {
            wheelRl.Rotate(Vector3.right, rotationSpeed);
            wheelRr.Rotate(Vector3.right, rotationSpeed);
        }
    }
//=============================================================================================
    void SetupBrakeLights()
    {
        if (monoBrakeLight != null)
        {
            monoBrakeMaterial = monoBrakeLight.material;
            baseEmissionColor = monoBrakeMaterial.GetColor("_EmissionColor"); // Save original color
        }

        if (leftBrakeLight != null)
        {
            leftBrakeMaterial = leftBrakeLight.material;
        }

        if (rightBrakeLight != null)
        {
            rightBrakeMaterial = rightBrakeLight.material;
        }
    }
    void UpdateBrakeLights()
    {
        bool brakeActive = isBraking;
        if(debugBrakeLights)
        {
            Debug.Log($"Brake Lights Update | Braking: {isBraking} | Handbraking: {isHandbraking} | Brake Active: {brakeActive}");
        }

        float emissionIntensity = brakeActive ? brakeEmissionIntensity : 0f; 

        Color targetColor = brakeLightColor * emissionIntensity; 

        if (monoBrakeMaterial != null)
        {
            monoBrakeMaterial.SetColor("_EmissionColor", targetColor);
            monoBrakeMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            monoBrakeMaterial.EnableKeyword("_EMISSION");
        }
        else
        {
            if (leftBrakeMaterial != null)
            {
                leftBrakeMaterial.SetColor("_EmissionColor", targetColor);
                leftBrakeMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                leftBrakeMaterial.EnableKeyword("_EMISSION");
            }

            if (rightBrakeMaterial != null)
            {
                rightBrakeMaterial.SetColor("_EmissionColor", targetColor);
                rightBrakeMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                rightBrakeMaterial.EnableKeyword("_EMISSION");
            }
        }
    }
    public void HandleBraking()
    {
        float appliedBrakeTorque = isBraking ? carData.brakeTorque * 2f : 0f; // Doubled braking force
        float frontBrakeBias = 0.6f; // 60% braking force to front wheels

        if (isBraking) // Normal braking when 'S' is pressed
        {
            // Apply front-heavy braking to mimic real-world physics
            wheelColliderFl.brakeTorque = appliedBrakeTorque * frontBrakeBias;
            wheelColliderFr.brakeTorque = appliedBrakeTorque * frontBrakeBias;
            wheelColliderRl.brakeTorque = appliedBrakeTorque * (1 - frontBrakeBias);
            wheelColliderRr.brakeTorque = appliedBrakeTorque * (1 - frontBrakeBias);

            // Reduce engine torque immediately when braking
            ApplyTorqueToWheels(0f);
        }
        else if (isHandbraking) // Handbrake engaged (locks rear wheels)
        {
            wheelColliderRl.brakeTorque = carData.handbrakeTorque;
            wheelColliderRr.brakeTorque = carData.handbrakeTorque;

            // Cut all engine torque when handbrake is used
            ApplyTorqueToWheels(0f);

            // Adjust rear tire friction to simulate skidding
            AdjustFrictionForHandbrake(true);
        }
        else // Release brakes when not pressed
        {
            wheelColliderFl.brakeTorque = 0f;
            wheelColliderFr.brakeTorque = 0f;
            wheelColliderRl.brakeTorque = 0f;
            wheelColliderRr.brakeTorque = 0f;

            // Restore normal friction when handbrake is released
            AdjustFrictionForHandbrake(false);
        }
    }
    void AdjustFrictionForHandbrake(bool isHandbraking)
    {
        EnableDriftMode(isHandbraking); 

        WheelFrictionCurve rearFriction = wheelColliderRl.sidewaysFriction;
        WheelFrictionCurve frontFriction = wheelColliderFl.sidewaysFriction;

        if (isHandbraking)
        {
            rearFriction.stiffness = 0.4f * carData.gripReductionFactor; 
            frontFriction.stiffness = 1.2f; 
        }
        else
        {
            rearFriction.stiffness = 1.0f;
            frontFriction.stiffness = 1.0f;
        }

        wheelColliderRl.sidewaysFriction = rearFriction;
        wheelColliderRr.sidewaysFriction = rearFriction;
        wheelColliderFl.sidewaysFriction = frontFriction;
        wheelColliderFr.sidewaysFriction = frontFriction;
    }
//=============================================================================================
    public void HandleSteering(float steer)
    {
        float speed = _rigidbody.linearVelocity.magnitude * 3.6f; // Convert m/s to km/h
        float speedFactor = carData.steeringSensitivityCurve.Evaluate(speed);

        // Dynamic steering limit (less rotation at high speeds)
        float maxAllowedSteering = Mathf.Lerp(10f, carData.maxSteerAngle, 1 - (speed / 200f));
        float adjustedSteering = Mathf.Clamp(steer * speedFactor, -maxAllowedSteering, maxAllowedSteering);

        // Apply countersteering if needed
        if (Mathf.Abs(_rigidbody.angularVelocity.y) > 0.2f)
        {
            float counterSteer = -_rigidbody.angularVelocity.y * (carData.driftSteerAssist * 1.5f);
            adjustedSteering += counterSteer;
        }

        // Reduce steering effectiveness if FWD (understeer prevention)
        if (carData.drivetrain == DrivetrainType.FWD)
        {
            adjustedSteering *= 0.9f; 
        }
        // Increase effectiveness if RWD (better rotation)
        else if (carData.drivetrain == DrivetrainType.RWD)
        {
            adjustedSteering *= 1.1f;
        }

        float driftFactor = isHandbraking ? 1.3f : 1f; // More steering when drifting
        wheelColliderFl.steerAngle = adjustedSteering * driftFactor;
        wheelColliderFr.steerAngle = adjustedSteering * driftFactor;

        // Visually rotate the wheels
        wheelFl.parent.localRotation = Quaternion.Euler(0, adjustedSteering, 0);
        wheelFr.parent.localRotation = Quaternion.Euler(0, adjustedSteering, 0);
    }
    void EnableDriftMode(bool enable)
    {
        WheelFrictionCurve rearFriction = wheelColliderRl.sidewaysFriction;
        WheelFrictionCurve frontFriction = wheelColliderFl.sidewaysFriction;

        if (enable)
        {
            // Lower rear grip and slightly increase front grip
            rearFriction.stiffness = 0.4f; 
            frontFriction.stiffness = 1.2f; 

            // Reduce overall grip at high speed for looser handling
            float speedEffect = Mathf.Clamp01(_rigidbody.linearVelocity.magnitude / 80f);
            rearFriction.stiffness *= (1 - speedEffect * 0.5f);

            // Apply a gentle counter-steer force automatically
            float driftSteer = -Input.GetAxis("Horizontal") * 15f;
            _rigidbody.AddTorque(Vector3.up * driftSteer);
        }
        else
        {
            // Reset grip to normal
            rearFriction.stiffness = 1.0f;
            frontFriction.stiffness = 1.0f;
        }

        // Apply new friction values
        wheelColliderRl.sidewaysFriction = rearFriction;
        wheelColliderRr.sidewaysFriction = rearFriction;
        wheelColliderFl.sidewaysFriction = frontFriction;
        wheelColliderFr.sidewaysFriction = frontFriction;
    }   
//=============================================================================================
    void HandleTransmission(float throttle)
    {

        if(isAutomatic)
        {
            HandleAutomaticShifting(); // Automatic gear shifting
        }
        else
        {
            HandleManualShifting(); // Manual gear shifting
        }

        int safeGearIndex = Mathf.Clamp(currentGear, 0, carData.gearRatios.Length - 1);
        float gearRatio = carData.gearRatios[safeGearIndex];
        


        float finalTorque = Mathf.Clamp(
            throttle * carData.motorPower * gearRatio * carData.differentialRatio,
            -carData.motorPower * 2.0f, 
            carData.motorPower * 2.0f
        );

        if (currentRPM >= carData.redline)
        {
            finalTorque = 0f; // Force no acceleration past redline
        }
        if (debugRPMandTorque)
        {
            Debug.Log($"Final Torque Sent to Wheels: {finalTorque:F2}");
        }
        ApplyTorqueToWheels(finalTorque);
    }
    public void HandleManualShifting()
    {
        if (Time.time - lastShiftTime < shiftCooldown)
            return; // Prevent gear skipping by enforcing a cooldown

        if (Input.GetKeyDown(KeyCode.E)) // Upshift
        {
            if (currentGear < carData.gearRatios.Length - 1) 
            {
                currentGear++;
                currentRPM *= 0.7f;
                lastShiftTime = Time.time;
                if (debugManualGearShift)
                {
                    Debug.Log($"[Manual Shift] Shift Up: Gear {currentGear}");
                    logData.AppendLine($"[Manual Shift] Shift Up: Gear {currentGear}");
                }
                if (currentGear > 1) // ✅ Smooth Transition from Neutral
                {
                    _rigidbody.linearVelocity *= 0.9f; // Reduce speed slightly to avoid jerking
                }
            }
            else
            {
                if (debugManualGearShift)
                {
                    Debug.Log("[Manual Shift] Already in highest gear");
                    logData.AppendLine("[Manual Shift] Already in highest gear");
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.Q)) // Downshift
        {
            if (currentGear > 0) 
            {
                currentGear--;
                currentRPM *= 0.7f;
                lastShiftTime = Time.time;
                if (debugManualGearShift)
                {
                    Debug.Log($"[Manual Shift] Shift Down: Gear {currentGear}");
                    logData.AppendLine($"[Manual Shift] Shift Down: Gear {currentGear}");
                }

                if (currentGear == 1) // ✅ Entering Neutral
                {
                    ApplyTorqueToWheels(0f);
                    if (debugManualGearShift)
                    {
                        Debug.Log("[Manual Shift] Entered Neutral - Removing Torque");
                        logData.AppendLine("[Manual Shift] Entered Neutral - Removing Torque");
                    }
                }
            }
            else
            {
                if (debugManualGearShift)
                {
                Debug.Log("[Manual Shift] Already in lowest gear");
                logData.AppendLine("[Manual Shift] Already in lowest gear");
                }
            }
        }
    }
    public void HandleAutomaticShifting()
    {
        if (Time.time - lastShiftTime < shiftCooldown)
            return; // Prevent gear skipping by enforcing a cooldown

        // ✅ Drive, Neutral, Reverse Handling
        if (Input.GetKeyDown(KeyCode.E)) // Pressing E puts the car in Drive (D)
        {
            if (currentGear == 0) // If in Reverse, go to Neutral first
            {
                currentGear = 1; // Neutral
                if(debugAutomaticGearShift)
                {
                    Debug.Log("[Automatic Shift] Shift to Neutral (N)");
                    logData.AppendLine("[Automatic Shift] Shift to Neutral (N)");
                }
            }
            else if (currentGear == 1) // If in Neutral, go to Drive
            {
                currentGear = 2; // Drive (D) starts from Gear 1 (index 2 in logic)
                if(debugAutomaticGearShift)
                {
                    Debug.Log("[Automatic Shift] Shift to Drive (D)");
                    logData.AppendLine("[Automatic Shift] Shift to Drive (D)");
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.Q)) // Pressing Q shifts to Neutral or Reverse
        {
            if (currentGear > 1) // If in Drive, move to Neutral first
            {
                currentGear = 1; // Neutral
                if(debugAutomaticGearShift)
                {
                    Debug.Log("[Automatic Shift] Shift to Neutral (N)");
                    logData.AppendLine("[Automatic Shift] Shift to Neutral (N)");
                }
            }
            else if (currentGear == 1) // If in Neutral, shift to Reverse
            {
                currentGear = 0; // Reverse
                if(debugAutomaticGearShift)
                {
                    Debug.Log("[Automatic Shift] Shift to Reverse (R)");
                    logData.AppendLine("[Automatic Shift] Shift to Reverse (R)");
                }
            }
        }

        // ✅ Gradual Throttle Reduction Near UpShiftRPM
        if (currentRPM >= carData.UpShiftRPM - 100 && currentGear > 1) // Reduce power near upshift
        {
            float rpmExcess = (currentRPM - (carData.UpShiftRPM - 100)) / 100f; // Normalize range from 0-1
            float throttle = Mathf.Clamp01(1f - rpmExcess * 0.7f); // Reduce throttle progressively
            gasInput = throttle;
        }

        // ✅ Automatic Upshifting in Drive Mode
        if (currentRPM >= carData.UpShiftRPM && currentGear < carData.gearRatios.Length - 1 && currentGear > 1)
        {
            currentGear++;
            currentRPM *= 0.7f;
            lastShiftTime = Time.time;
            if(debugAutomaticGearShift)
            {
                Debug.Log($"[Automatic Shift] Shift Up: Gear {currentGear}");
                logData.AppendLine($"[Automatic Shift] Shift Up: Gear {currentGear}");
            }
        }
        
        // ✅ Automatic Downshifting in Drive Mode
        else if (currentRPM <= carData.DownShiftRPM && currentGear > 2) // Use DownShiftRPM for downshifting
        {
            currentGear--;
            currentRPM *= 0.7f;
            lastShiftTime = Time.time;
            if(debugAutomaticGearShift)
            {
                Debug.Log($"[Automatic Shift] Shift Down: Gear {currentGear}");
                logData.AppendLine($"[Automatic Shift] Shift Down: Gear {currentGear}");
            }
        }
    }
    private float CalculateTorque()
    {
        float torque = 0f;
        
        if (isEngineRunning)
        {
            int safeGearIndex = Mathf.Clamp(currentGear, 0, carData.gearRatios.Length - 1);
            float gearRatio = carData.gearRatios[safeGearIndex];
            float wheelRPM = Mathf.Abs((wheelColliderRr.rpm + wheelColliderRl.rpm) / 2f) * gearRatio * carData.differentialRatio;


            // 🎯 Base Torque Calculation
            float baseTorque = carData.motorPower * gearRatio * carData.differentialRatio;

            // 🚀 Dynamic Acceleration Adjustment Based on Speed
            float speedEffect = Mathf.Clamp01(_rigidbody.linearVelocity.magnitude / 50f); // 🔥 Adjusted scale factor
            
            float accelerationMultiplier = Mathf.Lerp(
                carData.lowSpeedAccelerationMultiplier, 
                carData.highSpeedAccelerationMultiplier, 
                speedEffect
            );

            baseTorque *= accelerationMultiplier;  // ✅ Apply the multiplier correctly
            
            if (debugRPMandTorque)
            {
                // ✅ DEBUG LOGS - Check Values in Console
                Debug.Log($"Speed: {_rigidbody.linearVelocity.magnitude:F2}, SpeedEffect: {speedEffect:F2}");
                Debug.Log($"Low Multiplier: {carData.lowSpeedAccelerationMultiplier}, High Multiplier: {carData.highSpeedAccelerationMultiplier}");
                //Debug.Log($"Applied Acceleration Multiplier: {accelerationMultiplier:F2}");
                Debug.Log($"Base Torque After Multiplier: {baseTorque:F2}");
            
            }
            
            // 🚨 Prevent Over-Revving
            if (currentRPM >= carData.redline && currentGear > 1)
            {
                return -carData.engineBraking * 2.0f;
            }

            // 🏎️ Neutral Handling - Allow Free Revving
            if (currentGear == 1) // Neutral
            {
                // Let RPM rise without applying torque to the wheels
                float targetRPM = Mathf.Lerp(carData.idleRPM, carData.redline, gasInput);
                currentRPM = Mathf.Lerp(currentRPM, targetRPM, Time.deltaTime * 5f);
                return 0f; // No torque applied to wheels
            }

            // 📈 Rev Matching & Torque Clamping
            currentRPM = Mathf.Lerp(currentRPM, Mathf.Clamp(wheelRPM, carData.idleRPM, carData.redline), Time.deltaTime * 3f);
            torque = Mathf.Clamp(baseTorque * gasInput, -carData.motorPower * 2f, carData.motorPower * 2f); // ✅ Use gasInput to scale final torque
            if (debugRPMandTorque)
            {
                Debug.Log($"Final Torque Applied: {torque:F2}");
            }
        }

        return torque;
    }
//=============================================================================================
    void ApplyNaturalDeceleration()
    {
        if (Mathf.Abs(gasInput) < 0.01f && !isBraking && !isHandbraking) // If no gas and not braking
        {
            float decelerationForce = carData.rollingResistance * _rigidbody.linearVelocity.magnitude;
            _rigidbody.AddForce(-_rigidbody.linearVelocity.normalized * decelerationForce, ForceMode.Acceleration);

            float engineBrakingForce = carData.engineBraking * clutchValue * Mathf.Sign(_rigidbody.linearVelocity.z);
            if (Mathf.Abs(_rigidbody.linearVelocity.z) < 0.5f) engineBrakingForce = 0f; // Stop braking at low speed
            ApplyTorqueToWheels(-engineBrakingForce);


            // Additional safety to remove torque completely if speed is below a threshold
            if (_rigidbody.linearVelocity.magnitude < 0.5f)
            {
                ApplyTorqueToWheels(0f);
                _rigidbody.linearVelocity = Vector3.zero; // Stop completely
            }
        }

        if (debugRPMandTorque)
        {
            Debug.Log($"[Deceleration] Speed: {_rigidbody.linearVelocity.magnitude:F2} | Engine Braking: {carData.engineBraking:F2}");
            logData.AppendLine($"[Deceleration] Speed: {_rigidbody.linearVelocity.magnitude:F2} | Engine Braking: {carData.engineBraking:F2}");
        }

    }
//=============================================================================================
    private TrailRenderer CreateSkidTrail()
    {
        if (skidPrefab == null) return null;

        GameObject skidInstance = Instantiate(skidPrefab);
        TrailRenderer trail = skidInstance.GetComponent<TrailRenderer>();
        trail.emitting = false; // Start disabled
        return trail;
    }
    void HandleSkidMarks()
    {
        WheelCollider[] wheels = { wheelColliderFl, wheelColliderFr, wheelColliderRl, wheelColliderRr };

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            WheelHit hit;

            if (wheel.GetGroundHit(out hit))
            {
                float sidewaysSlip = Mathf.Abs(hit.sidewaysSlip);
                float forwardSlip = Mathf.Abs(hit.forwardSlip);
                bool isDrifting = sidewaysSlip > 0.25f;
                bool isBrakingHard = forwardSlip > 0.35f && isHandbraking;
                bool shouldSkid = isDrifting || isBrakingHard;

                if (shouldSkid)
                {
                    if (activeSkidInstances[i] == null && skidPrefab != null)
                    {
                        activeSkidInstances[i] = Instantiate(skidPrefab, hit.point + Vector3.up * 0.02f, Quaternion.identity);
                        TrailRenderer trail = activeSkidInstances[i].GetComponent<TrailRenderer>();
                        trail.emitting = true;
                    }

                    if (activeSkidInstances[i] != null)
                    {
                        activeSkidInstances[i].transform.position = hit.point + Vector3.up * 0.02f;

                        // Make sure emitting is ON
                        TrailRenderer trail = activeSkidInstances[i].GetComponent<TrailRenderer>();
                        if (!trail.emitting)
                            trail.emitting = true;
                    }
                }
                else
                {
                    if (activeSkidInstances[i] != null)
                    {
                        TrailRenderer trail = activeSkidInstances[i].GetComponent<TrailRenderer>();

                        // 🛑 Stop emitting, let it fade
                        trail.emitting = false;

                        // 💨 Destroy after fade time (based on trail.time)
                        Destroy(activeSkidInstances[i], trail.time);
                        activeSkidInstances[i] = null;
                    }
                }
            }
            else
            {
                if (activeSkidInstances[i] != null)
                {
                    TrailRenderer trail = activeSkidInstances[i].GetComponent<TrailRenderer>();
                    trail.emitting = false;
                    Destroy(activeSkidInstances[i], trail.time);
                    activeSkidInstances[i] = null;
                }
            }
        }
    }
    void HandleDirtParticles()
    {
        WheelCollider[] wheels = { wheelColliderFl, wheelColliderFr, wheelColliderRl, wheelColliderRr };

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            WheelHit hit;

            if (wheel.GetGroundHit(out hit))
            {
                int areaMask = GetNavMeshAreaAtPosition(hit.point);
                bool isOnDirt = (areaMask & (1 << 3)) != 0; // Area 3 = Dirt
                float speed = _rigidbody.linearVelocity.magnitude;

                if (isOnDirt && speed > 1f)
                {
                    // Create particle if not active
                    if (activeDirtParticles[i] == null && dirtParticlePrefab != null)
                    {
                        activeDirtParticles[i] = Instantiate(dirtParticlePrefab, hit.point + Vector3.up * 0.1f, Quaternion.identity, transform);
                        Vector3 backwardsAndUp = (-_rigidbody.linearVelocity.normalized + Vector3.up * 0.4f).normalized;
                        activeDirtParticles[i].transform.rotation = Quaternion.LookRotation(backwardsAndUp);


                    }

                    if (activeDirtParticles[i] != null)
                    {
                        activeDirtParticles[i].transform.position = hit.point + Vector3.up * 0.1f;
                        var ps = activeDirtParticles[i].GetComponent<ParticleSystem>();
                        var emission = ps.emission;
                        var main = ps.main;
                        var velocity = ps.velocityOverLifetime;

                        emission.enabled = true;

                        // 🔧 Dynamic intensity and lifetime
                        float speedFactor = Mathf.Clamp01(speed / 50f); // Normalize 0-1 up to 50 km/h

                        emission.rateOverTime = Mathf.Lerp(10f, 100f, speedFactor);
                        main.startLifetime = Mathf.Lerp(0.2f, 1.0f, speedFactor);
                        main.startSpeed = Mathf.Lerp(0.5f, 5.0f, speedFactor);
                        main.startSize = Mathf.Lerp(1f,10f, speedFactor);

                        velocity.enabled = true;
                        velocity.space = ParticleSystemSimulationSpace.World;

                        if (!ps.isPlaying)
                            ps.Play();
                    }
                }
                else
                {
                    if (activeDirtParticles[i] != null)
                    {
                        Destroy(activeDirtParticles[i], 1f);
                        activeDirtParticles[i] = null;
                    }
                }
            }
            else
            {
                if (activeDirtParticles[i] != null)
                {
                    Destroy(activeDirtParticles[i], 1f);
                    activeDirtParticles[i] = null;
                }
            }
        }
    }
    void HandleSmoketParticles()
    {
        WheelCollider[] wheels = { wheelColliderFl, wheelColliderFr, wheelColliderRl, wheelColliderRr };

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            WheelHit hit;

            if (wheel.GetGroundHit(out hit))
            {
                int areaMask = GetNavMeshAreaAtPosition(hit.point);
                float speed = _rigidbody.linearVelocity.magnitude;

                bool isDirtSurface = (areaMask & ((1 << 2) | (1 << 3) | (1 << 4))) != 0;
                float sidewaysSlip = Mathf.Abs(hit.sidewaysSlip);
                float forwardSlip = Mathf.Abs(hit.forwardSlip);
                bool isSliding = sidewaysSlip > 0.25f || forwardSlip > 0.35f;

                bool shouldEmit = false;

                if (isDirtSurface)
                {
                    shouldEmit = speed > 1f;
                    skidTimers[i] = 0f; // Reset timer on dirt
                }
                else if (isSliding)
                {
                    skidTimers[i] += Time.fixedDeltaTime;
                    if (skidTimers[i] >= skidDelayThreshold)
                        shouldEmit = true;
                }
                else
                {
                    skidTimers[i] = 0f; // Reset if not sliding
                }

                if (shouldEmit)
                {
                    if (activeSmokeParticles[i] == null && smokeParticlePrefab != null)
                    {
                        activeSmokeParticles[i] = Instantiate(smokeParticlePrefab, hit.point + Vector3.up * 0.1f, Quaternion.identity, transform);
                        Vector3 backwardsAndUp = (-_rigidbody.linearVelocity.normalized + Vector3.up * 0.4f).normalized;
                        activeSmokeParticles[i].transform.rotation = Quaternion.LookRotation(backwardsAndUp);

                        // ✅ Assign the correct material based on surface
                        var psRenderer = activeSmokeParticles[i].GetComponent<ParticleSystemRenderer>();
                        if (psRenderer != null)
                        {
                            psRenderer.material = GetSmokeMaterialByArea(areaMask);
                        }
                    }


                    if (activeSmokeParticles[i] != null)
                    {
                        activeSmokeParticles[i].transform.position = hit.point + Vector3.up * 0.1f;

                        var ps = activeSmokeParticles[i].GetComponent<ParticleSystem>();
                        var emission = ps.emission;
                        var main = ps.main;
                        var velocity = ps.velocityOverLifetime;

                        emission.enabled = true;

                        Color smokeColor = GetColorByNavMeshArea(areaMask);
                        main.startColor = smokeColor;

                        float speedFactor = Mathf.Clamp01(speed / 50f);
                        emission.rateOverTime = Mathf.Lerp(20f, 80f, speedFactor);
                        main.startLifetime = Mathf.Lerp(1.0f, 2.0f, speedFactor);
                        main.startSpeed = Mathf.Lerp(0.2f, 1.2f, speedFactor);
                        main.startSize = Mathf.Lerp(10f, 100f, speedFactor);
                        main.gravityModifier = 0.1f;

                        velocity.enabled = true;
                        velocity.space = ParticleSystemSimulationSpace.World;

                        if (!ps.isPlaying)
                            ps.Play();
                    }
                }
                else
                {
                    if (activeSmokeParticles[i] != null)
                    {
                        var ps = activeSmokeParticles[i].GetComponent<ParticleSystem>();
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        Destroy(activeSmokeParticles[i], 2f);
                        activeSmokeParticles[i] = null;
                    }
                }
            }
            else
            {
                skidTimers[i] = 0f;

                if (activeSmokeParticles[i] != null)
                {
                    var ps = activeSmokeParticles[i].GetComponent<ParticleSystem>();
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    Destroy(activeSmokeParticles[i], 2f);
                    activeSmokeParticles[i] = null;
                }
            }
        }
    }
    Material GetSmokeMaterialByArea(int areaMask)
    {
        if ((areaMask & (1 << 3)) != 0) return dirtSmokeMaterial;   // Dirt
        if ((areaMask & (1 << 4)) != 0) return mudSmokeMaterial;    // Mud
        if ((areaMask & (1 << 5)) != 0) return gravelSmokeMaterial; // Gravel
        return asphaltSmokeMaterial;                                // Default → Asphalt
    }
//=============================================================================================
    public void SetAIGas(float gas)
    {
        if (isAIControlled)
        {
            gasInput = Mathf.Clamp(gas, 0f, 1f);
        }
    }
    public void SetAISteering(float steer)
    {
        if (isAIControlled)
        {
            float adjustedSteer = Mathf.Clamp(steer, -1f, 1f) * carData.maxSteerAngle;
            HandleSteering(adjustedSteer);
        }
    }
    public void SetAIBrake(bool brake)
    {
        if (isAIControlled)
        {
            isBraking = brake;
        }
    }
//=============================================================================================
    /*void OnApplicationQuit()
    {
        // ✅ Append log data instead of overwriting
        File.AppendAllText(logFilePath, logData.ToString());
        Debug.Log($"Debug log saved to: {logFilePath}");
    }
    void DetectWheelColliderAnomalies()
    {
        WheelCollider[] wheels = { wheelColliderFl, wheelColliderFr, wheelColliderRl, wheelColliderRr };

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelHit hit;
            if (wheels[i].GetGroundHit(out hit))
            {
                float suspensionDistance = hit.force; // Get suspension compression force

                // Check for sudden suspension force changes (twitching detection)
                if (Mathf.Abs(suspensionDistance - lastSuspensionDistances[i]) > suspensionThreshold)
                {
                    string wheelName = i switch
                    {
                        0 => "Front Left",
                        1 => "Front Right",
                        2 => "Rear Left",
                        3 => "Rear Right",
                        _ => "Unknown"
                    };
                    if(debugTwitchDetection)
                    {
                        Debug.LogWarning($"[Twitching Detected] {wheelName} suspension changed too fast: Δ {Mathf.Abs(suspensionDistance - lastSuspensionDistances[i]):F4}");
                        logData.AppendLine($"[Twitching Detected] {wheelName} suspension change: Δ {Mathf.Abs(suspensionDistance - lastSuspensionDistances[i]):F4} at {Time.time:F2}s");
                    }
                }

                lastSuspensionDistances[i] = suspensionDistance;
            }
        }
    }
    void DetectCarJumping()
    {
        float verticalVelocity = (_rigidbody.position.y - lastCarPosition.y) / Time.fixedDeltaTime;

        // Check if car suddenly jumps upwards
        if (Mathf.Abs(verticalVelocity - lastCarYVelocity) > jumpThreshold)
        {
            if (debugJumpDetection)
            {
                Debug.LogWarning($"[Jump Detected] Car jumped too fast: Δ {Mathf.Abs(verticalVelocity - lastCarYVelocity):F4} m/s");
                logData.AppendLine($"[Jump Detected] Car jumped too fast: Δ {Mathf.Abs(verticalVelocity - lastCarYVelocity):F4} m/s at {Time.time:F2}s");
            }
        }

        lastCarYVelocity = verticalVelocity;
        lastCarPosition = _rigidbody.position;
    }
    void DebugGripChanges()
    {
        WheelHit hit;
        WheelCollider[] wheels = { wheelColliderFl, wheelColliderFr, wheelColliderRl, wheelColliderRr };
        string[] names = { "Front Left", "Front Right", "Rear Left", "Rear Right" };

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i].GetGroundHit(out hit))
            {
                float sidewaysSlip = hit.sidewaysSlip;
                float forwardSlip = hit.forwardSlip;

                if (Mathf.Abs(sidewaysSlip) > 0.25f || Mathf.Abs(forwardSlip) > 0.35f)
                {
                    if (!debugGripChange) return;
                    {
                        Debug.Log($"🛞 Grip Loss on {names[i]} | Side Slip: {sidewaysSlip:F2} | Forward Slip: {forwardSlip:F2}");
                        logData.AppendLine($"[Grip Loss] {names[i]} | Side Slip: {sidewaysSlip:F2}, Forward Slip: {forwardSlip:F2} @ {Time.time:F2}s");
                    }
                }
            }
        }
    }
    */
}