using System;
using System.Collections.Generic;

public class Player : Person 
{
    // Player-Specific Meta Data
    public float total_time_played_game;
    public string date_joined_game;
    public string type_of_player { get; set; } = "Casual"; 

    public Player() : base() // IMPORTANT: Calls Person() to set up inventory/social
    {
        date_joined_game = DateTime.Now.ToString("MM/dd/yyyy");
        this.inventory.currentGold = 100f; 
    }
}