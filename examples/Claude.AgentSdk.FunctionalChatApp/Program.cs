using System.Text.Json;
using Claude.AgentSdk.Functional;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.FunctionalChatApp;

public static class Program
{
    private static readonly ChatConfig _defaultConfig = new(
        """
        You are a helpful AI assistant. You can help users with a wide variety of tasks including:
        - Answering questions
        - Writing and editing text
        - Coding and debugging
        - Analysis and research
        - Creative tasks

        Be concise but thorough in your responses.
        """,
        "sonnet",
        100,
        PermissionMode.AcceptEdits,
        ["Bash", "Read", "Write", "Edit", "Glob", "Grep", "WebSearch", "WebFetch", "TodoWrite"]
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
            Result<ChatState> sessionResult = await RunSessionAsync(config);

            sessionResult.Match(
                state =>
                {
                    if (!state.ShouldContinue)
                    {
                        Console.WriteLine("\nGoodbye!");
                        Environment.Exit(0);
                    }
                    // Continue to restart session
                },
                error =>
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
            ClaudeAgentOptions options = CreateOptions(config);
            await using ClaudeAgentClient client = new(options);
            await using ClaudeAgentSession session = await client.CreateSessionAsync();

            PrintConnected();

            return await ProcessChatMessagesAsync(session);
        });
    }

    private static ClaudeAgentOptions CreateOptions(ChatConfig config)
    {
        return new ClaudeAgentOptions
        {
            SystemPrompt = config.SystemPrompt,
            Model = config.Model,
            MaxTurns = config.MaxTurns,
            PermissionMode = config.PermissionMode,
            AllowedTools = config.AllowedTools.ToList()
        };
    }

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
            Option<Command> command = GetUserInput()
                .Bind(ParseCommand);

            // Handle command using pattern matching
            (bool shouldContinue, bool shouldRestart) = await command.Match(
                async cmd => await HandleCommandAsync(cmd, session),
                () => Task.FromResult((true, false))
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
    private static Option<Command> ParseCommand(string input)
    {
        return input.ToLowerInvariant() switch
        {
            "/exit" or "/quit" => Option.Some<Command>(new QuitCommand()),
            "/clear" => Option.Some<Command>(new ClearCommand()),
            _ when input.StartsWith('/') => Option.NoneOf<Command>(), // Unknown command
            _ => Option.Some<Command>(new ChatCommand(input))
        };
    }

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
        Pipeline<Message, ProcessingResult> processingPipeline = CreateMessageProcessingPipeline();

        await foreach (Message msg in session.ReceiveResponseAsync())
        {
            Result<ProcessingResult> result = processingPipeline.Run(msg);

            if (result.Match(r => r == ProcessingResult.Completed, _ => false))
            {
                Console.WriteLine();
                break;
            }
        }

        Console.WriteLine();
        return (true, false);
    }

    private static Pipeline<Message, ProcessingResult> CreateMessageProcessingPipeline()
    {
        return Pipeline.StartWith<Message, ProcessingResult>(ProcessMessage);
    }

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
        IReadOnlyList<ContentBlock> blocks = assistant.MessageContent.Content;

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

    private static Option<string> GetToolInputSummary(string toolName, JsonElement? input)
    {
        return input.HasValue
            ? ExtractToolSummary(toolName, input.Value)
            : Option.NoneOf<string>();
    }

    private static Option<string> ExtractToolSummary(string toolName, JsonElement element)
    {
        return Result.Try(() => toolName switch
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
                opt => opt,
                _ => Option.NoneOf<string>()
            );
    }

    private static Option<string> GetPropertyString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement prop)
            ? Option.FromNullable(prop.GetString())
            : Option.NoneOf<string>();
    }

    private static string Truncate(string? text, int maxLength)
    {
        return Option.FromNullable(text)
            .Map(t => t.Length <= maxLength ? t : t[..maxLength] + "...")
            .GetValueOrDefault("");
    }

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

    private enum ProcessingResult { Continue, NoOutput, Completed }
}
