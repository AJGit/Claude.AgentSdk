using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
/// Demonstrates the simplest use case: a one-shot query with streaming response.
/// </summary>
public class BasicQueryExample : IExample
{
    public string Name => "Basic Query";
    public string Description => "Simple one-shot query with streaming response";

    public async Task RunAsync()
    {
        Console.WriteLine("This example shows the most basic usage of the Claude Agent SDK.");
        Console.WriteLine("We'll send a simple prompt and stream the response.\n");

        // Create client with default options
        await using var client = new ClaudeAgentClient();

        // Send a simple query and stream the response
        var prompt = "What is the capital of France? Please respond in one sentence.";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Response:");

        await foreach (var message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    // Print assistant's text content
                    foreach (var block in assistant.MessageContent.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Console.Write(text.Text);
                        }
                    }
                    break;

                case ResultMessage result:
                    Console.WriteLine($"\n\n[Query completed - Cost: ${result.TotalCostUsd:F4}]");
                    Console.WriteLine($"[Session ID: {result.SessionId}]");
                    break;
            }
        }
    }
}
