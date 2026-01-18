using System.Text.Json;
using Claude.AgentSdk.Schema;

namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
///     Demonstrates using structured outputs with JSON schema validation.
/// </summary>
public class StructuredOutputExample : IExample
{
    public string Name => "Structured Outputs (JSON Schema)";
    public string Description => "Get typed, validated JSON responses from Claude";

    public async Task RunAsync()
    {
        Console.WriteLine("This example demonstrates structured outputs with JSON schema.");
        Console.WriteLine("Claude's response will be validated against a defined schema.\n");

        // Define the output schema using the SchemaGenerator
        var schema = SchemaGenerator.Generate<MovieReview>("movie_review");

        Console.WriteLine("Schema:");
        Console.WriteLine(JsonSerializer.Serialize(
            JsonDocument.Parse(schema.GetRawText()).RootElement,
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine();

        var options = new ClaudeAgentOptions
        {
            SystemPrompt =
                "You are a movie critic. Analyze movies and provide structured reviews. Return ONLY the JSON object with no additional text.",
            OutputFormat = schema,
            MaxTurns = 3 // Allow enough turns for structured output completion
        };

        await using var client = new ClaudeAgentClient(options);

        var prompt = "Write a review for the movie 'The Matrix' (1999)";
        Console.WriteLine($"Prompt: {prompt}\n");

        var result = await client.QueryToCompletionAsync(prompt);

        if (result?.StructuredOutput is not null)
        {
            Console.WriteLine("Structured Response:");
            Console.WriteLine("--------------------");

            // Parse the structured output
            try
            {
                var review = JsonSerializer.Deserialize<MovieReview>(
                    result.StructuredOutput.Value.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (review is not null)
                {
                    Console.WriteLine($"Title: {review.Title}");
                    Console.WriteLine($"Year: {review.Year}");
                    Console.WriteLine($"Rating: {review.Rating}/10");
                    Console.WriteLine($"Genre: {string.Join(", ", review.Genres)}");
                    Console.WriteLine($"\nSummary:\n{review.Summary}");
                    Console.WriteLine("\nPros:");
                    foreach (var pro in review.Pros)
                    {
                        Console.WriteLine($"  + {pro}");
                    }

                    Console.WriteLine("\nCons:");
                    foreach (var con in review.Cons)
                    {
                        Console.WriteLine($"  - {con}");
                    }

                    Console.WriteLine($"\nRecommended: {(review.Recommended ? "Yes" : "No")}");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to parse response: {ex.Message}");
                Console.WriteLine($"Raw structured output: {result.StructuredOutput.Value.GetRawText()}");
            }

            string context = result.Usage is not null
                ? $"{result.Usage.TotalContextTokens / 1000.0:F0}k"
                : "?";
            Console.WriteLine($"\n[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4} | {context}]");
        }
        else
        {
            Console.WriteLine("No structured output received.");
            if (result is not null)
            {
                Console.WriteLine($"Result text: {result.Result}");
                Console.WriteLine($"IsError: {result.IsError}");
            }
        }
    }
}

/// <summary>
///     Schema for movie review structured output.
/// </summary>
public class MovieReview
{
    public required string Title { get; set; }
    public required int Year { get; set; }
    public required double Rating { get; set; }
    public required string[] Genres { get; set; }
    public required string Summary { get; set; }
    public required string[] Pros { get; set; }
    public required string[] Cons { get; set; }
    public required bool Recommended { get; set; }
}
