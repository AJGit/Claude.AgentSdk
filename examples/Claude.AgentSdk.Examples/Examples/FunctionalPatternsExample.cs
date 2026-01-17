using System.Text.Json;
using Claude.AgentSdk.Functional;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
/// Demonstrates functional programming patterns with the Claude Agent SDK.
/// Shows Option, Result, Validation, Pipeline, and functional composition.
/// </summary>
public class FunctionalPatternsExample : IExample
{
    public string Name => "Functional Programming Patterns";
    public string Description => "Demonstrates Option, Result, Validation, Pipeline, and functional composition";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates functional programming patterns.");
        Console.WriteLine("We'll use Option, Result, Validation, and Pipeline to handle");
        Console.WriteLine("Claude's responses in a type-safe, composable way.\n");

        await DemonstrateOptionPattern();
        await DemonstrateResultPattern();
        await DemonstrateValidationPattern();
        await DemonstratePipelinePattern();
        await DemonstrateCollectionExtensions();
    }

    private static async Task DemonstrateOptionPattern()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  1. OPTION PATTERN - Safe handling of optional values");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant. Keep responses brief.",
            MaxTurns = 1
        };

        await using var client = new ClaudeAgentClient(options);

        // Query Claude and safely extract the first text content
        var result = await client.QueryToCompletionAsync("What is 2 + 2? Answer with just the number.");

        // Using Option to safely handle the response
        var answer = Option.FromNullable(result)
            .Bind(r => Option.FromNullable(r.Result))
            .Map(text => text.Trim())
            .Where(text => !string.IsNullOrEmpty(text));

        // Pattern matching on Option
        var message = answer.Match(
            some: text => $"Claude answered: {text}",
            none: () => "No response received"
        );

        Console.WriteLine($"  Result: {message}");

        // Chaining with Map and Bind
        var parsed = answer
            .Bind(Option.TryParseInt)
            .Map(num => num * 2);

        Console.WriteLine($"  Doubled: {parsed.Match(n => n.ToString(), () => "Could not parse")}\n");
    }

    private static async Task DemonstrateResultPattern()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  2. RESULT PATTERN - Explicit error handling");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        // Create a pipeline that may fail
        var queryResult = await ExecuteQueryWithResult("Give me a JSON object with keys 'name' and 'age'.");

        // Process the result functionally
        var processedResult = queryResult
            .Map(response => response.Trim())
            .Bind(ParseJsonResponse)
            .Map(FormatPerson)
            .Do(msg => Console.WriteLine($"  Success: {msg}"))
            .DoOnError(err => Console.WriteLine($"  Error: {err}"));

        Console.WriteLine($"  Final status: {(processedResult.IsSuccess ? "OK" : "Failed")}\n");
    }

    private static async Task<Result<string>> ExecuteQueryWithResult(string prompt)
    {
        return await Result.TryAsync(async () =>
        {
            var options = new ClaudeAgentOptions
            {
                SystemPrompt = "Return only valid JSON. No markdown, no explanation.",
                MaxTurns = 1
            };

            await using var client = new ClaudeAgentClient(options);
            var result = await client.QueryToCompletionAsync(prompt);

            return result?.Result ?? throw new InvalidOperationException("No response");
        });
    }

    private static Result<JsonDocument> ParseJsonResponse(string json)
    {
        return Result.Try(() =>
        {
            // Clean up potential markdown code blocks
            var cleaned = json
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();
            return JsonDocument.Parse(cleaned);
        });
    }

    private static string FormatPerson(JsonDocument doc)
    {
        var root = doc.RootElement;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : "Unknown";
        var age = root.TryGetProperty("age", out var a) ? a.GetInt32().ToString() : "?";
        return $"{name}, age {age}";
    }

    private static async Task DemonstrateValidationPattern()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  3. VALIDATION PATTERN - Accumulating errors");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        // Simulate validating Claude agent options
        var testCases = new[]
        {
            new AgentConfig("", -1, "invalid-model"),  // Multiple errors
            new AgentConfig("Valid prompt", 5, "sonnet"),  // Valid
            new AgentConfig("", 10, "sonnet"),  // One error
        };

        foreach (var config in testCases)
        {
            var validation = ValidateAgentConfig(config);

            validation.Match(
                valid: cfg => Console.WriteLine($"  Valid: Prompt='{cfg.SystemPrompt[..Math.Min(20, cfg.SystemPrompt.Length)]}...', MaxTurns={cfg.MaxTurns}"),
                invalid: errors => Console.WriteLine($"  Invalid ({errors.Count} errors): {string.Join("; ", errors)}")
            );
        }

        Console.WriteLine();

        // Now use a valid config
        var validConfig = new AgentConfig("You are a helpful assistant.", 3, "sonnet");
        var validationResult = ValidateAgentConfig(validConfig);

        if (validationResult.IsValid)
        {
            Console.WriteLine("  Running query with validated config...");
            var options = new ClaudeAgentOptions
            {
                SystemPrompt = validationResult.Value.SystemPrompt,
                MaxTurns = validationResult.Value.MaxTurns,
                Model = validationResult.Value.Model
            };

            await using var client = new ClaudeAgentClient(options);
            var result = await client.QueryToCompletionAsync("Say 'Validation works!' in exactly those words.");
            Console.WriteLine($"  Response: {result?.Result?.Trim()}\n");
        }
    }

    private sealed record AgentConfig(string SystemPrompt, int MaxTurns, string Model);

    private static Validation<AgentConfig, string> ValidateAgentConfig(AgentConfig config)
    {
        var promptValidation = Validation.From(
            config.SystemPrompt,
            p => !string.IsNullOrWhiteSpace(p),
            "System prompt cannot be empty"
        ).ToGeneric();

        var turnsValidation = Validation.From(
            config.MaxTurns,
            t => t > 0,
            "MaxTurns must be positive"
        ).ToGeneric();

        var modelValidation = Validation.From(
            config.Model,
            m => m is "sonnet" or "opus" or "haiku",
            "Model must be 'sonnet', 'opus', or 'haiku'"
        ).ToGeneric();

        // Combine all validations - errors accumulate
        return Validation.Map3<string, int, string, AgentConfig, string>(
            promptValidation,
            turnsValidation,
            modelValidation,
            (_, _, _) => config
        );
    }

    private static async Task DemonstratePipelinePattern()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  4. PIPELINE PATTERN - Railway-oriented programming");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        // Create a reusable query pipeline
        var queryPipeline = Pipeline
            .Start<string>()
            .ThenEnsure(
                prompt => !string.IsNullOrWhiteSpace(prompt),
                "Prompt cannot be empty")
            .ThenEnsure(
                prompt => prompt.Length <= 1000,
                prompt => $"Prompt too long ({prompt.Length} > 1000 chars)")
            .Then(prompt => prompt.Trim())
            .ThenTap(prompt => Console.WriteLine($"  Processing: '{prompt[..Math.Min(30, prompt.Length)]}...'"));

        // Test the pipeline with various inputs
        var testPrompts = new[]
        {
            "",
            "What is the capital of France?",
            new string('x', 1001)
        };

        foreach (var prompt in testPrompts)
        {
            var result = queryPipeline.Run(prompt);
            result.Match(
                success: p => Console.WriteLine($"  Valid prompt: '{p[..Math.Min(20, p.Length)]}...'"),
                failure: err => Console.WriteLine($"  Rejected: {err}")
            );
        }

        Console.WriteLine();

        // Use a valid prompt with the full async pipeline
        var asyncPipeline = Pipeline
            .StartAsync<string, string>(async prompt =>
            {
                var options = new ClaudeAgentOptions { MaxTurns = 1 };
                await using var client = new ClaudeAgentClient(options);
                var result = await client.QueryToCompletionAsync(prompt);
                return result?.Result ?? "";
            })
            .Then(response => response.Trim())
            .ThenEnsure(r => r.Length > 0, "Empty response")
            .ThenTap(r => Console.WriteLine($"  Got response: {r[..Math.Min(50, r.Length)]}..."));

        var pipelineResult = await asyncPipeline.RunAsync("What is 10 + 10? Answer with just the number.");
        Console.WriteLine($"  Pipeline result: {pipelineResult.Match(r => r, e => $"Error: {e}")}\n");
    }

    private static async Task DemonstrateCollectionExtensions()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  5. COLLECTION EXTENSIONS - Functional collection ops");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "Give brief answers.",
            MaxTurns = 1
        };

        await using var client = new ClaudeAgentClient(options);

        // Collect multiple responses
        var prompts = new[]
        {
            "What is 1 + 1?",
            "What is 2 + 2?",
            "What is 3 + 3?"
        };

        Console.WriteLine("  Querying Claude with multiple prompts...");

        var results = new List<Result<string>>();
        foreach (var prompt in prompts)
        {
            var result = await Result.TryAsync(async () =>
            {
                var response = await client.QueryToCompletionAsync(prompt);
                return response?.Result?.Trim() ?? throw new InvalidOperationException("No response");
            });
            results.Add(result);
        }

        // Partition results into successes and failures
        var (successes, failures) = results.Partition();

        Console.WriteLine($"  Successes: {successes.Count}, Failures: {failures.Count}");

        // Use FirstOrNone for safe access
        var firstSuccess = successes.FirstOrNone();
        Console.WriteLine($"  First success: {firstSuccess.GetValueOrDefault("none")}");

        // Use Choose to filter and transform
        var numericAnswers = results.Choose(r =>
            r.IsSuccess && int.TryParse(r.Value.Trim(), out var num)
                ? Option.Some(num)
                : Option.None);

        Console.WriteLine($"  Numeric answers: [{string.Join(", ", numericAnswers)}]");

        // Safe aggregation
        var average = numericAnswers.ToList().AverageOrNone();
        Console.WriteLine($"  Average: {average.Match(a => a.ToString("F1"), () => "N/A")}");

        Console.WriteLine();
    }
}
