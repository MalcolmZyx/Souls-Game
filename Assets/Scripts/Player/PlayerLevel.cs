using System;
using System.Collections;
using NUnit.Framework.Internal.Commands;
using UnityEngine;
public class PlayerLevel : MonoBehaviour
{
    [SerializeField] private PlayerAttributes playerAttributes;
    [SerializeField] private PlayerInventory playerInventory;
    private int currentLevel;
    private int cost;

    void Start()
    {
        currentLevel = 0;
        cost = 100;
    }

    public void FreeFrags()
    {
        playerInventory.fragments += 10000;
        playerAttributes.NotifyAttributesChanged();
    }

    // getter to access the current value of the next upgrade based on how many have been made
    public int GetUpgradeCost(int baseLvl, int increaseAmt)
    {
        int totalCost = 0;

        for (int i = 0; i < increaseAmt; i++)
        {
            int level = baseLvl + i;
            int cost = Mathf.RoundToInt(10f * Mathf.Pow(1.15f, level));
            totalCost += cost;
        }

        return totalCost;
    }


    // calculates the actual cost of all chosen upgrades together using formula determined in GetUpgradeCost above
    public int GetTotalPendingCost()
    {
        int cost = 0;

        cost += GetUpgradeCost(playerAttributes.constitution, playerAttributes.tempCON);
        cost += GetUpgradeCost(playerAttributes.strength, playerAttributes.tempSTR);
        cost += GetUpgradeCost(playerAttributes.endurance, playerAttributes.tempEND);

        return cost;
    }


    // code for increase button on each stat, no upper limit implemented here
    public void IncreaseStat(string stat)
    {
        switch (stat)
        {
            case "CON":
                playerAttributes.tempCON++;
                break;
            
            case "STR":
                playerAttributes.tempSTR++;
                break;

            case "END":
                playerAttributes.tempEND++;
                break;
        }

        playerAttributes.NotifyAttributesChanged();
    }


    // code for decrease button on each stat, should only decrease back to previous value or default, whichever was higher
    public void DecreaseStat(string stat)
    {
        switch (stat)
        {
            case "CON":
                if (playerAttributes.tempCON > 0)
                    playerAttributes.tempCON--;
                break;
            
            case "STR":
                if (playerAttributes.tempSTR > 0)
                    playerAttributes.tempSTR--;
                break;

            case "END":
                if (playerAttributes.tempEND > 0)
                    playerAttributes.tempEND--;
                break;
        }

        playerAttributes.NotifyAttributesChanged();
    }

    // confirm button to save new stats, checks if player has enough fragments before changing the stats
    public void ConfirmStats()
    {
        int totalCost = GetTotalPendingCost();

        if (playerInventory.fragments < totalCost) return;

        playerInventory.fragments -= totalCost;

        playerAttributes.constitution += playerAttributes.tempCON;
        playerAttributes.strength += playerAttributes.tempSTR;
        playerAttributes.endurance += playerAttributes.tempEND;

        playerAttributes.ResetTempStats();
        playerAttributes.NotifyAttributesChanged();
    }


    // cancel button, to exit without changing stats
    public void CancelStats()
    {
        playerAttributes.ResetTempStats();
    }

}