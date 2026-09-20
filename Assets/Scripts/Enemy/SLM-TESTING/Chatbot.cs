using System;
using System.Threading;
using System.Threading.Tasks;

public class Chatbot
{
    private static CancellationTokenSource? _typingCts = null;

    public static async Task Run()
    {
        NPC npc = new NPC { personName = "Eldric", role = "Wizard" };
        npc.personality.Openness = 0.9f;

        Player player = new Player { personName = "Hero" };
        CloudRunner cloud = new CloudRunner();

        // Keep conversation history for memory
        var history = new System.Collections.Generic.List<(string role, string content)>();

        Console.WriteLine("=== Chat with Eldric (type 'quit' to exit, press ENTER while typing to interrupt) ===\n");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("You: ");
            Console.ResetColor();

            string input = Console.ReadLine() ?? "";

            if (input.ToLower() == "quit") break;
            if (string.IsNullOrWhiteSpace(input)) continue;

            // Cancel any ongoing typing
            _typingCts?.Cancel();

            player.sayings.Add(input);
            history.Add(("user", input));

            string systemPrompt = SLMInputSystem.GenerateSystemPrompt(npc, player)
                + " IMPORTANT: Keep your response to 1-2 sentences maximum. Be concise and in-character.";

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[Groq AI] Thinking... ");
            Console.ResetColor();

            string response = await cloud.GenerateResponseAsync(systemPrompt, npc, player);
            history.Add(("assistant", response));

            // Clear the "Thinking..." line
            Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"Eldric: ");
            Console.ResetColor();

            // Typewriter effect with cancellation
            _typingCts = new CancellationTokenSource();
            await TypewriterEffect(response, _typingCts.Token);

            Console.WriteLine("\n");
        }
    }

    private static async Task TypewriterEffect(string text, CancellationToken token)
    {
        foreach (char c in text)
        {
            if (token.IsCancellationRequested)
            {
                // Print remaining text instantly if interrupted
                Console.WriteLine(text.Substring(text.IndexOf(c)));
                return;
            }

            Console.Write(c);

            // Vary speed slightly for natural feel
            int delay = c == ',' || c == '.' || c == '!' || c == '?' ? 80 : 18;

            try
            {
                await Task.Delay(delay, token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine(text.Substring(text.IndexOf(c) + 1));
                return;
            }
        }
    }
}