using System;
using System.Collections.Generic;
using System.Linq;

// ------------------------------------------------------------
// SYSTEM 1: INVENTORY & ECONOMY
// ------------------------------------------------------------
public class InventorySystem2 
{
    public float currentGold;
    public float maxCarryWeight = 50f;
    public List<Item> contents = new List<Item>();
    
    // "Item?" means this can be null (empty slot)
    public Item? equippedWeapon;
    public Item? equippedOutfit; 

    // --- METHODS ---

    // Wrapper to make your Test script work
    public void AddItem(Item newItem)
    {
        TryAddItem(newItem);
    }

    public bool TryAddItem(Item newItem)
    {
        if (GetTotalWeight() + newItem.weight > maxCarryWeight)
        {
            Console.WriteLine($"[Inventory] Too heavy! Cannot pick up {newItem.name}.");
            return false;
        }
        contents.Add(newItem);
        return true;
    }

    // Restored this method for your Test script
    public bool HasItem(string id)
    {
        return contents.Exists(x => x.itemID == id);
    }

    public void DropItem(string itemID)
    {
        Item? item = contents.Find(x => x.itemID == itemID);
        if (item != null)
        {
            contents.Remove(item);
            if (equippedWeapon == item) equippedWeapon = null;
            if (equippedOutfit == item) equippedOutfit = null;
            Console.WriteLine($"[Inventory] Dropped {item.name}.");
        }
    }

    public float GetTotalWeight()
    {
        return contents.Sum(x => x.weight);
    }

    public bool TransferItem(string itemID, InventorySystem2 targetInventory, float price = 0)
    {
        Item? item = contents.Find(x => x.itemID == itemID);
        if (item == null) return false;

        if (targetInventory.currentGold < price)
        {
            Console.WriteLine("[Trade] Buyer cannot afford this.");
            return false;
        }

        if (!targetInventory.TryAddItem(item)) return false;

        this.contents.Remove(item);
        this.currentGold += price;
        targetInventory.currentGold -= price;
        
        Console.WriteLine($"[Trade] Sold {item.name} for {price} gold.");
        return true;
    }
    
    // Helper for your test script to equip items easily
    public void Equip(Item item)
    {
        if (item.category == ItemCategory.Weapon) equippedWeapon = item;
        if (item.category == ItemCategory.Apparel) equippedOutfit = item;
    }
}

public class Item 
{
    // Initialized to empty strings to stop CS8618 Warnings
    public string itemID { get; set; } = "";
    public string name { get; set; } = "";
    public ItemCategory category; 
    public float weight;
    public float value;
    public int level;          
    public List<string> tags = new List<string>(); 

    public Item() { } // Empty constructor

    public Item(string id, string n, ItemCategory cat, float w, float v)
    {
        itemID = id; name = n; category = cat; weight = w; value = v;
    }
}

public enum ItemCategory { General, Weapon, Apparel, Consumable, KeyItem }

// ------------------------------------------------------------
// SYSTEM 2: SOCIAL MATRIX 
// ------------------------------------------------------------
public class SocialMatrix 
{
    public float globalReputation; 
    public List<RelationshipEntry> knownPeople = new List<RelationshipEntry>();

    public RelationshipStatus GetStatus(string personID)
    {
        var rel = GetRelationship(personID);
        if (rel == null) return RelationshipStatus.Stranger;

        if (rel.fear > 0.8f) return RelationshipStatus.Terrified;
        if (rel.affinity < -0.5f) return RelationshipStatus.Enemy;
        if (rel.affinity > 0.5f && rel.trust > 0.5f) return RelationshipStatus.Friend;
        if (rel.trust < 0.2f) return RelationshipStatus.Untrusted;

        return RelationshipStatus.Neutral;
    }

    public void ModifyOpinion(string personID, float trustDelta, float fearDelta, float affinityDelta = 0)
    {
        var rel = GetRelationship(personID);
        if (rel == null)
        {
            // Fix warning by initializing
            rel = new RelationshipEntry { personID = personID, lastInteractionDate = DateTime.Now.ToString() };
            knownPeople.Add(rel);
        }

        rel.trust = Math.Clamp(rel.trust + trustDelta, 0f, 1f);
        rel.fear = Math.Clamp(rel.fear + fearDelta, 0f, 1f);
        rel.affinity = Math.Clamp(rel.affinity + affinityDelta, -1f, 1f);
        rel.lastInteractionDate = DateTime.Now.ToString();
    }

    public RelationshipEntry? GetRelationship(string personID) 
    {
        return knownPeople.Find(x => x.personID == personID);
    }
}

public enum RelationshipStatus { Stranger, Friend, Enemy, Terrified, Untrusted, Neutral }

public class RelationshipEntry 
{
    public string personID { get; set; } = "";
    public float affinity; 
    public float trust;    
    public float fear;     
    public string lastInteractionDate { get; set; } = "";
}

// ------------------------------------------------------------
// SYSTEM 3: GOAL ENGINE
// ------------------------------------------------------------
public class GoalEngine 
{
    public Goal? currentGoal; // Nullable
    public List<Goal> goalQueue = new List<Goal>(); 

    public void AddGoal(string desc, int priority)
    {
        goalQueue.Add(new Goal { description = desc, priority = priority });
        goalQueue = goalQueue.OrderByDescending(g => g.priority).ToList();
        EvaluateGoals();
    }

    // Wrapper for Testing.cs (if needed)
    public void SetGoal(string desc, int priority)
    {
        AddGoal(desc, priority);
    }

    public void CompleteCurrentGoal()
    {
        if (currentGoal != null)
        {
            Console.WriteLine($"[Goals] Completed: {currentGoal.description}");
            currentGoal = null;
        }
        EvaluateGoals();
    }

    public void EvaluateGoals()
    {
        if (goalQueue.Count > 0)
        {
            var bestGoal = goalQueue[0];
            if (currentGoal == null || bestGoal.priority > currentGoal.priority)
            {
                currentGoal = bestGoal;
                goalQueue.RemoveAt(0);
                Console.WriteLine($"[Goals] New Objective: {currentGoal.description}");
            }
        }
    }
}

public class Goal 
{
    public string description { get; set; } = ""; 
    public int priority;       
    public bool isComplete;
}

// ------------------------------------------------------------
// SYSTEM 4: MEMORY & PERCEPTION
// ------------------------------------------------------------
public class MemoryStream 
{
    public List<Memory> shortTermBuffer = new List<Memory>();
    public List<Memory> longTermArchive = new List<Memory>();

    public void AddObservation(string who, string what, string where)
    {
        shortTermBuffer.Add(new Memory { 
            Who = who, What = what, Where = where, When = DateTime.Now.Ticks 
        });
    }

    public void CommitToLongTerm(Memory mem)
    {
        if (mem.Importance >= 7) longTermArchive.Add(mem);
    }
    
    public bool HasMemoryOf(string keyword)
    {
        if (shortTermBuffer.Exists(m => m.What.Contains(keyword))) return true;
        if (longTermArchive.Exists(m => m.What.Contains(keyword))) return true;
        return false;
    }

    public void ProcessDecay()
    {
        long currentTime = DateTime.Now.Ticks;
        long decayThreshold = 10000000; 

        shortTermBuffer.RemoveAll(m => (currentTime - m.When) > decayThreshold);
        
        for (int i = longTermArchive.Count - 1; i >= 0; i--)
        {
            longTermArchive[i].Importance--; 
            if (longTermArchive[i].Importance <= 0)
            {
                longTermArchive.RemoveAt(i);
            }
        }
    }

    // --- GOSSIP LOGIC ---
    // Returns TRUE if the gossip was believed and accepted
    public bool ReceiveGossip(Memory incomingMemory, float trustLevel)
    {
        // 1. Check Trust Filter
        // If we don't trust the source (0.5), we ignore the gossip.
        if (trustLevel < 0.5f) 
        {
            Console.WriteLine($"[Gossip] Rejected. Trust ({trustLevel}) is too low.");
            return false;
        }

        // 2. Check Redundancy
        // If we already know this, don't duplicate it.
        if (HasMemoryOf(incomingMemory.What)) 
        {
            Console.WriteLine("[Gossip] Rejected. Already known.");
            return false;
        }

        // 3. Accept Memory (Mark as Hearsay)
        Memory hearsay = new Memory
        {
            Who = incomingMemory.Who,
            What = incomingMemory.What,
            Where = incomingMemory.Where,
            When = incomingMemory.When,
            Why = incomingMemory.Why,
            Importance = incomingMemory.Importance - 1, // Gossip is slightly less impactful than seeing it
            isFact = false // It is NOT a verified fact
        };

        shortTermBuffer.Add(hearsay);
        Console.WriteLine($"[Gossip] Accepted: '{hearsay.What}' (Trust: {trustLevel})");
        return true;
    }
}

public class Memory 
{
    public string ID { get; set; } = Guid.NewGuid().ToString();
    public string Who { get; set; } = "";
    public string What { get; set; } = "";
    public string Where { get; set; } = "";
    public long When;           
    public string Why { get; set; } = "";
    public int Importance; 
    public bool isFact;         
}

// ------------------------------------------------------------
// SYSTEM 5: PERSONALITY
// ------------------------------------------------------------
public class PersonalityCore 
{
    public float Openness;
    public float Conscientiousness;
    public float Extraversion;
    public float Agreeableness;
    public float Neuroticism; 
    public float Aggression; 
    public float Gullibility; 
    
    public Mood currentMood = new Mood();
}

public class Mood {
    public string name { get; set; } = "Neutral";
    public float intensity; 
}

// ------------------------------------------------------------
// SYSTEM 6: PERCEPTION
// ------------------------------------------------------------
public class PerceptionLayer 
{
    public string currentActionState { get; set; } = "Idle";
    public List<string> visualTags = new List<string>(); 
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// using System.Collections.Generic;

// [System.Serializable]
// public class Inventory { // this is important for NPCs and Players (more important for players)
//     public List<string> itemNames = new List<string>(); // Added list for visibility testing
//     // this class will contain information about each item a given person has
//     // so, itemName will be the first thing listed. Then following that: 
//     // itemLevel
//     // itemCategory (weapon, potion, dance move, etc..)
//     // itemCost or itemValue
//     // any other attribute recommendations will be greatly appreciated (since prototype will aim to be similar to GTA 6, what does GTA 5 include for this?)
//     // 
//     // for the methods, some could be: adding/removing items and maybe basic getters/setters
//     // I don't know if I should include any other methods here, since I would like to separate out concerns. 
//     // Methods like displaying the inventory or performing an item action should be performed by a different class/script? 
// }

// [System.Serializable]
// public class Relationships { // this is important for both NPCs and Players. 
//     public float trustLevel;
//     // Similarly to the Inventory class, the Relationships will include the id of the person that they have a "relationship" with,
//     // the trustLevel feature (as seen above), numConvos, and then what else would you suggest? [I'm drawing a blank rn] 
//     // Methods: 
//     // newRelationship() -> has had more than a threshold amount of numConvos. 
//     // again, what else would you suggest? 
//     // this class will also contain the reputation score that one person has of another // will be used for global reputation
// }

// [System.Serializable]
// public class Events { // this is important for the AI for memory of past events.  
//     public List<string> knownEvents = new List<string>();
//     // I think I want this to be the parent class, this will contain a list of events. Events will include: 
//     // id of event, name of event,
//     // What happened, where it happened, when it happened, who was apart of it, and why it happened. 
//     // and how they found out.
//     // However, some of these attributes may be empty for a given event, and they will be filled in by others. 
//     // then there will be a child class called knownEvents and another called trueEvents (these will be what the NPC observed & if its true - not what they remember if that makes sense). 
//     // I'm not sure what each will include that would be unique to them, I suppose maybe not a child/adult class then, 
//     // rather just multiple declarations of them? 
// }

// [System.Serializable]
// public class Attire { // this class will be associated with the inventory class and also be input for the NPC AI
//     public string clothingType; 
//     // This class will be simple, and might be an item going into the inventory class actually. We also might want to create a general item class instead of having inventory handling that probably? 
//     // but regardless, we will have the attireName, attireType, attireColor, attireValue, etc.. 
// }

// [System.Serializable]
// public class Action_state { // serves as some visual inputs for the NPC AI 
//     public string stateName; // e.g., "Walking" 
//     // this will contain stateName, stateCategory (maybe? Like if its an aggressive state or default/regular state)
//     // this will communicate with the players tags most likely
// }

// [System.Serializable]
// public class Reputation { // will be used for individual relationships, and for the global reputation of someone for the player to be aware of
//     public int score;
//     // I think this can just contain the score and the method for calculating it.
//     // maybe there should also be a function to calculate the global reputation of a player, given the score assigned to them in the relationship class
// }

// [System.Serializable]
// public class PersonalityProfile { // this will be for distinguishing the NPC's personality when going into the SLM (context window)
//     public float aggression;
//     public float openness;
//     // make sure to include the big 5 for personalities as well
//     // this may adjust over time based on impactful events or relationships (level-10s)
// }

// [System.Serializable]
// public class ConvoMem { // this will be vital for the NPC to handle remembering conversations
//     public string lastTopic;
//     // this might include converting all binary mappings into something digestible for the SLM's context window. 
//     // also includes the 5Ws and any additional information for remembering the conversation that just occured 
// }

// [System.Serializable]
// public class Observations { // this will communicate with the tags of the person or whoever/whatever they're able to observe
//     public string lastSeenObject;
//     // this will include basic binary variables of what they have seen, and based on a basic algorithm, it will decide if they feel a certain way about that conversation
//     // and that feeling might make them want to spread logic, confront that person, hide from that person, etc.. 
//     // their observation also might adjust based on their personality and feelings toward that thing/person
// }

// [System.Serializable]
// public class Facts { // this is the ground truth, what actually happened -- not affected by the bias NPC
//     public List<string> trueFacts = new List<string>();
//     // this will also contain a basic algroithm or tag selection of what actually happened, basically what the NPC is observing - their bias
// }

// [System.Serializable]
// public class NPCGoal { // this will be for the NPC and what their ultimate goal is
//     public string goalDescription;
//     // there will be multiple goals and have a journey of goals based on each. 
//     // each one will have a weight of importance that NPC and may adjust based on the player's actions, etc.. 
// }

// [System.Serializable]
// public class Mood { // this is for the NPC's AI SLM context window as well & for the end mood of a given conversation
//     public string currentMoodName;
//     // the mood will be based on the NPC's personality and their sentiment score based on their last conversation. 
//     // however it will decrease as time goes on and based on what they're doing 
// }
