using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates streaming with partial messages and different message types.
/// </summary>
public class StreamingExample : IExample
{
    public string Name => "Streaming with Partial Messages";
    public string Description => "Shows how to handle streaming with partial content updates";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates streaming with partial message updates.");
        Console.WriteLine("You'll see the response appear character by character.\n");

        var options = new ClaudeAgentOptions
        {
            // Enable partial messages to see incremental updates
            IncludePartialMessages = true,

            // Limit response for demo purposes
            MaxTurns = 1
        };

        await using var client = new ClaudeAgentClient(options);

        var prompt = "Write a haiku about programming.";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Streaming Response:");
        Console.WriteLine("-------------------");

        var lastContentLength = 0;

        await foreach (var message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case SystemMessage { IsInit: true } system:
                    Console.WriteLine($"[Session initialized - Model: {system.Model}]");
                    break;

                case AssistantMessage assistant:
                    // For partial messages, print only the new content
                    foreach (var block in assistant.MessageContent.Content)
                    {
                        if (block is TextBlock text)
                        {
                            // Print new characters since last update
                            if (text.Text.Length > lastContentLength)
                            {
                                Console.Write(text.Text[lastContentLength..]);
                                lastContentLength = text.Text.Length;
                            }
                        }
                    }

                    break;

                case ResultMessage result:
                    Console.WriteLine("\n\n[Completed]");
                    Console.WriteLine($"  Total cost: ${result.TotalCostUsd:F4}");
                    Console.WriteLine($"  Duration: {result.DurationMs}ms");
                    Console.WriteLine($"  Turns: {result.NumTurns}");
                    break;
            }
        }
    }
}
