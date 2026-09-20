using System;

public class NPC : Person 
{
    // The "Brain" Components
    public PersonalityCore personality;
    public MemoryStream memory; 
    public GoalEngine goals;    

    public NPC() : base() // IMPORTANT: Calls Person() to set up inventory/social
    {
        // Initialize AI Systems
        personality = new PersonalityCore();
        memory = new MemoryStream();
        goals = new GoalEngine();

        // Default Config (to prevent null errors)
        personality.Openness = 0.5f;
        personality.Neuroticism = 0.5f;
        personality.Aggression = 0.0f;
    }
}