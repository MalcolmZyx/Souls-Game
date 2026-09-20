using UnityEngine;
using System;
using System.Collections;


// Players Stats, can level up
public class PlayerAttributes : MonoBehaviour
{
    public int constitution = 10;
    public int endurance = 10;
    public int strength = 10;
    public event Action OnAttributesChanged;

    // temp stats values to track currently changed values during level up before finalizing
    public int tempCON;
    public int tempSTR;
    public int tempEND;

    public int GetCON() => constitution + tempCON;
    public int GetSTR() => strength + tempSTR;
    public int GetEND() => endurance + tempEND;
    
    
    void Awake()
    {
        ResetTempStats();
    }

    public void ResetTempStats()
    {
        tempCON = 0;
        tempEND = 0;
        tempSTR = 0;

        OnAttributesChanged?.Invoke();
    }


    // allows other scripts to access this, thanks AI for debugging
    public void NotifyAttributesChanged()
    {
        OnAttributesChanged?.Invoke();
    }
}