using System.Collections.Generic;
using UnityEngine;

public class WayPointContainerScript : MonoBehaviour
{
    public List<Transform> waypoints;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        foreach(Transform tr in gameObject.GetComponentsInChildren<Transform>())
        {
            waypoints.Add(tr);
        }
        waypoints.RemoveAt(0);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
