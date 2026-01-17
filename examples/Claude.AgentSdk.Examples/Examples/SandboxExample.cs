using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates sandbox configuration options for secure command execution.
/// </summary>
public class SandboxExample : IExample
{
    public string Name => "Sandbox Configuration";
    public string Description => "Configure sandbox settings for secure command execution";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates different sandbox configuration options.");
        Console.WriteLine("Sandbox mode restricts what commands can do for security.\n");

        // Example 1: Simple sandbox mode (most common)
        Console.WriteLine("=== Example 1: Simple Sandbox Mode ===");
        Console.WriteLine("Using SandboxMode.Strict for basic sandboxing.\n");

        ClaudeAgentOptions simpleOptions = new()
        {
            // Simple mode - just specify strict/permissive/off
            Sandbox = SandboxMode.Strict,
            MaxTurns = 1
        };

        Console.WriteLine("Configured with: Sandbox = SandboxMode.Strict");

        // Example 2: Using static helpers
        Console.WriteLine("\n=== Example 2: Static Helpers ===");
        Console.WriteLine("Using SandboxConfig static properties for cleaner syntax.\n");

        ClaudeAgentOptions helperOptions = new()
        {
            Sandbox = SandboxConfig.Permissive, // Equivalent to SandboxMode.Permissive
            MaxTurns = 1
        };

        Console.WriteLine("Configured with: Sandbox = SandboxConfig.Permissive");

        // Example 3: Detailed settings for advanced control
        Console.WriteLine("\n=== Example 3: Detailed Sandbox Settings ===");
        Console.WriteLine("Full control over sandbox behavior with SandboxSettings.\n");

        ClaudeAgentOptions detailedOptions = new()
        {
            Sandbox = new SandboxSettings
            {
                IsEnabled = true,
                AutoAllowBashIfSandboxed = true, // Auto-approve bash when sandboxed
                ExcludedCommands = ["docker", "podman"], // These bypass sandbox
                AllowUnsandboxedCommands = false, // Model cannot request unsandboxed execution
                Network = new NetworkSandboxSettings
                {
                    AllowLocalBinding = true, // Allow dev servers to bind ports
                    AllowUnixSockets = ["/var/run/docker.sock"], // Docker socket access
                    HttpProxyPort = 8080
                },
                IgnoreViolations = new SandboxIgnoreViolations
                {
                    File = ["/tmp/*", "/var/tmp/*"], // Ignore violations in temp dirs
                    Network = ["localhost", "127.0.0.1"]
                }
            },
            MaxTurns = 1
        };

        Console.WriteLine("Configured with detailed SandboxSettings:");
        Console.WriteLine("  - IsEnabled: true");
        Console.WriteLine("  - AutoAllowBashIfSandboxed: true");
        Console.WriteLine("  - ExcludedCommands: docker, podman");
        Console.WriteLine("  - Network.AllowLocalBinding: true");
        Console.WriteLine("  - Network.AllowUnixSockets: /var/run/docker.sock");

        // Example 4: Fluent configuration
        Console.WriteLine("\n=== Example 4: Fluent Configuration ===");
        Console.WriteLine("Using SandboxConfig.WithSettings for fluent API.\n");

        ClaudeAgentOptions fluentOptions = new()
        {
            Sandbox = SandboxConfig.WithSettings(s => s with
            {
                AutoAllowBashIfSandboxed = true,
                ExcludedCommands = ["git", "npm"]
            }),
            MaxTurns = 1
        };

        Console.WriteLine("Configured with: SandboxConfig.WithSettings(...)");

        // Run a simple query with sandbox enabled
        Console.WriteLine("\n=== Running Query with Sandbox ===\n");

        await using ClaudeAgentClient client = new(simpleOptions);

        string prompt = "What is 2 + 2? Respond with just the number.";
        Console.WriteLine($"Prompt: {prompt}\n");
        Console.WriteLine("Response:");

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
                    Console.WriteLine($"\n\n[Query completed - Cost: ${result.TotalCostUsd:F4}]");
                    break;
            }
        }

        Console.WriteLine("\n\nSandbox configuration complete!");
    }
}
