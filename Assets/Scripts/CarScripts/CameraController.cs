using UnityEngine;

public class ArtOfRallyCamera : MonoBehaviour
{
    public Transform target;
    public float followDistance = 7f;
    public float height = 3.5f;
    public float rotationDamping = 2f;
    public float positionDamping = 3f;

    public float lookAheadFactor = 3f;
    public float speedInfluence = 0.1f;
    public float maxSpeedDistance = 10f;

    private Rigidbody carRigidbody;
    private Vector3 velocity = Vector3.zero;

    void Update()
    {
        // Dynamically assign the carRigidbody if it’s not set and we have a target
        if (target != null && carRigidbody == null)
        {
            carRigidbody = target.GetComponent<Rigidbody>();
        }
    }

    void FixedUpdate()
    {
        if (target == null || carRigidbody == null) return;

        float speed = carRigidbody.linearVelocity.magnitude;
        float dynamicDistance = followDistance + Mathf.Clamp(speed * speedInfluence, 0, maxSpeedDistance);

        Vector3 desiredPosition = target.position - target.forward * dynamicDistance + Vector3.up * height;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, 1f / positionDamping);

        Vector3 lookAhead = target.position + target.forward * lookAheadFactor;
        Quaternion desiredRotation = Quaternion.LookRotation(lookAhead - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationDamping);
    }
}
