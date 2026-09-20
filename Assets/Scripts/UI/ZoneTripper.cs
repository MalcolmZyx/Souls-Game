using UnityEngine;
using System;
using UnityEngine.Events;

public class ZoneTripper : MonoBehaviour //detects if a player is in a sphere collider zone and triggers an event for it
{

    [SerializeField] SphereCollider sphereCollider;
    public UnityEvent ZoneTripped = new UnityEvent();
    public UnityEvent ZoneLeft = new UnityEvent();

    void Awake()
    {
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            ZoneTripped?.Invoke();
            Debug.Log("Zone tripped!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            ZoneLeft?.Invoke();
            Debug.Log("Zone left!");
        }
    }
}
