using System;
using System.Text;
using System.Linq; 

public class SLMInputSystem 
{
    private const int MAX_SHORT_TERM_MEMORIES = 3;
    private const int MAX_LONG_TERM_MEMORIES = 2;

    public static string GenerateSystemPrompt(NPC npc, Player player)
    {
        StringBuilder sb = new StringBuilder();

        // IDENTITY & PERSONALITY
        string openness = npc.personality.Openness > 0.7f ? "Curious" : "Stubborn";
        string neuroticism = npc.personality.Neuroticism > 0.6f ? "Anxious" : "Calm";
        string aggression = npc.personality.Aggression > 0.5f ? "Hostile" : "Peaceful";

        sb.Append($"You are {npc.personName}, a {npc.role}. ");
        sb.Append($"Traits: {openness}, {neuroticism}, {aggression}. ");
        if (npc.goals.currentGoal != null)
        {
            sb.Append($"Current Goal: {npc.goals.currentGoal.description}. ");
        }

        // CONTEXTUAL AWARENESS
        var relation = npc.social.GetRelationship(player.personName);
        if (relation != null)
        {
            if (relation.trust < 0.3f) sb.Append("[CTX: DISTRUST] ");
            if (relation.fear > 0.6f)  sb.Append("[CTX: FEAR] ");
            if (relation.affinity > 0.7f) sb.Append("[CTX: FRIEND] ");
        }
        else
        {
            sb.Append("[CTX: STRANGER] ");
        }

        if (player.inventory.equippedWeapon != null)
        {
             sb.Append($"[CTX: PLAYER_ARMED_WITH_{player.inventory.equippedWeapon.name.ToUpper()}] ");
        }

        // MEMORY
        sb.Append(" MEMORY: ");
        
        var recentObs = npc.memory.shortTermBuffer
            .OrderByDescending(m => m.When)
            .Take(MAX_SHORT_TERM_MEMORIES);

        foreach (var mem in recentObs)
        {
            sb.Append($"Saw {mem.Who} {mem.What}. ");
        }

        return sb.ToString();
    }
}





