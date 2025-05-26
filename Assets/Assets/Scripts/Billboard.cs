using UnityEngine;

public class Billboard : MonoBehaviour
{
    void Update()
    {
        if (Camera.main != null)
        {
            Vector3 targetPosition = Camera.main.transform.position;
            targetPosition.y = transform.position.y; // Optional: lock Y so it doesn't tilt
            transform.LookAt(targetPosition);
        }
    }
}