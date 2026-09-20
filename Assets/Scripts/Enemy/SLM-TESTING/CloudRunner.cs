using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // for JsonUtility

public class CloudRunner
{
    private const string API_URL = "https://api.groq.com/openai/v1/chat/completions";

    private static readonly HttpClient client = new HttpClient();

    public CloudRunner()
    {
    }

    [Serializable]
    private class GroqMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class GroqPayload
    {
        public string model;
        public GroqMessage[] messages;
        public float temperature;
        public int max_tokens;
    }

    // Basic structure to parse Groq's response using JsonUtility
    [Serializable]
    private class GroqResponse
    {
        public GroqChoice[] choices;
    }

    [Serializable]
    private class GroqChoice
    {
        public GroqMessage message;
    }

    public async Task<string> GenerateResponseAsync(string systemPrompt, NPC npc, Player player)
    {
        // Fetch API key securely from PlayerPrefs
        string rawKey = Core.GrokAPIKeyManager.GetAPIKey();
        if (string.IsNullOrEmpty(rawKey))
        {
            Debug.LogError("[CloudRunner] Grok API Key is missing! Please enter it via the BYOK UI.");
            return "...";
        }
        
        string authHeader = rawKey.StartsWith("Bearer ") ? rawKey : "Bearer " + rawKey;

        // 1. Get the last thing the player said
        string lastSaying = player.sayings.Count > 0 ? player.sayings[player.sayings.Count - 1] : "...";

        // 2. Create Payload (OpenAI Format) using Unity's JsonUtility compatible classes
        GroqPayload payload = new GroqPayload
        {
            model = "llama-3.1-8b-instant",
            messages = new GroqMessage[]
            {
                new GroqMessage { role = "system", content = systemPrompt },
                new GroqMessage { role = "user", content = $"[Player Action]: {lastSaying}\n\nReact to this action or statement in character." }
            },
            temperature = 0.7f,
            max_tokens = 150
        };

        string jsonPayload = JsonUtility.ToJson(payload);
        
        try
        {
            // 3. Send Request using HttpRequestMessage for thread-safety (not modifying static client defaults)
            using (var request = new HttpRequestMessage(HttpMethod.Post, API_URL))
            {
                request.Headers.Add("Authorization", authHeader);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    if (error.Contains("invalid_api_key"))
                    {
                        Debug.LogError($"[CloudRunner] Unauthorized: Invalid API Key. Note: You are calling the GROQ API (LPU), not GROK (xAI). Ensure you are using a key from console.groq.com");
                    }
                    else
                    {
                        Debug.LogError($"[CloudRunner] {response.StatusCode}: {error}");
                    }
                    return "[Brain Error]";
                }

                // 4. Parse Response
                string jsonResponse = await response.Content.ReadAsStringAsync();
                
                // Unity's JsonUtility can parse the base response
                GroqResponse parsedResponse = JsonUtility.FromJson<GroqResponse>(jsonResponse);

                if (parsedResponse != null && parsedResponse.choices != null && parsedResponse.choices.Length > 0)
                {
                    string reply = parsedResponse.choices[0].message.content;
                    return CleanResponse(reply ?? "");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CloudRunner Exception] {ex.Message}");
        }

        return "...";
    }

    private string CleanResponse(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Trim();
    }
}






