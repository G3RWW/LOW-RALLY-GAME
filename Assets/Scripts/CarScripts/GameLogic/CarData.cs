using UnityEngine;

[CreateAssetMenu(fileName = "NewCarData", menuName = "Car/CarData")]
public class CarData : ScriptableObject
{
    [Header("Car Specifications")]
    public float maxSteerAngle = 30f;
    public float motorPower = 500f;
    public float brakeTorque = 1000f;
    public float handbrakeTorque = 3000f;
    public float rollingResistance = 0.1f; // Adjust based on vehicle weight
    public float engineBraking = 50f; // Higher value = stronger engine braking effect
    [Header("RPM & Gearing")]
    public float idleRPM = 900f;
    public float UpShiftRPM = 6000f;
    public float DownShiftRPM = 2500f;
    public float differentialRatio = 3.5f; // Example default value
    public float redline = 7000f;
    public float finalDriveRatio = 4.1f;
    public AnimationCurve hpToRPMCurve;

    // :white_check_mark: Added Reverse Gear + Neutral + Forward Gears
    public float[] gearRatios = { -3.5f, 0.0f, 3.5f, 2.1f, 1.4f, 1.0f, 0.8f, 0.7f };

    [Header("Friction Settings")]
    public float forwardFrictionStiffness = 1.2f;
    public float sidewaysFrictionStiffness = 0.8f;

    [Header("Drivetrain Settings")]
    public DrivetrainType drivetrain = DrivetrainType.RWD;

    [Header("Advanced Steering & Handling")]
    public AnimationCurve steeringSensitivityCurve = new AnimationCurve(
    new Keyframe(0, 1.5f),   // More sensitive at standstill
    new Keyframe(20, 1.3f),  // Responsive at low speeds
    new Keyframe(60, 1.1f),  // Balanced mid-speed
    new Keyframe(100, 0.8f), // Reduced sensitivity at high speed
    new Keyframe(150, 0.6f),
    new Keyframe(200, 0.5f)
    );

    public float driftSteerAssist = 0.5f; // :rocket: Adds small counter-steering when drifting
    public float gripReductionFactor = 0.6f; // :wheel: Reduces grip dynamically based on steering input

    [Header("Acceleration Control")]
    public float lowSpeedAccelerationMultiplier = 1.5f; // :red_car: Boosts torque at low speeds
    public float highSpeedAccelerationMultiplier = 0.7f; // :chart_with_downwards_trend: Reduces power at high speeds for stability
}

public enum DrivetrainType
{
    FWD, // Front-Wheel Drive
    RWD, // Rear-Wheel Drive
    AWD  // All-Wheel Drive
}

