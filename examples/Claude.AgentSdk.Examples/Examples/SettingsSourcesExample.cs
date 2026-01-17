using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates loading settings from CLAUDE.md files.
/// </summary>
public class SettingsSourcesExample : IExample
{
    public string Name => "Settings Sources (CLAUDE.md)";
    public string Description => "Load project and user settings from CLAUDE.md files";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates loading settings from CLAUDE.md files.\n");
        Console.WriteLine("CLAUDE.md files can contain project-specific instructions that are");
        Console.WriteLine("automatically loaded into the system prompt.\n");

        Console.WriteLine("Available Setting Sources:");
        Console.WriteLine("  - Project: Loads CLAUDE.md from the working directory (with parent traversal)");
        Console.WriteLine("  - User: Loads ~/.claude/CLAUDE.md");
        Console.WriteLine("  - Local: Loads CLAUDE.md from current directory only (no traversal)");
        Console.WriteLine();

        // Example 1: Project settings only
        Console.WriteLine("Example 1: Loading Project Settings");
        Console.WriteLine("------------------------------------");

        ClaudeAgentOptions projectOptions = new()
        {
            // Load project-level CLAUDE.md
            SettingSources = [SettingSource.Project],
            SystemPrompt = SystemPromptConfig.ClaudeCode(),
            MaxTurns = 1
        };

        await ShowSettingsInfoAsync(projectOptions, "project");

        Console.WriteLine("\n");

        // Example 2: Both project and user settings
        Console.WriteLine("Example 2: Loading Project + User Settings");
        Console.WriteLine("-------------------------------------------");

        ClaudeAgentOptions bothOptions = new()
        {
            // Load both project and user CLAUDE.md files
            SettingSources = [SettingSource.Project, SettingSource.User],
            SystemPrompt = SystemPromptConfig.ClaudeCode(),
            MaxTurns = 1
        };

        await ShowSettingsInfoAsync(bothOptions, "project and user");

        Console.WriteLine("\n");

        // Example 3: Local settings (no parent traversal)
        Console.WriteLine("Example 3: Local Settings Only");
        Console.WriteLine("-------------------------------");

        ClaudeAgentOptions localOptions = new()
        {
            // Only load from current directory, no parent traversal
            SettingSources = [SettingSource.Local],
            SystemPrompt = SystemPromptConfig.ClaudeCode(),
            MaxTurns = 1
        };

        await ShowSettingsInfoAsync(localOptions, "local");

        Console.WriteLine("\n");

        // Example 4: Additional data paths
        Console.WriteLine("Example 4: Additional Data Paths");
        Console.WriteLine("---------------------------------");
        Console.WriteLine("You can also specify additional paths to load CLAUDE.md-like files from:");
        Console.WriteLine();

        ClaudeAgentOptions additionalPathsOptions = new()
        {
            SettingSources = [SettingSource.Project],
            AdditionalDataPaths =
            [
                "./team-guidelines/GUIDELINES.md",
                "./docs/CODING_STANDARDS.md"
            ],
            SystemPrompt = SystemPromptConfig.ClaudeCode(),
            MaxTurns = 1
        };

        Console.WriteLine("Configuration:");
        Console.WriteLine("  SettingSources: [Project]");
        Console.WriteLine("  AdditionalDataPaths:");
        foreach (string path in additionalPathsOptions.AdditionalDataPaths!)
        {
            Console.WriteLine($"    - {path}");
        }
    }

    private static async Task ShowSettingsInfoAsync(ClaudeAgentOptions options, string sourceName)
    {
        Console.WriteLine($"Loading settings from: {sourceName}");
        Console.WriteLine();

        await using ClaudeAgentClient client = new(options);

        // Just ask about the settings to verify they're loaded
        string prompt =
            "What special instructions or context do you have from CLAUDE.md files? If none, just say 'No CLAUDE.md loaded'.";

        await foreach (Message message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (ContentBlock block in assistant.MessageContent.Content)
                    {
                        if (block is TextBlock text)
                        {
                            Console.Write(text.Text);
                        }
                    }

                    break;

                case ResultMessage result:
                    Console.WriteLine($"\n[Cost: ${result.TotalCostUsd:F4}]");
                    break;
            }
        }
    }
}
