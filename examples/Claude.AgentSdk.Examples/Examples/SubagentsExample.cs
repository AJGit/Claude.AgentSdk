using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates using custom subagents for specialized tasks.
/// </summary>
public class SubagentsExample : IExample
{
    public string Name => "Custom Subagents";
    public string Description => "Define specialized subagents for task delegation";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates custom subagents for specialized tasks.");
        Console.WriteLine("We'll define a code-reviewer and a docs-writer subagent.\n");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = SystemPromptConfig.ClaudeCode(
                "You can delegate specialized tasks to subagents when appropriate."
            ),

            // Allow the Task tool for subagent invocation
            AllowedTools = ["Task", "Read", "Glob"],

            // Define custom subagents
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["code-reviewer"] = new()
                {
                    Description = "Expert code reviewer. Use for security, quality, and best practices reviews.",
                    Prompt = @"You are an expert code reviewer. Analyze code for:
- Security vulnerabilities
- Performance issues
- Best practices violations
- Code quality and maintainability

Provide specific, actionable feedback with line references.",
                    Tools = ["Read", "Grep", "Glob"],
                    Model = "sonnet" // Use a specific model for this agent
                },

                ["docs-writer"] = new()
                {
                    Description =
                        "Technical documentation writer. Use for generating docs, READMEs, and API documentation.",
                    Prompt = @"You are a technical documentation specialist. Create clear,
comprehensive documentation following best practices:
- Use proper markdown formatting
- Include code examples
- Structure content logically
- Write for the target audience",
                    Tools = ["Read", "Write", "Glob"],
                    Model = "sonnet"
                },

                ["test-generator"] = new()
                {
                    Description = "Unit test generator. Use for creating comprehensive test suites.",
                    Prompt = @"You are a testing expert. Generate thorough unit tests that:
- Cover edge cases
- Test error conditions
- Use appropriate assertions
- Follow testing best practices for the language/framework",
                    Tools = ["Read", "Write", "Glob", "Grep"]
                }
            },

            PermissionMode = PermissionMode.AcceptEdits,
            MaxTurns = 5
        };

        await using var client = new ClaudeAgentClient(options);

        var prompt = @"Please use the code-reviewer subagent to review the following code snippet:

```python
def get_user(id):
    query = f""SELECT * FROM users WHERE id = {id}""
    result = db.execute(query)
    return result
```

Provide the review findings.";

        Console.WriteLine($"Prompt:\n{prompt}\n");
        Console.WriteLine("Response:");
        Console.WriteLine("---------");

        await foreach (var message in client.QueryAsync(prompt))
        {
            switch (message)
            {
                case AssistantMessage assistant:
                    foreach (var block in assistant.MessageContent.Content)
                    {
                        switch (block)
                        {
                            case TextBlock text:
                                Console.WriteLine(text.Text);
                                break;

                            case ToolUseBlock { Name: "Task" }:
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.WriteLine("\n[Delegating to subagent...]");
                                Console.ResetColor();
                                break;
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
