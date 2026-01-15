# Resume Generator Example

Generate professional resumes by researching a person's background using web search.

## What This Example Demonstrates

- **WebSearch tool** for researching a person's professional background
- **Document generation** - creating structured markdown files
- **One-shot query** with tool execution
- **Command-line argument handling**

## Features

Given a person's name, this example:
1. Searches for their professional background (LinkedIn, GitHub, company pages)
2. Gathers information about experience, education, and skills
3. Generates a well-structured markdown resume
4. Saves the resume to the output directory

## Running the Example

```bash
cd examples/Claude.AgentSdk.ResumeGenerator
dotnet run -- "Jane Doe"
```

Or run without arguments to be prompted:

```bash
dotnet run
Enter the person's name: John Smith
```

## Output

The generated resume is saved to `output/resume.md` with this structure:

```markdown
# John Smith

## Professional Summary
Experienced software engineer with 10+ years...

## Work Experience

### Senior Engineer - Tech Company (2020-Present)
- Led development of microservices architecture
- Mentored team of 5 junior developers
- Reduced deployment time by 40%

### Software Developer - Startup Inc (2015-2020)
...

## Education
- M.S. Computer Science, Stanford University (2015)
- B.S. Computer Science, MIT (2013)

## Skills
- Languages: Python, TypeScript, Go, Rust
- Frameworks: React, Node.js, Django
- Cloud: AWS, GCP, Kubernetes
```

## Key Code Patterns

### System Prompt for Resume Writing

```csharp
private const string SystemPrompt = """
    You are a professional resume writer. Research a person and create
    a 1-page resume in markdown format.

    WORKFLOW:
    1. Use WebSearch to find the person's background (LinkedIn, GitHub, etc.)
    2. Gather information about their experience, education, skills
    3. Create a well-structured markdown resume file in output/resume.md
    4. If the person cannot be found, create a template resume with placeholder data

    OUTPUT DIRECTORY: output/resume.md

    STYLE GUIDELINES:
    - Keep it concise - max 1 page worth of content
    - 2-3 bullet points per job
    - Focus on quantifiable achievements
    - Use professional, action-oriented language

    RESUME FORMAT:
    - Professional Summary
    - Work Experience (reverse chronological)
    - Education
    - Skills
    """;
```

### Configuration and Execution

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = SystemPrompt,
    Model = "sonnet",
    MaxTurns = 30,
    PermissionMode = PermissionMode.AcceptEdits,
    AllowedTools = ["WebSearch", "WebFetch", "Write", "Read", "Glob", "Bash"]
};

await using var client = new ClaudeAgentClient(options);
await foreach (var message in client.QueryAsync(prompt))
{
    ProcessMessage(message);
}
```

### Processing Messages

```csharp
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
                        Console.Write(text.Text);
                        break;
                    case ToolUseBlock toolUse when toolUse.Name == "WebSearch":
                        var query = GetJsonProperty(toolUse.Input, "query");
                        Console.WriteLine($"Searching: \"{query}\"");
                        break;
                    case ToolUseBlock toolUse when toolUse.Name == "Write":
                        Console.WriteLine($"Writing: {GetJsonProperty(toolUse.Input, "file_path")}");
                        break;
                }
            }
            break;

        case ResultMessage result:
            Console.WriteLine($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4}]");
            break;
    }
}

// Helper for safe JSON extraction
private static string? GetJsonProperty(JsonElement? input, string propertyName)
{
    try
    {
        if (input?.TryGetProperty(propertyName, out var value) == true)
            return value.GetString();
    }
    catch { }
    return null;
}
```

## Project Structure

```
Claude.AgentSdk.ResumeGenerator/
├── Program.cs              # Main entry point
├── Claude.AgentSdk.ResumeGenerator.csproj
└── output/                 # Generated resumes (created at runtime)
    └── resume.md
```

## Notes

- If the person cannot be found, Claude will acknowledge this and create a template resume
- The example uses the Sonnet model for balanced speed and quality
- WebSearch results depend on publicly available information

## Ported From

This example is ported from the official TypeScript demo:
`claude-agent-sdk-demos/resume-generator/resume-generator.ts`
