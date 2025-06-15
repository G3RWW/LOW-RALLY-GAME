using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CameraMover : MonoBehaviour
{
    [System.Serializable]
    public class CameraPoint
    {
        public string pointName;
        public Transform cameraPosition;
        public Transform targetPosition;
        public Button uiButton;
    }

    public Transform cameraTransform;
    public Transform targetTransform;
    public float moveSpeed = 3f;

    public CameraPoint basePoint; // Default on launch
    public CameraPoint[] points;

    void Start()
    {
        if (basePoint != null)
        {
            cameraTransform.position = basePoint.cameraPosition.position;
            targetTransform.position = basePoint.targetPosition.position;
            cameraTransform.rotation = Quaternion.LookRotation(targetTransform.position - cameraTransform.position);

            // 🧠 Make sure basePoint button triggers movement too
            if (basePoint.uiButton != null)
                basePoint.uiButton.onClick.AddListener(() => MoveToPoint(basePoint));
        }

        foreach (CameraPoint cp in points)
        {
            if (cp.uiButton != null)
                cp.uiButton.onClick.AddListener(() => MoveToPoint(cp));
        }
    }



    void MoveToPoint(CameraPoint point)
    {
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(point));
    }

    IEnumerator MoveRoutine(CameraPoint point)
    {
        Vector3 startCamPos = cameraTransform.position;
        Vector3 startTargetPos = targetTransform.position;
        Vector3 endCamPos = point.cameraPosition.position;
        Vector3 endTargetPos = point.targetPosition.position;

        Quaternion startRotation = cameraTransform.rotation;
        Quaternion endRotation = Quaternion.LookRotation(endTargetPos - endCamPos);

        float elapsed = 0f;
        float duration = Vector3.Distance(startCamPos, endCamPos) / moveSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            cameraTransform.position = Vector3.Lerp(startCamPos, endCamPos, t);
            targetTransform.position = Vector3.Lerp(startTargetPos, endTargetPos, t);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        cameraTransform.position = endCamPos;
        targetTransform.position = endTargetPos;
        cameraTransform.rotation = endRotation;
    }
}
