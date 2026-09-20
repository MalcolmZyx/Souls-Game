using System;
using System.Collections.Generic;

public abstract class Person 
{
    // Initialize defaults to avoid "Non-nullable field" warnings
    public string id { get; set; } = Guid.NewGuid().ToString();
    public string personName { get; set; } = "Unknown";
    public int age;
    public string role { get; set; } = "Civilian"; 

    public InventorySystem2 inventory;
    public SocialMatrix social;
    public PerceptionLayer perception; 

    public string alignment { get; set; } = "Neutral"; 
    public List<string> achievements; 
    public Dictionary<string, int> skills; 
    public List<string> sayings; 

    public Person() 
    {
        inventory = new InventorySystem2();
        social = new SocialMatrix();
        perception = new PerceptionLayer();
        
        achievements = new List<string>();
        skills = new Dictionary<string, int>();
        sayings = new List<string>();
    }
}