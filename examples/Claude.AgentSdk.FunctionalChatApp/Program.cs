using System.Text.Json;
using Claude.AgentSdk.Functional;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.FunctionalChatApp;

public static class Program
{
    // Configuration as an immutable record
    private sealed record ChatConfig(
        string SystemPrompt,
        string Model,
        int MaxTurns,
        PermissionMode PermissionMode,
        IReadOnlyList<string> AllowedTools
    );

    // Chat state as an immutable record
    private sealed record ChatState(
        bool ShouldContinue,
        bool ShouldRestart,
        Option<string> LastError
    );

    // Command result as a discriminated union using Either
    private abstract record Command;
    private sealed record QuitCommand : Command;
    private sealed record ClearCommand : Command;
    private sealed record ChatCommand(string Message) : Command;

    private static readonly ChatConfig _defaultConfig = new(
        SystemPrompt: """
            You are a helpful AI assistant. You can help users with a wide variety of tasks including:
            - Answering questions
            - Writing and editing text
            - Coding and debugging
            - Analysis and research
            - Creative tasks

            Be concise but thorough in your responses.
            """,
        Model: "sonnet",
        MaxTurns: 100,
        PermissionMode: PermissionMode.AcceptEdits,
        AllowedTools: ["Bash", "Read", "Write", "Edit", "Glob", "Grep", "WebSearch", "WebFetch", "TodoWrite"]
    );

    public static async Task Main(string[] args)
    {
        PrintBanner();

        // Main loop using functional recursion pattern
        await RunChatLoopAsync(_defaultConfig);
    }

    private static void PrintBanner()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Functional Chat App - Claude Agent SDK");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("A functional programming approach to chat apps.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /clear  - Start a new conversation");
        Console.WriteLine("  /exit   - Exit the application");
        Console.WriteLine();
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();
    }

    private static async Task RunChatLoopAsync(ChatConfig config)
    {
        // Outer loop - handles session restarts
        while (true)
        {
            // Create session and run chat using Result for error handling
            var sessionResult = await RunSessionAsync(config);

            sessionResult.Match(
                success: state =>
                {
                    if (!state.ShouldContinue)
                    {
                        Console.WriteLine("\nGoodbye!");
                        Environment.Exit(0);
                    }
                    // Continue to restart session
                },
                failure: error =>
                {
                    PrintError(error);
                    Console.WriteLine("Press Enter to start a new conversation, or Ctrl+C to exit...");
                    Console.ReadLine();
                }
            );

            Console.WriteLine();
        }
    }

    private static async Task<Result<ChatState>> RunSessionAsync(ChatConfig config)
    {
        return await Result.TryAsync(async () =>
        {
            var options = CreateOptions(config);
            await using var client = new ClaudeAgentClient(options);
            await using var session = await client.CreateSessionAsync();

            PrintConnected();

            return await ProcessChatMessagesAsync(session);
        });
    }

    private static ClaudeAgentOptions CreateOptions(ChatConfig config) =>
        new()
        {
            SystemPrompt = config.SystemPrompt,
            Model = config.Model,
            MaxTurns = config.MaxTurns,
            PermissionMode = config.PermissionMode,
            AllowedTools = config.AllowedTools.ToList()
        };

    private static void PrintConnected()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("[Connected - new conversation started]");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintError(string error)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\nError: {error}");
        Console.ResetColor();
    }

    private static async Task<ChatState> ProcessChatMessagesAsync(ClaudeAgentSession session)
    {
        while (true)
        {
            // Get and parse user input using Option
            var command = GetUserInput()
                .Bind(ParseCommand);

            // Handle command using pattern matching
            var (shouldContinue, shouldRestart) = await command.Match(
                some: async cmd => await HandleCommandAsync(cmd, session),
                none: () => Task.FromResult((true, false))
            );

            if (!shouldContinue || shouldRestart)
            {
                return new ChatState(shouldContinue, shouldRestart, Option.None);
            }
        }
    }

    private static Option<string> GetUserInput()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("You: ");
        Console.ResetColor();

        return Option.FromNullable(Console.ReadLine())
            .Map(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s));
    }

    // Parse input string into a Command using functional pattern matching
    private static Option<Command> ParseCommand(string input) =>
        input.ToLowerInvariant() switch
        {
            "/exit" or "/quit" => Option.Some<Command>(new QuitCommand()),
            "/clear" => Option.Some<Command>(new ClearCommand()),
            _ when input.StartsWith('/') => Option.NoneOf<Command>(), // Unknown command
            _ => Option.Some<Command>(new ChatCommand(input))
        };

    private static async Task<(bool ShouldContinue, bool ShouldRestart)> HandleCommandAsync(
        Command command,
        ClaudeAgentSession session)
    {
        return command switch
        {
            QuitCommand => (false, false),
            ClearCommand => HandleClear(),
            ChatCommand chat => await HandleChatAsync(chat.Message, session),
            _ => (true, false)
        };
    }

    private static (bool, bool) HandleClear()
    {
        Console.WriteLine();
        Console.WriteLine("[Starting new conversation...]");
        Console.WriteLine();
        return (true, true);
    }

    private static async Task<(bool, bool)> HandleChatAsync(string message, ClaudeAgentSession session)
    {
        await session.SendAsync(message);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Claude: ");
        Console.ResetColor();

        // Process response using functional pipeline
        var processingPipeline = CreateMessageProcessingPipeline();

        await foreach (var msg in session.ReceiveResponseAsync())
        {
            var result = processingPipeline.Run(msg);

            if (result.Match(r => r == ProcessingResult.Completed, _ => false))
            {
                Console.WriteLine();
                break;
            }
        }

        Console.WriteLine();
        return (true, false);
    }

    private enum ProcessingResult { Continue, NoOutput, Completed }

    private static Pipeline<Message, ProcessingResult> CreateMessageProcessingPipeline() =>
        Pipeline.StartWith<Message, ProcessingResult>(ProcessMessage);

    private static Result<ProcessingResult> ProcessMessage(Message message)
    {
        return message switch
        {
            AssistantMessage assistant => ProcessAssistantMessage(assistant),
            ResultMessage result => ProcessResultMessage(result),
            _ => Result.Success(ProcessingResult.NoOutput)
        };
    }

    private static Result<ProcessingResult> ProcessAssistantMessage(AssistantMessage assistant)
    {
        // Use functional collection operations
        var blocks = assistant.MessageContent.Content;

        blocks
            .Choose(block => block switch
            {
                TextBlock text => Option.Some<Action>(() => Console.Write(text.Text)),
                ToolUseBlock toolUse => Option.Some<Action>(() => PrintToolUse(toolUse)),
                ThinkingBlock => Option.Some<Action>(PrintThinking),
                _ => Option.NoneOf<Action>()
            })
            .ToList()
            .ForEach(action => action());

        return Result.Success(ProcessingResult.Continue);
    }

    private static void PrintToolUse(ToolUseBlock toolUse)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"\n[Using {toolUse.Name}");

        // Use Option for safe property access
        GetToolInputSummary(toolUse.Name, toolUse.Input)
            .Do(summary => Console.Write($": {summary}"));

        Console.Write("]");
        Console.ResetColor();
    }

    private static void PrintThinking()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("[thinking...]");
        Console.ResetColor();
    }

    private static Result<ProcessingResult> ProcessResultMessage(ResultMessage result)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4}]");
        Console.ResetColor();
        return Result.Success(ProcessingResult.Completed);
    }

    private static Option<string> GetToolInputSummary(string toolName, JsonElement? input) =>
        input.HasValue
            ? ExtractToolSummary(toolName, input.Value)
            : Option.NoneOf<string>();

    private static Option<string> ExtractToolSummary(string toolName, JsonElement element) =>
        Result.Try(() => toolName switch
        {
            "WebSearch" => GetPropertyString(element, "query").Map(q => $"\"{Truncate(q, 40)}\""),
            "Read" => GetPropertyString(element, "file_path").Map(p => Path.GetFileName(p) ?? p),
            "Write" => GetPropertyString(element, "file_path").Map(p => Path.GetFileName(p) ?? p),
            "Edit" => GetPropertyString(element, "file_path").Map(p => Path.GetFileName(p) ?? p),
            "Bash" => GetPropertyString(element, "command").Map(c => Truncate(c, 30)),
            "Glob" => GetPropertyString(element, "pattern"),
            "Grep" => GetPropertyString(element, "pattern").Map(p => $"\"{Truncate(p, 30)}\""),
            _ => Option.NoneOf<string>()
        })
        .Match(
            success: opt => opt,
            failure: _ => Option.NoneOf<string>()
        );

    private static Option<string> GetPropertyString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop)
            ? Option.FromNullable(prop.GetString())
            : Option.NoneOf<string>();

    private static string Truncate(string? text, int maxLength) =>
        Option.FromNullable(text)
            .Map(t => t.Length <= maxLength ? t : t[..maxLength] + "...")
            .GetValueOrDefault("");
}
