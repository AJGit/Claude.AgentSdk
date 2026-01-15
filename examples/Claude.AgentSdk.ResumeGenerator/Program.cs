/// <summary>
/// Resume Generator - Claude Agent SDK Example
///
/// This example demonstrates:
/// - Using WebSearch to research a person's professional background
/// - Generating structured documents (markdown resume)
/// - One-shot query with tool execution
///
/// Usage: dotnet run -- "Person Name"
///
/// Ported from: claude-agent-sdk-demos/resume-generator/
/// </summary>

using System.Text.Json;
using Claude.AgentSdk;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.ResumeGenerator;

public static class Program
{
    private const string SystemPrompt = """
        You are a professional resume writer. Research a person and create a 1-page resume in markdown format.

        WORKFLOW:
        1. Use WebSearch to find the person's background (LinkedIn, GitHub, company pages, articles)
        2. Gather information about their experience, education, skills, and achievements
        3. Create a well-structured markdown resume file

        OUTPUT:
        - Save the resume to: output/resume.md

        RESUME FORMAT:
        - Professional summary (2-3 sentences)
        - Work Experience (3 most recent/relevant positions)
        - Education
        - Skills (technical and soft skills)
        - Notable achievements or projects

        STYLE GUIDELINES:
        - Keep it concise - max 1 page worth of content
        - 2-3 bullet points per job
        - Focus on quantifiable achievements where possible
        - Use professional, action-oriented language
        - Include dates for positions and education

        If you cannot find enough information about the person, acknowledge this and create
        a template resume that they can fill in with their own details.
        """;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Resume Generator - Claude Agent SDK");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Get person name from args or prompt
        string personName;
        if (args.Length > 0)
        {
            personName = string.Join(" ", args);
        }
        else
        {
            Console.Write("Enter the person's name: ");
            personName = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(personName))
            {
                Console.WriteLine("Error: Please provide a person's name.");
                Console.WriteLine("Usage: dotnet run -- \"Person Name\"");
                return;
            }
        }

        Console.WriteLine($"Generating resume for: {personName}");
        Console.WriteLine();

        // Setup output directory
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = SystemPrompt,
            Model = "sonnet",
            MaxTurns = 30,
            PermissionMode = PermissionMode.AcceptEdits,
            AllowedTools = ["WebSearch", "WebFetch", "Write", "Read", "Glob", "Bash"]
        };

        var prompt = $"""
            Research "{personName}" and create a professional 1-page resume as a markdown file.

            Search for their professional background, work experience, education, and skills.
            Use multiple searches to gather comprehensive information.

            Save the final resume to: output/resume.md
            """;

        Console.WriteLine("Researching and creating resume...");
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();

        await using var client = new ClaudeAgentClient(options);

        await foreach (var message in client.QueryAsync(prompt))
        {
            ProcessMessage(message);
        }

        // Check if resume was created
        var resumePath = Path.Combine(outputDir, "resume.md");
        Console.WriteLine();
        Console.WriteLine(new string('=', 50));

        if (File.Exists(resumePath))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Resume saved to: {resumePath}");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("Resume Preview:");
            Console.WriteLine(new string('-', 50));

            var content = await File.ReadAllTextAsync(resumePath);
            // Show first 30 lines
            var lines = content.Split('\n').Take(30);
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }

            if (content.Split('\n').Length > 30)
            {
                Console.WriteLine("...[truncated]...");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Note: Resume file was not created at the expected path.");
            Console.WriteLine("Check the output above for the generated content.");
            Console.ResetColor();
        }

        Console.WriteLine(new string('=', 50));
    }

    private static void ProcessMessage(Message message)
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

                        case ToolUseBlock toolUse:
                            Console.ForegroundColor = ConsoleColor.Cyan;

                            if (toolUse.Name == "WebSearch" && toolUse.Input is { } input)
                            {
                                var query = GetJsonProperty(input, "query");
                                Console.WriteLine($"Searching: \"{query}\"");
                            }
                            else if (toolUse.Name == "Write" && toolUse.Input is { } writeInput)
                            {
                                var filePath = GetJsonProperty(writeInput, "file_path");
                                Console.WriteLine($"Writing: {Path.GetFileName(filePath ?? "file")}");
                            }
                            else
                            {
                                Console.WriteLine($"Using tool: {toolUse.Name}");
                            }

                            Console.ResetColor();
                            break;
                    }
                }
                break;

            case ResultMessage result:
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[Completed in {result.DurationMs / 1000.0:F1}s | Cost: ${result.TotalCostUsd:F4}]");
                Console.ResetColor();
                break;
        }
    }

    private static string? GetJsonProperty(JsonElement element, string propertyName)
    {
        try
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                return value.GetString();
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }
}
