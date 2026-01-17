using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates bidirectional/interactive session mode for multi-turn conversations.
/// </summary>
public class InteractiveSessionExample : IExample
{
    public string Name => "Interactive Session (Bidirectional Mode)";
    public string Description => "Multi-turn conversation with bidirectional communication";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates an interactive multi-turn conversation.");
        Console.WriteLine("The client creates a session for bidirectional communication.");
        Console.WriteLine("Type 'quit' to end the session.\n");

        ClaudeAgentOptions options = new()
        {
            SystemPrompt = "You are a helpful assistant. Keep responses concise (2-3 sentences max).",
            MaxTurns = 3 // Limit turns per message for demo
        };

        await using ClaudeAgentClient client = new(options);

        // Create a session for bidirectional mode
        Console.WriteLine("Creating session with Claude...");
        await using ClaudeAgentSession session = await client.CreateSessionAsync();
        Console.WriteLine("Connected! Starting conversation...\n");

        // Start receiving messages in background
        Task receiveTask = Task.Run(async () =>
        {
            try
            {
                await foreach (Message message in session.ReceiveAsync())
                {
                    switch (message)
                    {
                        case AssistantMessage assistant:
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.Write("Claude: ");
                            Console.ResetColor();

                            foreach (ContentBlock block in assistant.MessageContent.Content)
                            {
                                if (block is TextBlock text)
                                {
                                    Console.WriteLine(text.Text);
                                }
                            }

                            Console.WriteLine();
                            break;

                        case SystemMessage system:
                            if (system.IsInit)
                            {
                                Console.WriteLine($"[System: Session ready, model={system.Model}]");
                            }

                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal termination
            }
        });

        // Interactive loop
        string[] prompts =
        [
            "Hello! What's your name?",
            "What's 2 + 2?",
            "quit"
        ];

        Console.WriteLine("(Demo mode: sending pre-defined messages)\n");

        foreach (string prompt in prompts)
        {
            if (prompt.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[Ending session...]");
                break;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You: ");
            Console.ResetColor();
            Console.WriteLine(prompt);

            await session.SendAsync(prompt);

            // Wait a bit for response
            await Task.Delay(3000);
        }

        Console.WriteLine("\n[Session ended]");
    }
}
