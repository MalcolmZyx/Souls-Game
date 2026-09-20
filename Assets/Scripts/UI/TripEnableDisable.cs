using UnityEngine;
using System;
using Unity.VisualScripting;
using UnityEngine.Events;

public class TripEnableDisable : MonoBehaviour //shows/hides an object depending on input from zonetripper
{

    [SerializeField] ZoneTripper zoneTripper;
    [SerializeField] BossDamageable bD;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        zoneTripper.ZoneTripped.AddListener(ShowObject);
        zoneTripper.ZoneTripped.AddListener(HideObject);
        bD.OnBossDeath += HideObject;
    }

    public void ShowObject()
    {
        Debug.Log("trying to show object!");
        this.gameObject.SetActive(true);
    }

    public void HideObject()
    {
        Debug.Log("trying to hide object!");
        this.gameObject.SetActive(false);
    }
}
