# Research Agent Example

A multi-agent orchestration example demonstrating how to build a research system with specialized subagents.

## What This Example Demonstrates

- **Multi-agent orchestration** with a lead agent delegating to specialized subagents
- **AgentDefinition** for defining subagents with specific tools and prompts
- **Hook-based tracking** of all tool calls across agents (SubagentStart/SubagentStop hooks)
- **Interactive bidirectional mode** using `CreateSessionAsync`/`SendAsync`/`ReceiveResponseAsync`
- **JSONL logging** of tool calls for analysis
- **Debug diagnostics** via `System.Diagnostics.Debug.WriteLine` (visible in debugger output)

## Architecture

```
Lead Agent (coordinator) - Model: sonnet
├── Researcher Subagents (2-4 parallel) - WebSearch, Read, Write - Model: haiku
└── Report Writer Subagent - Glob, Read, Write - Model: haiku
```

The lead agent:
1. Breaks research requests into 2-4 subtopics
2. Spawns researcher subagents in parallel
3. Waits for all research to complete
4. Spawns a report writer to synthesize findings

## Running the Example

```bash
cd examples/Claude.AgentSdk.ResearchAgent

# Interactive mode (default) - enter prompts manually
dotnet run

# Auto-run mode (for debugging) - runs with default prompt
dotnet run -- --auto

# Run with a custom prompt
dotnet run -- "Research the latest developments in quantum computing"
```

In interactive mode, enter a research topic:
```
You: Research the latest developments in quantum computing
```

Type `exit`, `quit`, or `q` to exit interactive mode.

## Output Structure

```
files/
├── research_notes/     # Markdown files from researchers
└── reports/           # Final synthesized reports

logs/
└── session_YYYYMMDD_HHMMSS/
    └── tool_calls.jsonl   # Structured tool usage log
```

## Key Code Patterns

### Defining Subagents

```csharp
var agents = new Dictionary<string, AgentDefinition>
{
    ["researcher"] = new AgentDefinition
    {
        Description = "Use this agent when you need to gather research information...",
        Tools = ["WebSearch", "Read", "Write"],  // Read for accessing previously written notes
        Prompt = researcherPrompt,
        Model = "haiku"
    },
    ["report-writer"] = new AgentDefinition
    {
        Description = "Use this agent to create formal research reports...",
        Tools = ["Glob", "Read", "Write"],
        Prompt = reportWriterPrompt,
        Model = "haiku"
    }
};
```

### Restricting the Main Agent's Tools

The lead agent is restricted to only use the `Task` tool, forcing it to delegate all work to subagents:

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = leadAgentPrompt,
    // Only allow the main agent to spawn subagents - no direct tool use
    AllowedTools = ["Task"],
    Agents = agents,
    Model = "sonnet",      // Main agent uses sonnet (subagents use haiku)
    MaxTurns = 50,
    PermissionMode = PermissionMode.BypassPermissions
};
```

This ensures the orchestration pattern is enforced - the lead agent coordinates while subagents do the actual work.

### Tracking Subagent Tool Calls

```csharp
var hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
{
    [HookEvent.PreToolUse] = new List<HookMatcher>
    {
        new()
        {
            Hooks = [tracker.PreToolUseHookAsync]
        }
    },
    [HookEvent.PostToolUse] = new List<HookMatcher>
    {
        new()
        {
            Hooks = [tracker.PostToolUseHookAsync]
        }
    },
    // Track when subagents start and stop
    [HookEvent.SubagentStart] = new List<HookMatcher>
    {
        new()
        {
            Hooks = [tracker.SubagentStartHookAsync]
        }
    },
    [HookEvent.SubagentStop] = new List<HookMatcher>
    {
        new()
        {
            Hooks = [tracker.SubagentStopHookAsync]
        }
    }
};
```

### Detecting Subagent Spawns

```csharp
if (toolUse.Name == "Task" && toolUse.Input is { } input)
{
    var subagentType = GetJsonProperty(input, "subagent_type");
    var description = GetJsonProperty(input, "description");

    tracker.RegisterSubagentSpawn(toolUse.Id, subagentType, description);
}
```

## Project Structure

```
Claude.AgentSdk.ResearchAgent/
├── Program.cs              # Main entry point and message handling
├── SubagentTracker.cs      # Hook-based tool call tracking
├── Prompts/
│   ├── LeadAgent.txt       # Coordinator agent prompt
│   ├── Researcher.txt      # Data-focused research prompt
│   └── ReportWriter.txt    # Report synthesis prompt
└── Claude.AgentSdk.ResearchAgent.csproj
```

## Example Session

```
You: Research electric vehicles

Agent: Breaking this into 4 research areas: battery technology, market trends,
manufacturers, and charging infrastructure. Spawning researchers now.

==================================================
SUBAGENT SPAWNED: RESEARCHER-1
==================================================
Task: Battery technology research

[RESEARCHER-1] -> WebSearch (query="EV battery technology 2024 statistics")
[RESEARCHER-1] -> Write (file="battery_technology.md")

...

Research complete. Report saved to files/reports/electric_vehicles_report.md
[45.2s | $0.0234]
```

## Ported From

This example is ported from the official Python demo:
`claude-agent-sdk-demos/research-agent/`
