using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates the simplest use case: a one-shot query with streaming response.
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
        await using ClaudeAgentClient client = new();

        // Send a simple query and stream the response
        string prompt = "What is the capital of France? Please respond in one sentence.";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Response:");

        await foreach (Message message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    // Print assistant's text content
                    foreach (ContentBlock block in assistant.MessageContent.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Console.Write(text.Text);
                        }
                    }

                    break;

                case ResultMessage result:
                    string context = result.Usage is not null
                        ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k"
                        : "?";
                    Console.WriteLine($"\n\n[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {context}]");
                    Console.WriteLine($"[Session ID: {result.SessionId}]");
                    break;
            }
        }
    }
}
