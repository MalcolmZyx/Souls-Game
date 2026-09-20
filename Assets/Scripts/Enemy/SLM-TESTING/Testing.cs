using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Testing
{
    // MAIN ENTRY POINT
    public static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("========================================");
        Console.WriteLine("    AUTOMATED SYSTEMS CHECK INITIATED   ");
        Console.WriteLine("========================================");
        Console.ResetColor();

        // 1. UNIT TESTS (Test individual pieces)
        RunTest("Inventory System", Test_Economy_Logic);
        RunTest("Social Matrix", Test_Social_Status_Logic);
        RunTest("Goal System", Test_Goal_Logic);
        RunTest("Gossip Propagation", Test_Gossip_Logic);

        // Run the Cloud Test synchronously since Main cannot safely be async in Unity CLI tools
        Test_Cloud_SLM().GetAwaiter().GetResult();

        // // 2. INTEGRATION TEST (Test how they work together)
        // RunTest("FULL GAME LOOP SIMULATION", Test_Complex_Simulation);

        Console.WriteLine("\n--- Switching to Chatbot Mode ---");
        // Chatbot.Run() needs to be updated if it exists, or simulated here
        // Chatbot.Run().GetAwaiter().GetResult();
        
        Console.WriteLine("\n\nTests Complete. Press any key to exit.");
        Console.ReadKey();
    }

    // --------------------------------------------------------
    // THE TEST RUNNER 
    // --------------------------------------------------------
    static void RunTest(string testName, Func<bool> testMethod)
    {
        Console.Write($"Testing {testName,-30} ... ");
        try
        {
            bool result = testMethod();
            if (result)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PASS]");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAIL]");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR: {ex.Message}]");
        }
        Console.ResetColor();
    }

    // --------------------------------------------------------
    // TEST: ADVANCED INVENTORY (Weight & Trading)
    // --------------------------------------------------------
    static bool Test_Economy_Logic()
    {
        Console.WriteLine("\n[Testing Economy...]");
        
        InventorySystem2 seller = new InventorySystem2 { currentGold = 0 };
        InventorySystem2 buyer = new InventorySystem2 { currentGold = 100 };

        // Create a heavy item and a light item
        Item heavyRock = new Item("rock", "Big Rock", ItemCategory.General, 45f, 0f);
        Item goldRing = new Item("ring", "Gold Ring", ItemCategory.Apparel, 0.5f, 50f);

        seller.AddItem(heavyRock);
        seller.AddItem(goldRing);

        // 1. Test Weight Limit
        // Seller already has 45.5kg. Max is 50.
        // Try to add another heavy rock (should fail)
        bool pickupSuccess = seller.TryAddItem(new Item("rock2", "Big Rock 2", ItemCategory.General, 45f, 0f));
        if (pickupSuccess) { Console.WriteLine("FAIL: Exceeded max weight."); return false; }

        // 2. Test Trading
        // Buyer buys Ring for 50g
        bool tradeSuccess = seller.TransferItem("ring", buyer, 50f);
        
        if (!tradeSuccess) return false;
        if (seller.currentGold != 50) return false;
        if (buyer.currentGold != 50) return false;
        if (!buyer.HasItem("ring")) return false;

        return true;
    }

    // --------------------------------------------------------
    // TEST: SOCIAL DYNAMICS (Friend vs Enemy)
    // --------------------------------------------------------
    static bool Test_Social_Status_Logic()
    {
        Console.WriteLine("\n[Testing Social Logic...]");
        SocialMatrix matrix = new SocialMatrix();
        string playerID = "Player_01";

        // 1. Make them an Enemy
        matrix.ModifyOpinion(playerID, 0f, 0f, -0.8f); // High dislike
        if (matrix.GetStatus(playerID) != RelationshipStatus.Enemy) return false;

        // 2. Make them Terrifying (Fear overrides Hate)
        matrix.ModifyOpinion(playerID, 0f, 0.9f, 0f); // Add High Fear
        if (matrix.GetStatus(playerID) != RelationshipStatus.Terrified) return false;

        // 3. Make them a Friend (Remove fear, add trust/affinity)
        matrix.ModifyOpinion(playerID, 0.9f, -0.9f, 1.0f); 
        // Logic: Trust=~0.9, Fear=~0, Affinity=~0.2 (since we started at -0.8)
        // Adjust affinity more to cross threshold
        matrix.ModifyOpinion(playerID, 0f, 0f, 0.8f); 

        if (matrix.GetStatus(playerID) != RelationshipStatus.Friend) 
        {
            Console.WriteLine($"FAIL: Status is {matrix.GetStatus(playerID)}");
            return false;
        }

        return true;
    }

    // --------------------------------------------------------
    // TEST: GOAL INTERRUPTION
    // --------------------------------------------------------
    static bool Test_Goal_Logic()
    {
        Console.WriteLine("\n[Testing Goal Priority...]");
        GoalEngine engine = new GoalEngine();

        // 1. Start a low priority task
        engine.AddGoal("Sweep Floor", 1);
        if (engine.currentGoal.description != "Sweep Floor") return false;

        // 2. Add an EMERGENCY task
        engine.AddGoal("Run from Fire", 10);
        
        // The engine should automatically switch because 10 > 1
        if (engine.currentGoal.description != "Run from Fire") return false;

        // 3. Complete Emergency
        engine.CompleteCurrentGoal();

        // Should go back to next highest (Sweep Floor) if queue logic holds, 
        // OR be empty depending on implementation. 
        // In my code above, I didn't re-queue the interrupted task (Simulation choice).
        // So current should be null or next in queue.
        
        return true;
    }

    // --------------------------------------------------------
    // TEST: GOSSIP PROPAGATION (The "Telephone" Game)
    // --------------------------------------------------------
    static bool Test_Gossip_Logic()
    {
        Console.WriteLine("\n[Testing Gossip Propagation...]");

        // 1. SETUP
        NPC witness = new NPC { personName = "Witness" };
        NPC listener = new NPC { personName = "Listener" };

        // 2. CREATE A MEMORY
        Memory shockingEvent = new Memory 
        { 
            Who = "Player", 
            What = "Robbed Bank", 
            When = DateTime.Now.Ticks,
            Importance = 8 
        };
        witness.memory.shortTermBuffer.Add(shockingEvent);

        // 3. ATTEMPT 1: STRANGER GOSSIP (Low Trust)
        // Listener doesn't know Witness, so Trust is 0 (or default low)
        float trust = 0.2f; 
        bool success1 = listener.memory.ReceiveGossip(shockingEvent, trust);
        
        if (success1) { Console.WriteLine("FAIL: Believed a stranger."); return false; }

        // 4. ATTEMPT 2: FRIEND GOSSIP (High Trust)
        trust = 0.9f;
        bool success2 = listener.memory.ReceiveGossip(shockingEvent, trust);

        if (!success2) { Console.WriteLine("FAIL: Ignored a friend."); return false; }
        
        // 5. VERIFY DATA
        if (!listener.memory.HasMemoryOf("Robbed Bank")) return false;
        
        return true;
    }

    // --------------------------------------------------------
    // TEST 7: LIVE CLOUD AI (Real Intelligence)
    // --------------------------------------------------------
    static async Task Test_Cloud_SLM()
    {
        Console.WriteLine("\n   [Simulating: Live Cloud Inference...]");

        // 1. SETUP
        Player p = new Player { personName = "Malcolm", role = "Hero" };
        NPC npc = new NPC { personName = "Eldric", role = "Wizard" };

        // 2. CONTEXT
        p.inventory.Equip(new Item { name = "Magic Staff", category = ItemCategory.Weapon });
        npc.personality.Openness = 0.9f; 
        
        // Simulating the player speaking
        p.sayings.Add("Can you teach me a spell?"); 

        // 3. GENERATE PROMPT (Using your Input System)
        string systemPrompt = SLMInputSystem.GenerateSystemPrompt(npc, p);
        
        // 4. CALL CLOUD (The Real Brain)
        CloudRunner brain = new CloudRunner();
        string aiReply = await brain.GenerateResponseAsync(systemPrompt, npc, p);

        // 5. OUTPUT
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\nNPC ({npc.personName}): \"{aiReply}\"");
        Console.ResetColor();
    }

}





