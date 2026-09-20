using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace Core
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleEnemyChat : MonoBehaviour
    {
        [Header("NPC Identity")]
        public string characterName = "The Silent Guard";
        public string characterRole = "Vow-Broken Guard";
        
        [Header("SLM Personality Traits")]
        [Range(0f, 1f)] public float openness = 0.3f; // Cynical
        [Range(0f, 1f)] public float neuroticism = 0.8f; // Paranoid
        [Range(0f, 1f)] public float aggression = 0.4f; // Grumpy

        [Header("Core Prompt")]
        [TextArea(5, 10)]
        public string systemPrompt = @"You are an ancient Skeleton Guard in the Dead Wind Cliffs. 
You took a vow of silence to contain Adar, but you have broken it to warn this foolish traveler. 
You are cynical, dry, and slightly cruel. 
Keep your responses short and ancient.";

        [Header("Interaction Settings")]
        public float interactionRadius = 8f;
        public int typingSpeedMs = 25;

        [Header("Lore System")]
        public TextAsset loreJson;

        private NPC npcData;
        private Player playerData;
        private CloudRunner cloudRunner;

        private bool playerInRange = false;
        private bool isChatting = false;
        private bool conversationEnded = false;

        private string chatHistory = "";
        private bool isWaitingForAI = false;
        private Vector2 scrollPosition = Vector2.zero;
        private Transform playerTransform;

        private Rigidbody rb;
        private LoreDatabase loreDb;
        private bool isLoreMode = false;
        private int currentLoreIndex = 0;
        private string tempKeyInput = "";
        private string savedApiKey = "";
        private string currentMessage = "";

        private string currentNodeId = "root";
        private bool isFreeTalking = false;
        private float lastMenuTransitionTime = 0f;
        private int lastMenuTransitionFrame = -1;

        [Header("Controller Support")]
        private int selectedIndex = 0;
        private float nextInputTime = 0f;
        private const float inputCooldown = 0.2f;

        public enum InteractionPhase { FirstMeeting, Known, PostDeath }
        private InteractionPhase currentPhase = InteractionPhase.FirstMeeting;

        private float lastManualScrollTime = 0f;
        private const float autoScrollDelay = 2.0f;
        private Rect lastSelectedRect;
        private float targetScrollY = -1f;
        private string keyError = "";
        private const string ENCRYPTION_SALT = "BONE_GUARD_VOW"; 

        void Start()
        {
            // 1. Rigidbody & Physics setup
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; 
                rb.useGravity = false;
            }

            // 2. Component Verification
            if (loreJson == null) 
            {
                Debug.LogError($"[SimpleEnemyChat] {characterName}: BossLore JSON is missing from the inspector!");
            }
            else
            {
                string jsonContent = loreJson.text.Trim();
                if (string.IsNullOrEmpty(jsonContent))
                {
                    Debug.LogError("[SimpleEnemyChat] BossLore JSON is empty!");
                }
                else
                {
                    // Handle potential UTF-8 BOM if present
                    if (jsonContent.Length > 0 && jsonContent[0] != '{')
                    {
                        int firstBrace = jsonContent.IndexOf('{');
                        if (firstBrace >= 0) jsonContent = jsonContent.Substring(firstBrace);
                    }

                    try {
                        loreDb = JsonUtility.FromJson<LoreDatabase>(jsonContent);
                        if (loreDb == null) 
                        {
                            Debug.LogError("[SimpleEnemyChat] JsonUtility returned null for BossLore.json. Structure mismatch?");
                        }
                        else if (loreDb.dialogue_tree == null || loreDb.dialogue_tree.Length == 0)
                        {
                            Debug.LogWarning("[SimpleEnemyChat] LoreDatabase loaded but 'dialogue_tree' is empty. Verify JSON structure.");
                        }
                        else
                        {
                            Debug.Log($"[SimpleEnemyChat] Successfully loaded {loreDb.dialogue_tree.Length} dialogue nodes and {loreDb.lore_snippets.Length} lore snippets.");
                        }
                    } catch (System.Exception e) {
                        Debug.LogError("[SimpleEnemyChat] Failed to parse BossLore.json: " + e.Message);
                    }
                }
            }

            // 3. Initialize SLM & AI Data
            npcData = new NPC { personName = characterName, role = characterRole };
            npcData.personality.Openness = openness;
            npcData.personality.Neuroticism = neuroticism;
            npcData.personality.Aggression = aggression;

            playerData = new Player { personName = "Traveler" };
            cloudRunner = new CloudRunner();

            // 4. Input & API Persistence (Encrypted)
            string rawSaved = PlayerPrefs.GetString("GrokAPIKey", "");
            savedApiKey = DecryptKey(rawSaved);
            nextInputTime = Time.time;

            // 5. Hook up the Player Transform
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        void Update()
        {
            if (playerTransform == null) return;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= interactionRadius)
            {
                if (!playerInRange)
                {
                    playerInRange = true;
                    isChatting = true;
                    TriggerGreeting();
                    
                    // Show cursor for UI interaction
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                if (isChatting)
                {
                    HandleControllerInput();
                }
            }
            else
            {
                if (playerInRange)
                {
                    playerInRange = false;
                    isChatting = false;
                    currentMessage = ""; 
                    chatHistory = ""; // RESET for next meeting
                    
                    // Return to gameplay
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private void HandleControllerInput()
        {
            if (!isChatting) return;

            float vertical = 0;
            
            // 1. D-Pad & Stick Reading
            if (Gamepad.current != null)
            {
                Vector2 dpad = Gamepad.current.dpad.ReadValue();
                if (Mathf.Abs(dpad.y) > 0.1f) vertical = dpad.y;
            }

            // 2. Keyboard Reading
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical = 1;
                else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical = -1;
            }

            if (Mathf.Abs(vertical) > 0.1f)
            {
                // 1. NAVIGATION (Selection moves first)
                if (Time.time >= nextInputTime)
                {
                    int prevIndex = selectedIndex;
                    selectedIndex -= (int)Mathf.Sign(vertical);
                    nextInputTime = Time.time + inputCooldown;
                    
                    // Reset target scroll to allow fresh follow
                    targetScrollY = -1f;
                }

                // 2. BOUNDARY SCROLLING (Only if at top/bottom of selection)
                bool isAtTop = (selectedIndex <= 0);
                bool isAtBottom = false; // We check this in DrawUI where we know count
                
                // If pressing UP at the top button, scroll the text window up
                if (isAtTop && vertical > 0.1f)
                {
                    scrollPosition.y -= vertical * 800f * Time.deltaTime;
                    targetScrollY = -1f; // Suppress auto-follow
                }
                
                lastManualScrollTime = Time.time;
            }
        }

        private void TriggerGreeting()
        {
            scrollPosition = Vector2.zero; 
            chatHistory = ""; // Start fresh
            
            int deaths = PlayerPrefs.GetInt("PlayerDeathCount", 0);
            int interactions = PlayerPrefs.GetInt("NPC_Interactions_" + characterName, 0);
            
            currentNodeId = "root";
            isFreeTalking = false;

            string greeting = "Halt. I am watching."; 

            if (loreDb != null && loreDb.greetings != null)
            {
                string[] options;
                if (deaths > 0) options = loreDb.greetings.post_death;
                else if (interactions > 0) options = loreDb.greetings.returning;
                else options = loreDb.greetings.first_meeting;

                if (options != null && options.Length > 0)
                    greeting = options[UnityEngine.Random.Range(0, options.Length)];
            }

            TypewriterText(greeting);
            
            PlayerPrefs.SetInt("NPC_Interactions_" + characterName, interactions + 1);
            PlayerPrefs.Save();
        }

        private async void TypewriterText(string text)
        {
            isWaitingForAI = true;
            if (!string.IsNullOrEmpty(chatHistory)) chatHistory += "\n\n";
            
            string cleanText = text;
            string[] prefixes = { "You:", "Traveler:", "Player:", "NPC:" };
            foreach(var p in prefixes) {
                if (cleanText.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))
                    cleanText = cleanText.Substring(p.Length).Trim();
            }

            chatHistory += "<b>" + characterName + "</b>: ";

            for (int i = 0; i < cleanText.Length; i++)
            {
                if (!isChatting) break;
                chatHistory += cleanText[i];
                await Task.Delay(typingSpeedMs);
            }
            isWaitingForAI = false;
        }

        public void StartFreeTalk()
        {
            isFreeTalking = true;
            lastMenuTransitionTime = Time.time;
            lastMenuTransitionFrame = Time.frameCount;
            selectedIndex = 0;
            TypewriterText("Ask what you will, Traveler. I may answer... or I may simply watch you wither.");
        }

        public void BackToTree()
        {
            isFreeTalking = false;
            lastMenuTransitionTime = Time.time;
            lastMenuTransitionFrame = Time.frameCount;
            selectedIndex = 0;
            currentNodeId = "root";
        }

        public void StartLoreMode()
        {
            isLoreMode = true;
            currentLoreIndex = 0;
            chatHistory = "<i>The NPC's eyes glaze over as they recall an ancient sorrow...</i>";
            AdvanceLoreMode();
        }

        private void AdvanceLoreMode()
        {
            if (loreDb == null || loreDb.lore_snippets == null || loreDb.lore_snippets.Length == 0) return;

            if (currentLoreIndex < loreDb.lore_snippets.Length)
            {
                string content = loreDb.lore_snippets[currentLoreIndex].content;
                if (loreDb.story_transitions != null && loreDb.story_transitions.Length > 0)
                {
                    string trans = loreDb.story_transitions[currentLoreIndex % loreDb.story_transitions.Length];
                    TypewriterText(trans + "\n" + content);
                }
                else
                {
                    TypewriterText(content);
                }
                currentLoreIndex++;
            }
            else
            {
                isLoreMode = false;
                chatHistory += "\n\n<i>The vision fades. The skeleton guard blinks slowly.</i>";
                TriggerGreeting();
            }
        }

        private void OnGUI()
        {
            if (!isChatting) return;

            // DYNAMIC ASPECT RATIO CALCULATION
            float aspectRatio = (float)Screen.width / Screen.height;
            float widthPercent = 0.75f;
            if (aspectRatio > 2.0f) widthPercent = 0.5f; // Ultrawide
            else if (aspectRatio < 1.2f) widthPercent = 0.9f; // Portrait/Square

            float w = Screen.width * widthPercent;
            float h = Screen.height * 0.85f;
            float x = (Screen.width - w) / 2;
            float y = Screen.height - h - 50;

            GUI.backgroundColor = new Color(0.01f, 0.01f, 0.01f, 0.98f);
            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);
            
            // Handle target scroll lerp
            if (targetScrollY >= 0)
                scrollPosition.y = Mathf.Lerp(scrollPosition.y, targetScrollY, Time.deltaTime * 10f);

            // GLOBAL SCROLL FOR EVERYTHING
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            // Get current scroll view rect for edge detection
            Rect scrollVisibleRect = new Rect(0, scrollPosition.y, w, h);

            GUILayout.BeginVertical();
            GUILayout.Space(15);
            
            // 1. History
            GUIStyle textWrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 34, richText = true };
            textWrapStyle.normal.textColor = Color.white;
            GUILayout.Label(chatHistory, textWrapStyle);
            
            GUILayout.Space(20);
            
            // 2. Interaction UI
            if (isLoreMode) DrawLoreModeUI();
            else if (isFreeTalking) DrawFreeTalkUI();
            else DrawTreeOptionsUI();
            
            GUILayout.Space(15);
            GUILayout.EndVertical();

            // EDGE DETECTION LOGIC (Repaint only)
            if (Event.current.type == EventType.Repaint)
            {
                // Only follow selection if we aren't manually scrolling at the top edge
                if (selectedIndex > 0 || (selectedIndex == 0 && targetScrollY != -1))
                {
                    if (lastSelectedRect.yMax > scrollPosition.y + h - 60) // Near bottom
                        targetScrollY = lastSelectedRect.yMax - h + 60;
                    else if (lastSelectedRect.yMin < scrollPosition.y + 20) // Near top
                        targetScrollY = lastSelectedRect.yMin - 20;
                }
            }

            GUILayout.EndScrollView();

            // FOOTER INSTRUCTIONS
            GUIStyle footerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            footerStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            GUILayout.Space(5);
            GUILayout.Label("D-pad: Scroll | Y/Triangle: Select", footerStyle);
            GUILayout.Space(5);

            GUILayout.EndArea();
        }

        private void DrawTreeOptionsUI()
        {
            // 1. Tree Navigation Options
            if (loreDb == null || loreDb.dialogue_tree == null || loreDb.dialogue_tree.Length == 0) 
            {
                GUILayout.Label("<color=red>Error: Lore Database not loaded.</color>");
                GUILayout.Label("Ensure 'BossLore.json' is assigned and correctly formatted.");
                DrawSelectableButton("Walk Away", 0, () => CloseChat());
                return;
            }

            DialogueNode node = null;
            foreach (var n in loreDb.dialogue_tree) { if (n.id == currentNodeId) { node = n; break; } }
            if (node == null) node = loreDb.dialogue_tree[0];

            int baseOpts = node.options.Length;
            bool canGoBack = currentNodeId != "root";
            int totalOpts = baseOpts + 3; // + Lore, + AI, + Back to Start
            
            selectedIndex = Mathf.Clamp(selectedIndex, 0, totalOpts - 1);

            for (int i = 0; i < baseOpts; i++)
            {
                var opt = node.options[i];
                DrawSelectableButton("<b>></b> " + opt.text, i, () => SelectNode(opt.next));
            }

            DrawSelectableButton("<i>[Listen to the Mythos (Story Mode)]</i>", baseOpts, () => StartLoreMode());
            DrawSelectableButton("<i>[Ask your own question (AI)]</i>", baseOpts + 1, () => StartFreeTalk());
            
            if (canGoBack)
                DrawSelectableButton("<i>[Return to the start]</i>", baseOpts + 2, () => { currentNodeId = "root"; TriggerGreeting(); lastMenuTransitionTime = Time.time; lastMenuTransitionFrame = Time.frameCount; });
            else
                DrawSelectableButton("Walk Away", baseOpts + 2, () => CloseChat());
        }

        private void DrawFreeTalkUI()
        {
            if (Time.time < lastMenuTransitionTime + 0.2f) return; // Debounce transition
            if (string.IsNullOrEmpty(savedApiKey))
            {
                DrawAPIKeyPrompt();
                return;
            }

            GUILayout.Label("<b>DYNAMIC INTERACTION</b>");
            GUILayout.Label("<i>Choose how you wish to address the ancient skeleton:</i>");
            GUILayout.Space(10);

            string[] dynamicOptions = {
                "Mock his brittle bones.",
                "Question his ancient vow.",
                "Demand to know the truth about Adar.",
                "Challenge the old guard's sanity.",
                "Ask about the other guards.",
                "Boast of your own strength."
            };

            int totalOpts = dynamicOptions.Length + 1;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, totalOpts - 1);

            for (int i = 0; i < dynamicOptions.Length; i++)
            {
                int idx = i;
                DrawSelectableButton("<b>[Action]</b> " + dynamicOptions[i], i, () => {
                    SendMessageToCloud(dynamicOptions[idx]);
                });
            }

            DrawSelectableButton("<i>[Return to the Tree]</i>", dynamicOptions.Length, () => { isFreeTalking = false; selectedIndex = 0; });
        }

        private void SelectNode(string nextId)
        {
            if (string.IsNullOrEmpty(nextId)) return;
            DialogueNode nextNode = null;
            foreach (var n in loreDb.dialogue_tree) { if (n.id == nextId) { nextNode = n; break; } }

            if (nextNode != null)
            {
                string playerChoice = GetOptionText(nextId);
                currentNodeId = nextId;
                chatHistory += "\n\n<i>Traveler: " + playerChoice + "</i>";
                TypewriterText(nextNode.text);
                selectedIndex = 0;
                scrollPosition.y = float.MaxValue;
            }
        }

        private string GetOptionText(string nextId)
        {
            if (loreDb == null || loreDb.dialogue_tree == null) return "...";
            foreach(var node in loreDb.dialogue_tree)
            {
                foreach(var opt in node.options) { if (opt.next == nextId) return opt.text; }
            }
            return "Tell me more.";
        }

        private void DrawSelectableButton(string label, int index, System.Action onClick)
        {
            Color oldColor = GUI.backgroundColor;
            bool isSelected = (selectedIndex == index);
            if (isSelected) GUI.backgroundColor = new Color(1f, 0.9f, 0f, 1f);

            if (GUILayout.Button(label, GUILayout.Height(75))) 
            {
                if (!isWaitingForAI) onClick?.Invoke();
            }

            // CAPTURE RECT FOR SCROLLING
            if (isSelected && Event.current.type == EventType.Repaint)
            {
                lastSelectedRect = GUILayoutUtility.GetLastRect();
            }

            if (isSelected && Time.time > lastMenuTransitionTime + 0.3f && Time.frameCount > lastMenuTransitionFrame) // Prevent click-through
            {
                if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) 
                {
                    if (!isWaitingForAI) onClick?.Invoke();
                }
                if (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame) 
                {
                    if (!isWaitingForAI) onClick?.Invoke();
                }
            }
            GUI.backgroundColor = oldColor;
        }

        private void DrawLoreModeUI()
        {
            GUILayout.Space(10);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, 1);

            if (!isWaitingForAI && !conversationEnded) DrawSelectableButton("Continue Listening...", 0, () => AdvanceLoreMode());
            DrawSelectableButton("Walk Away", 1, () => { isLoreMode = false; CloseChat(); });
        }

        private void CloseChat() { isChatting = false; chatHistory = ""; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        private void DrawAPIKeyPrompt()
        {
            GUILayout.Label("<b><color=red>[ ! ]</color> COGNITIVE LINK SEVERED</b>");
            GUILayout.Label("<i>\"My mind... it requires a 'Grok Key' to reach the higher planes...\"</i>");
            GUILayout.Space(10);
            
            GUILayout.Label("<color=grey>Provide your own API Key (Stored securely in PlayerPrefs):</color>");
            
            // Mask the key for privacy
            tempKeyInput = GUILayout.PasswordField(tempKeyInput, '*', GUILayout.Height(30));
            
            if (!string.IsNullOrEmpty(keyError))
                GUILayout.Label($"<color=orange>{keyError}</color>");

            selectedIndex = Mathf.Clamp(selectedIndex, 0, 1);
            DrawSelectableButton("Restore Connection (Save Key)", 0, () => {
                if (IsValidKey(tempKeyInput)) 
                {
                    string encrypted = EncryptKey(tempKeyInput);
                    PlayerPrefs.SetString("GrokAPIKey", encrypted);
                    PlayerPrefs.Save();
                    
                    savedApiKey = tempKeyInput;
                    tempKeyInput = ""; 
                    keyError = "";
                }
            });
            DrawSelectableButton("Back to Ancestral Lore", 1, () => { isFreeTalking = false; selectedIndex = 0; keyError = ""; });
        }

        private bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key)) { keyError = "The key cannot be empty, Traveler."; return false; }
            if (key.Length < 20) { keyError = "This sigil is too short to hold a soul."; return false; }
            if (key.Contains(" ")) { keyError = "Accursed whitespace has corrupted the key!"; return false; }
            return true;
        }

        private string EncryptKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string result = "";
            for (int i = 0; i < key.Length; i++)
            {
                result += (char)(key[i] ^ ENCRYPTION_SALT[i % ENCRYPTION_SALT.Length]);
            }
            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(result));
        }

        private string DecryptKey(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            try {
                byte[] decodedBytes = System.Convert.FromBase64String(encrypted);
                string decoded = System.Text.Encoding.UTF8.GetString(decodedBytes);
                string result = "";
                for (int i = 0; i < decoded.Length; i++)
                {
                    result += (char)(decoded[i] ^ ENCRYPTION_SALT[i % ENCRYPTION_SALT.Length]);
                }
                return result;
            } catch { return ""; }
        }

        private async void SendMessageToCloud(string messageToSend)
        {
            chatHistory += "\n\nYou: " + messageToSend;
            if (playerData != null) playerData.sayings.Add(messageToSend); // Sync with AI logic
            isWaitingForAI = true;

            // 1. LOCAL LORE LOOKUP (Primary Focus)
            string lowerMsg = messageToSend.ToLower();
            string localResponse = "";
            if (loreDb != null)
            {
                // Check Lore Snippets
                foreach (var snippet in loreDb.lore_snippets)
                {
                    foreach (var key in snippet.keywords) 
                    { 
                        if (lowerMsg.Contains(key.ToLower())) { localResponse = snippet.content; break; } 
                    }
                    if (!string.IsNullOrEmpty(localResponse)) break;
                }

                // Check Dialogue Tree Nodes (Deep Search)
                if (string.IsNullOrEmpty(localResponse) && loreDb.dialogue_tree != null)
                {
                    foreach (var node in loreDb.dialogue_tree)
                    {
                        if (node.text.ToLower().Contains(lowerMsg)) { localResponse = node.text; break; }
                        foreach (var opt in node.options)
                        {
                            if (lowerMsg.Contains(opt.text.ToLower())) { localResponse = node.text; break; }
                        }
                        if (!string.IsNullOrEmpty(localResponse)) break;
                    }
                }

                int deaths = PlayerPrefs.GetInt("PlayerDeathCount", 0);
                if (string.IsNullOrEmpty(localResponse) && deaths > 0 && UnityEngine.Random.value > 0.4f)
                {
                    if (loreDb.mocking_snippets != null && loreDb.mocking_snippets.Length > 0)
                        localResponse = loreDb.mocking_snippets[UnityEngine.Random.Range(0, loreDb.mocking_snippets.Length)];
                }
            }

            if (!string.IsNullOrEmpty(localResponse)) 
            { 
                await Task.Delay(UnityEngine.Random.Range(300, 600)); 
                isWaitingForAI = false; // RELEASE LOCK
                TypewriterText(localResponse); 
                return; 
            }

            // 2. API FALLBACK
            try 
            {
                string specializedPrompt = $@"You are {characterName}, an ancient human skeleton guard lingering in the Dead Wind Cliffs.
You are weary, cynical, and possess a dry, biting wit. You find the player's presence a nuisance.
Lore Context: You serve Adar, the bird-god of silence. You despise the 'Living' who disturb the quiet.
History: The player has died {PlayerPrefs.GetInt("PlayerDeathCount", 0)} times. Reference this with mocking pity.
Tone: Archaic, condescending, and extremely brief. 
Constraint: Never exceed 2 short sentences. Do not offer help. Be a wall of bone and regret.";

                string finalSystemPrompt = $"{specializedPrompt}\n\nRecent Chat:\n{chatHistory}";
                
                string reply = await cloudRunner.GenerateResponseAsync(finalSystemPrompt, npcData, playerData);
                reply = reply.Replace("\"", "").Trim();

                // ROBUST PREFIX STRIPPING
                string[] redundantPrefixes = { characterName + ":", "You:", "Traveler:", "Silent Guard:" };
                foreach (var prefix in redundantPrefixes)
                {
                    if (reply.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        reply = reply.Substring(prefix.IndexOf(':') + 1).Trim();
                    }
                }
                
                if (UnityEngine.Random.value > 0.8f) reply += "\n\n*His jaw bone clicks as he sighs.*";
                
                TypewriterText(reply);
            }
            finally 
            {
                isWaitingForAI = false;
            }
        }
    }

    [System.Serializable] public class GreetingData { public string[] first_meeting, returning, post_death; }
    [System.Serializable] public class LoreSnippet { public string id; public string[] keywords; public string content; }
    [System.Serializable] public class OptionData { public string text; public string next; }
    [System.Serializable] public class DialogueNode { public string id; public string text; public OptionData[] options; }
    [System.Serializable] public class LoreDatabase { 
        public GreetingData greetings; 
        public string[] mocking_snippets, story_transitions; 
        public DialogueNode[] dialogue_tree; 
        public LoreSnippet[] lore_snippets; 
    }
}
