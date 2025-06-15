using UnityEngine;

public class BrakingPointsContainerScript : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the AI's "BrakeTrigger" entered
        if (other.gameObject.tag == "car")
        {
            Debug.Log($"🚗 AI Car Entered Brake Zone: {other.gameObject.name}");

            AICarController car = other.GetComponentInParent<AICarController>();
            if (car != null)
            {
                Debug.Log("✅ AI Car DETECTED inside brake zone!");
                car.IsInsideBrakeZone = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "car")
        {
            Debug.Log($"🚗 AI Car Exited Brake Zone: {other.gameObject.name}");

            AICarController car = other.GetComponentInParent<AICarController>();
            if (car != null)
            {
                Debug.Log("✅ AI Car EXITED brake zone!");
                car.IsInsideBrakeZone = false;
            }
        }
    }
}
