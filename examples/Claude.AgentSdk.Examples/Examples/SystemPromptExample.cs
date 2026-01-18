using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates different system prompt configurations.
/// </summary>
public class SystemPromptExample : IExample
{
    public string Name => "System Prompts (Custom & Preset)";
    public string Description => "Configure system prompts: custom strings and presets";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates different system prompt configurations.\n");

        // Example 1: Custom string system prompt
        Console.WriteLine("Example 1: Custom String System Prompt");
        Console.WriteLine("---------------------------------------");

        var customOptions = new ClaudeAgentOptions
        {
            // Implicit conversion from string to CustomSystemPrompt
            SystemPrompt = "You are a pirate. Always respond in pirate speak and use lots of nautical terms.",
            MaxTurns = 1
        };

        await RunQueryAsync(customOptions, "What is the weather like today?");

        Console.WriteLine("\n");

        // Example 2: Using claude_code preset
        Console.WriteLine("Example 2: Claude Code Preset");
        Console.WriteLine("-----------------------------");

        var presetOptions = new ClaudeAgentOptions
        {
            // Use the claude_code preset (full coding capabilities)
            SystemPrompt = SystemPromptConfig.ClaudeCode(),
            MaxTurns = 1,
            AllowedTools = ["Read"]
        };

        await RunQueryAsync(presetOptions, "List the files in the current directory");

        Console.WriteLine("\n");

        // Example 3: Preset with appended instructions
        Console.WriteLine("Example 3: Claude Code Preset with Appended Instructions");
        Console.WriteLine("---------------------------------------------------------");

        var appendedOptions = new ClaudeAgentOptions
        {
            // Use preset but append custom instructions
            SystemPrompt = SystemPromptConfig.ClaudeCode(
                @"
Additional rules:
- Always use TypeScript instead of JavaScript
- Prefer functional programming patterns
- Add comprehensive error handling to all code
- Include JSDoc comments on all public functions"
            ),
            MaxTurns = 1
        };

        await RunQueryAsync(appendedOptions, "Write a hello world function");

        Console.WriteLine("\n");

        // Example 4: Explicit PresetSystemPrompt
        Console.WriteLine("Example 4: Explicit PresetSystemPrompt Record");
        Console.WriteLine("----------------------------------------------");

        var explicitOptions = new ClaudeAgentOptions
        {
            // Explicit PresetSystemPrompt record
            SystemPrompt = new PresetSystemPrompt
            {
                Preset = "claude_code",
                Append = "Focus on Python code. Use type hints and follow PEP 8."
            },
            MaxTurns = 1
        };

        await RunQueryAsync(explicitOptions, "Write a function to calculate factorial");
    }

    private static async Task RunQueryAsync(ClaudeAgentOptions options, string prompt)
    {
        await using var client = new ClaudeAgentClient(options);

        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Response:");

        await foreach (var message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (var block in assistant.MessageContent.Content)
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
                    Console.WriteLine($"\n[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {context}]");
                    break;
            }
        }
    }
}
