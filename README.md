# Claude.AgentSdk

A C# SDK for building agents with the Claude Code CLI. This SDK provides a .NET interface to the same agent capabilities that power Claude Code.

## Requirements

- **.NET 10.0** or later
- **Claude Code CLI** installed and available in PATH

## Installation

### Install Claude Code CLI

```bash
npm install -g @anthropic-ai/claude-code
```

Verify installation:
```bash
claude --version
```

### Add SDK to Your Project

Reference the project directly:
```xml
<ProjectReference Include="path/to/Claude.AgentSdk.csproj" />
```

Or (once published):
```bash
dotnet add package Claude.AgentSdk
```

## Quick Start

### Simple Query

```csharp
using Claude.AgentSdk;
using Claude.AgentSdk.Messages;

var client = new ClaudeAgentClient();

await foreach (var message in client.QueryAsync("What is the capital of France?"))
{
    if (message is AssistantMessage assistant)
    {
        foreach (var block in assistant.MessageContent.Content)
        {
            if (block is TextBlock text)
            {
                Console.Write(text.Text);
            }
        }
    }
}
```

### With Options

```csharp
var options = new ClaudeAgentOptions
{
    Model = "sonnet",                              // Model to use
    MaxTurns = 10,                                 // Limit conversation turns
    SystemPrompt = "You are a helpful assistant.", // Custom system prompt (string)
    AllowedTools = ["Read", "Glob", "Grep"],       // Tools Claude can use
    WorkingDirectory = "/path/to/project"          // Working directory
};

var client = new ClaudeAgentClient(options);
```

### Using CLAUDE.md Files

CLAUDE.md files provide project-specific context and instructions. To load them, you must explicitly specify `SettingSources`:

```csharp
var options = new ClaudeAgentOptions
{
    // Use Claude Code's system prompt (includes tool instructions, code guidelines, etc.)
    SystemPrompt = SystemPromptConfig.ClaudeCode(),

    // IMPORTANT: You must specify SettingSources to load CLAUDE.md files
    // The claude_code preset alone does NOT load CLAUDE.md automatically
    SettingSources = [SettingSource.Project],  // Load project-level CLAUDE.md

    WorkingDirectory = "/path/to/project"
};

var client = new ClaudeAgentClient(options);

await foreach (var message in client.QueryAsync("Help me refactor this code"))
{
    // Claude now has access to your project guidelines from CLAUDE.md
}
```

#### System Prompt Options

```csharp
// Option 1: Custom string prompt (replaces default entirely)
SystemPrompt = "You are a Python specialist."

// Option 2: Use Claude Code's preset (includes tools, code guidelines, safety)
SystemPrompt = SystemPromptConfig.ClaudeCode()

// Option 3: Use preset with appended instructions
SystemPrompt = SystemPromptConfig.ClaudeCode(append: "Always use TypeScript strict mode.")

// Option 4: Explicit preset configuration
SystemPrompt = new PresetSystemPrompt
{
    Preset = "claude_code",
    Append = "Focus on performance optimization."
}
```

#### Setting Sources

```csharp
// Load only project-level CLAUDE.md (./CLAUDE.md or ./.claude/CLAUDE.md)
SettingSources = [SettingSource.Project]

// Load only user-level CLAUDE.md (~/.claude/CLAUDE.md)
SettingSources = [SettingSource.User]

// Load both project and user-level CLAUDE.md
SettingSources = [SettingSource.Project, SettingSource.User]

// Load project, user, and local settings (CLAUDE.local.md - gitignored)
SettingSources = [SettingSource.Project, SettingSource.User, SettingSource.Local]
```

**CLAUDE.md locations:**
- **Project-level:** `CLAUDE.md` or `.claude/CLAUDE.md` in your working directory
- **User-level:** `~/.claude/CLAUDE.md` for global instructions across all projects
- **Local-level:** `CLAUDE.local.md` or `.claude/CLAUDE.local.md` (typically gitignored)

### Tool Permission Callback

Control which tools Claude can use:

```csharp
var options = new ClaudeAgentOptions
{
    AllowedTools = ["Read", "Write", "Bash"],
    CanUseTool = async (request, ct) =>
    {
        Console.WriteLine($"Claude wants to use: {request.ToolName}");
        Console.WriteLine($"Input: {request.Input}");

        // Auto-allow read operations
        if (request.ToolName == "Read")
            return new PermissionResultAllow();

        // Deny dangerous operations
        if (request.ToolName == "Bash")
            return new PermissionResultDeny { Message = "Bash not allowed" };

        // Allow with modifications
        return new PermissionResultAllow();
    }
};
```

### MCP Servers

Model Context Protocol (MCP) servers extend Claude with custom tools and capabilities. The SDK supports four transport types.

#### Transport Types

| Transport | Config Type            | Description                       |
| --------- | ---------------------- | --------------------------------- |
| **stdio** | `McpStdioServerConfig` | External process via stdin/stdout |
| **SSE**   | `McpSseServerConfig`   | Server-Sent Events over HTTP      |
| **HTTP**  | `McpHttpServerConfig`  | HTTP request/response             |
| **SDK**   | `McpSdkServerConfig`   | In-process C# tools               |

#### stdio Server (External Process)

```csharp
var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["filesystem"] = new McpStdioServerConfig
        {
            Command = "npx",
            Args = ["@modelcontextprotocol/server-filesystem"],
            Env = new Dictionary<string, string>
            {
                ["ALLOWED_PATHS"] = "/Users/me/projects"
            }
        }
    },
    AllowedTools = ["mcp__filesystem__list_files", "mcp__filesystem__read_file"]
};
```

#### SSE Server (Remote)

```csharp
var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["remote-api"] = new McpSseServerConfig
        {
            Url = "https://api.example.com/mcp/sse",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer your-token"
            }
        }
    }
};
```

#### HTTP Server (Remote)

```csharp
var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["http-service"] = new McpHttpServerConfig
        {
            Url = "https://api.example.com/mcp",
            Headers = new Dictionary<string, string>
            {
                ["X-API-Key"] = "your-api-key"
            }
        }
    }
};
```

#### SDK Server (In-Process C# Tools)

Define tools directly in C# that Claude can call:

```csharp
using Claude.AgentSdk.Tools;

// Create a tool server
var toolServer = new McpToolServer("my-tools", "1.0.0");

// Register a tool with typed input
toolServer.RegisterTool<CalculatorInput>(
    "calculate",
    "Perform arithmetic operations",
    async (input, ct) =>
    {
        var result = input.Operation switch
        {
            "add" => input.A + input.B,
            "multiply" => input.A * input.B,
            _ => throw new ArgumentException("Unknown operation")
        };
        return ToolResult.Text($"Result: {result}");
    });

// Or use attributes
public class MyTools
{
    [ClaudeTool("get_weather", "Get weather for a location")]
    public Task<string> GetWeather(string location)
    {
        return Task.FromResult($"Weather in {location}: 72°F, sunny");
    }
}

toolServer.RegisterToolsFrom(new MyTools());

// Use with client
var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["my-tools"] = new McpSdkServerConfig
        {
            Name = "my-tools",
            Instance = toolServer
        }
    }
};

record CalculatorInput(double A, double B, string Operation);
```

#### Combining Multiple MCP Servers

```csharp
var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        // External filesystem server
        ["filesystem"] = new McpStdioServerConfig
        {
            Command = "npx",
            Args = ["@modelcontextprotocol/server-filesystem"]
        },
        // Remote API via SSE
        ["remote-api"] = new McpSseServerConfig
        {
            Url = "https://api.example.com/mcp/sse"
        },
        // In-process custom tools
        ["custom"] = new McpSdkServerConfig
        {
            Name = "custom",
            Instance = myToolServer
        }
    },
    // Allow specific MCP tools (format: mcp__<server>__<tool>)
    AllowedTools = [
        "mcp__filesystem__list_files",
        "mcp__remote-api__query",
        "mcp__custom__calculate"
    ]
};
```

### Subagents

Subagents are separate agent instances that handle focused subtasks. Use them to isolate context, run tasks in parallel, and apply specialized instructions.

#### Defining Subagents

```csharp
var options = new ClaudeAgentOptions
{
    // Task tool is required for subagent invocation
    AllowedTools = ["Read", "Grep", "Glob", "Task"],

    Agents = new Dictionary<string, AgentDefinition>
    {
        ["code-reviewer"] = new AgentDefinition
        {
            // Description tells Claude when to use this subagent
            Description = "Expert code review specialist. Use for quality, security, and maintainability reviews.",

            // Prompt defines the subagent's behavior
            Prompt = """
                You are a code review specialist with expertise in security and best practices.
                When reviewing code:
                - Identify security vulnerabilities
                - Check for performance issues
                - Suggest specific improvements
                Be thorough but concise.
                """,

            // Tools restricts what the subagent can do (read-only here)
            Tools = ["Read", "Grep", "Glob"],

            // Model overrides the default model for this subagent
            Model = "sonnet"
        },

        ["test-runner"] = new AgentDefinition
        {
            Description = "Runs and analyzes test suites. Use for test execution and coverage analysis.",
            Prompt = "You are a test execution specialist. Run tests and analyze results.",
            // Bash access lets this subagent run test commands
            Tools = ["Bash", "Read", "Grep"]
        }
    }
};

var client = new ClaudeAgentClient(options);
await foreach (var msg in client.QueryAsync("Review the authentication module for security issues"))
{
    // Claude will automatically delegate to code-reviewer based on the task
}
```

#### AgentDefinition Properties

| Property      | Type                     | Required | Description                                              |
| ------------- | ------------------------ | -------- | -------------------------------------------------------- |
| `Description` | `string`                 | Yes      | When to use this agent (Claude uses this for delegation) |
| `Prompt`      | `string`                 | Yes      | System prompt defining the agent's role                  |
| `Tools`       | `IReadOnlyList<string>?` | No       | Allowed tools (inherits all if omitted)                  |
| `Model`       | `string?`                | No       | Model override ("sonnet", "opus", "haiku")               |

#### Common Tool Combinations

| Use Case           | Tools                                       | Description                    |
| ------------------ | ------------------------------------------- | ------------------------------ |
| Read-only analysis | `["Read", "Grep", "Glob"]`                  | Can examine but not modify     |
| Test execution     | `["Bash", "Read", "Grep"]`                  | Can run commands               |
| Code modification  | `["Read", "Edit", "Write", "Grep", "Glob"]` | Full read/write                |
| Full access        | `null` (omit)                               | Inherits all tools from parent |

#### Explicit Invocation

To guarantee Claude uses a specific subagent, mention it by name:

```csharp
await foreach (var msg in client.QueryAsync("Use the code-reviewer agent to check the auth module"))
{
    // Directly invokes the named subagent
}
```

> **Note:** Subagents cannot spawn their own subagents. Don't include "Task" in a subagent's Tools array.

#### Subagent Tool Configuration

**How Tool Permissions Work:**

The main agent's `Tools` list creates a **base tool pool**. Subagents can use:
1. Tools from the main agent's pool (shared tools like Read, Write)
2. Tools in their own `AgentDefinition.Tools` list (even if not in main's pool)

**Working Pattern:**

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = "You are a coordinator. Spawn subagents to do research.",

    // Main agent: Task for spawning + shared tools subagents need
    Tools = new ToolsList(["Task", "Read", "Write"]),

    Agents = new Dictionary<string, AgentDefinition>
    {
        ["researcher"] = new AgentDefinition
        {
            Description = "Research specialist for web searches",
            // Subagent gets: WebSearch (own list) + Read, Write (from main's pool)
            Tools = ["WebSearch", "Write", "Read"],
            Prompt = "You research topics and save findings to files.",
            Model = "haiku"
        }
    }
};
```

| Tool      | Main Agent | Subagent Config | Subagent Can Use?            |
| --------- | ---------- | --------------- | ---------------------------- |
| Task      | ✓          | ✗               | N/A (for spawning)           |
| Read      | ✓          | ✓               | ✓ (from main's pool)         |
| Write     | ✓          | ✓               | ✓ (from main's pool)         |
| WebSearch | ✗          | ✓               | ✓ (from subagent's own list) |

**Key Points:**
- Include `Task` in main agent's tools for spawning subagents
- Include shared file tools (`Read`, `Write`) in main agent's tools - subagents need these in the pool
- Subagent-specific tools (like `WebSearch`) only need to be in the subagent's `Tools` list
- Use full absolute paths in prompts for file operations (relative paths may resolve incorrectly)
- The `parent_tool_use_id` field may be null, but you can identify subagent execution by checking the `Model` field (e.g., haiku vs sonnet)

**Common Pitfall:**

If main agent has `Tools = ["Task"]` only (without Read/Write), subagents cannot write files even if Write is in their Tools list. The shared tools must be in the main agent's pool.

### Slash Commands

Slash commands control Claude Code sessions with special `/` prefixed commands.

#### Discovering Available Commands

```csharp
var client = new ClaudeAgentClient();

await foreach (var msg in client.QueryAsync("Hello"))
{
    if (msg is SystemMessage sys && sys.IsInit)
    {
        Console.WriteLine("Available commands:");
        foreach (var cmd in sys.SlashCommands ?? [])
        {
            Console.WriteLine($"  {cmd}");
        }
        // Output: /compact, /clear, /help, /review, etc.
    }
}
```

#### Sending Slash Commands

Send commands as prompt strings:

```csharp
// Compact conversation history
await foreach (var msg in client.QueryAsync("/compact"))
{
    if (msg is SystemMessage sys && sys.IsCompactBoundary)
    {
        Console.WriteLine($"Compacted: {sys.CompactMetadata?.PreTokens} → {sys.CompactMetadata?.PostTokens} tokens");
    }
}

// Clear conversation and start fresh
await foreach (var msg in client.QueryAsync("/clear"))
{
    if (msg is SystemMessage sys && sys.IsInit)
    {
        Console.WriteLine($"New session: {sys.SessionId}");
    }
}
```

#### Commands with Arguments

Pass arguments after the command:

```csharp
// Custom command with arguments (e.g., /fix-issue defined in .claude/commands/fix-issue.md)
await foreach (var msg in client.QueryAsync("/fix-issue 123 high"))
{
    // Arguments are passed as $1="123", $2="high" to the command
}

// Refactor a specific file
await foreach (var msg in client.QueryAsync("/refactor src/auth/login.cs"))
{
    // ...
}
```

#### Custom Slash Commands

Create custom commands as markdown files in `.claude/commands/`:

**`.claude/commands/security-check.md`:**
```markdown
---
allowed-tools: Read, Grep, Glob
description: Run security vulnerability scan
model: claude-sonnet-4-5-20250929
---

Analyze the codebase for security vulnerabilities including:
- SQL injection risks
- XSS vulnerabilities
- Exposed credentials
```

**`.claude/commands/fix-issue.md`:**
```markdown
---
argument-hint: [issue-number] [priority]
description: Fix a GitHub issue
---

Fix issue #$1 with priority $2.
Check the issue description and implement the necessary changes.
```

#### File References in Commands

Reference files using `@` prefix in command definitions:

```markdown
Review the following configuration:
- Package config: @package.json
- TypeScript config: @tsconfig.json
```

#### Bash Output in Commands

Include bash command output using `!`:

```markdown
## Context
- Current status: !`git status`
- Recent changes: !`git diff HEAD~1`
```

#### SystemMessage Properties

| Property            | Type                              | Description                                        |
| ------------------- | --------------------------------- | -------------------------------------------------- |
| `Subtype`           | `string`                          | Message subtype ("init", "compact_boundary", etc.) |
| `SessionId`         | `string?`                         | Current session ID                                 |
| `SlashCommands`     | `IReadOnlyList<string>?`          | Available slash commands                           |
| `Tools`             | `IReadOnlyList<string>?`          | Available tools                                    |
| `McpServers`        | `IReadOnlyList<McpServerStatus>?` | MCP server connection status                       |
| `Model`             | `string?`                         | Current model                                      |
| `CompactMetadata`   | `CompactMetadata?`                | Compaction details (for compact_boundary)          |
| `IsInit`            | `bool`                            | True if subtype == "init"                          |
| `IsCompactBoundary` | `bool`                            | True if subtype == "compact_boundary"              |

### Skills

Skills extend Claude with specialized capabilities that Claude autonomously invokes when relevant. Skills are defined as `SKILL.md` files (not programmatically).

#### Enabling Skills

```csharp
var options = new ClaudeAgentOptions
{
    WorkingDirectory = "/path/to/project",  // Project with .claude/skills/

    // REQUIRED: Load skills from filesystem
    SettingSources = [SettingSource.Project, SettingSource.User],

    // REQUIRED: Enable the Skill tool
    AllowedTools = ["Skill", "Read", "Write", "Bash"]
};

var client = new ClaudeAgentClient(options);

// Claude automatically invokes relevant skills based on your request
await foreach (var msg in client.QueryAsync("Help me process this PDF document"))
{
    // If a PDF processing skill exists, Claude will use it
}
```

#### Skill Locations

| Location       | Path                          | Loaded When             |
| -------------- | ----------------------------- | ----------------------- |
| Project Skills | `.claude/skills/*/SKILL.md`   | `SettingSource.Project` |
| User Skills    | `~/.claude/skills/*/SKILL.md` | `SettingSource.User`    |

#### Creating Skills

Skills are directories containing a `SKILL.md` file:

**`.claude/skills/pdf-processor/SKILL.md`:**
```markdown
---
description: Extract and process text from PDF documents
---

# PDF Processing Skill

When the user needs to extract text from PDFs:

1. Use `pdftotext` or similar tools to extract content
2. Clean and format the extracted text
3. Return structured results

## Example Usage
- "Extract text from invoice.pdf"
- "Process all PDFs in the documents folder"
```

#### Discovering Available Skills

```csharp
// Ask Claude what skills are available
await foreach (var msg in client.QueryAsync("What Skills are available?"))
{
    if (msg is AssistantMessage assistant)
    {
        // Claude lists available skills based on current directory
    }
}
```

#### Key Points

- **Filesystem-only**: Skills cannot be defined programmatically (unlike Subagents)
- **`SettingSources` required**: Skills won't load without explicit `SettingSources` configuration
- **Auto-invoked**: Claude decides when to use skills based on their `description` field
- **Tool restrictions**: Control available tools via `AllowedTools` in your options

### Plugins

Plugins are packages of Claude Code extensions that can include commands, agents, skills, hooks, and MCP servers.

#### Loading Plugins

```csharp
var options = new ClaudeAgentOptions
{
    Plugins = [
        new PluginConfig { Path = "./my-plugin" },
        new PluginConfig { Path = "/absolute/path/to/another-plugin" }
    ]
};

var client = new ClaudeAgentClient(options);

await foreach (var msg in client.QueryAsync("Hello"))
{
    if (msg is SystemMessage sys && sys.IsInit)
    {
        // Plugin commands, agents, and features are now available
        Console.WriteLine($"Commands: {string.Join(", ", sys.SlashCommands ?? [])}");
        // Example: /help, /compact, my-plugin:custom-command
    }
}
```

#### Using Plugin Commands

Plugin commands are namespaced as `plugin-name:command-name`:

```csharp
// Use a plugin command
await foreach (var msg in client.QueryAsync("/my-plugin:greet"))
{
    // Claude executes the custom greeting command from the plugin
}
```

#### Plugin Structure

Plugins are directories with a `.claude-plugin/plugin.json` manifest:

```
my-plugin/
├── .claude-plugin/
│   └── plugin.json          # Required: plugin manifest
├── commands/                 # Custom slash commands
│   └── custom-cmd.md
├── agents/                   # Custom agents
│   └── specialist.md
├── skills/                   # Agent Skills
│   └── my-skill/
│       └── SKILL.md
├── hooks/                    # Event handlers
│   └── hooks.json
└── .mcp.json                # MCP server definitions
```

#### Multiple Plugins

```csharp
var options = new ClaudeAgentOptions
{
    Plugins = [
        new PluginConfig { Path = "./local-plugin" },
        new PluginConfig { Path = "./project-plugins/team-workflows" },
        new PluginConfig { Path = "~/.claude/custom-plugins/shared-plugin" }
    ]
};
```

#### PluginConfig Properties

| Property | Type     | Description                                     |
| -------- | -------- | ----------------------------------------------- |
| `Type`   | `string` | Plugin type (default: "local")                  |
| `Path`   | `string` | Path to plugin directory (relative or absolute) |

### Structured Outputs

Get responses in a specific JSON schema:

```csharp
var options = new ClaudeAgentOptions
{
    OutputFormat = JsonDocument.Parse("""
    {
        "type": "json_schema",
        "json_schema": {
            "name": "analysis",
            "strict": true,
            "schema": {
                "type": "object",
                "properties": {
                    "summary": { "type": "string" },
                    "sentiment": {
                        "type": "string",
                        "enum": ["positive", "negative", "neutral"]
                    },
                    "score": { "type": "number" }
                },
                "required": ["summary", "sentiment", "score"],
                "additionalProperties": false
            }
        }
    }
    """).RootElement
};

var client = new ClaudeAgentClient(options);

await foreach (var msg in client.QueryAsync("Analyze: I love this product!"))
{
    if (msg is AssistantMessage assistant)
    {
        var text = assistant.MessageContent.Content.OfType<TextBlock>().First();
        var result = JsonSerializer.Deserialize<AnalysisResult>(text.Text);
        Console.WriteLine($"Sentiment: {result.Sentiment}, Score: {result.Score}");
    }
}

record AnalysisResult(string Summary, string Sentiment, double Score);
```

### Type-Safe Structured Outputs (Recommended)

Use the `SchemaGenerator` to auto-generate JSON schemas from C# types:

```csharp
using Claude.AgentSdk.Schema;

// Define your output type with descriptions
[Description("Analysis of text sentiment")]
public record SentimentAnalysis
{
    [SchemaDescription("Brief summary of the text")]
    public required string Summary { get; init; }

    [SchemaDescription("Overall sentiment of the text")]
    public required Sentiment Sentiment { get; init; }

    [SchemaDescription("Confidence score from 0-1")]
    public required double Confidence { get; init; }
}

public enum Sentiment { Positive, Negative, Neutral }

// Generate schema automatically
var schema = SchemaGenerator.Generate<SentimentAnalysis>("sentiment_analysis");

// Or use the fluent extension method
var options = new ClaudeAgentOptions { Model = "sonnet" }
    .WithStructuredOutput<SentimentAnalysis>();

var client = new ClaudeAgentClient(options);

await foreach (var msg in client.QueryAsync("Analyze: I love this product!"))
{
    if (msg is AssistantMessage assistant)
    {
        // Type-safe parsing
        var result = assistant.ParseStructuredOutput<SentimentAnalysis>();
        Console.WriteLine($"Sentiment: {result?.Sentiment}, Confidence: {result?.Confidence}");
    }
}
```

### Hooks

Hooks let you intercept agent execution at key points to add validation, logging, security controls, or custom logic.

#### Available Hook Events

| Hook Event           | Description                             | Use Case                   |
| -------------------- | --------------------------------------- | -------------------------- |
| `PreToolUse`         | Before tool executes (can block/modify) | Block dangerous commands   |
| `PostToolUse`        | After tool executes successfully        | Log file changes           |
| `PostToolUseFailure` | When tool execution fails               | Handle errors              |
| `UserPromptSubmit`   | When user prompt is submitted           | Inject context             |
| `Stop`               | When agent execution stops              | Save session state         |
| `SubagentStart`      | When subagent initializes               | Track parallel tasks       |
| `SubagentStop`       | When subagent completes                 | Aggregate results          |
| `PreCompact`         | Before conversation compaction          | Archive transcript         |
| `PermissionRequest`  | When permission dialog would show       | Custom permission handling |
| `SessionStart`       | When session initializes                | Initialize telemetry       |
| `SessionEnd`         | When session terminates                 | Clean up resources         |
| `Notification`       | For agent status messages               | Send to Slack/PagerDuty    |

#### Basic Hook Example

```csharp
var options = new ClaudeAgentOptions
{
    Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
    {
        [HookEvent.PreToolUse] = new[]
        {
            new HookMatcher
            {
                Matcher = "Bash",  // Only for Bash tool (regex pattern)
                Hooks = new HookCallback[]
                {
                    async (input, toolUseId, context, ct) =>
                    {
                        if (input is PreToolUseHookInput pre)
                        {
                            Console.WriteLine($"About to run: {pre.ToolInput}");
                        }
                        return new SyncHookOutput { Continue = true };
                    }
                }
            }
        }
    }
};
```

#### Block Dangerous Operations

```csharp
var options = new ClaudeAgentOptions
{
    Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
    {
        [HookEvent.PreToolUse] = new[]
        {
            new HookMatcher
            {
                Matcher = "Write|Edit",  // Match file modification tools
                Hooks = new HookCallback[]
                {
                    async (input, toolUseId, context, ct) =>
                    {
                        if (input is PreToolUseHookInput pre)
                        {
                            var filePath = pre.ToolInput.GetProperty("file_path").GetString();
                            if (filePath?.EndsWith(".env") == true)
                            {
                                return new SyncHookOutput
                                {
                                    HookSpecificOutput = JsonSerializer.SerializeToElement(new
                                    {
                                        hookEventName = pre.HookEventName,
                                        permissionDecision = "deny",
                                        permissionDecisionReason = "Cannot modify .env files"
                                    })
                                };
                            }
                        }
                        return new SyncHookOutput { Continue = true };
                    }
                }
            }
        }
    }
};
```

#### Session Lifecycle Hooks

```csharp
var options = new ClaudeAgentOptions
{
    Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
    {
        [HookEvent.SessionStart] = new[]
        {
            new HookMatcher
            {
                Hooks = new HookCallback[]
                {
                    async (input, toolUseId, context, ct) =>
                    {
                        if (input is SessionStartHookInput start)
                        {
                            Console.WriteLine($"Session started: {start.Source}");
                        }
                        return new SyncHookOutput { Continue = true };
                    }
                }
            }
        },
        [HookEvent.SessionEnd] = new[]
        {
            new HookMatcher
            {
                Hooks = new HookCallback[]
                {
                    async (input, toolUseId, context, ct) =>
                    {
                        if (input is SessionEndHookInput end)
                        {
                            Console.WriteLine($"Session ended: {end.Reason}");
                        }
                        return new SyncHookOutput { Continue = true };
                    }
                }
            }
        },
        [HookEvent.Notification] = new[]
        {
            new HookMatcher
            {
                Hooks = new HookCallback[]
                {
                    async (input, toolUseId, context, ct) =>
                    {
                        if (input is NotificationHookInput notification)
                        {
                            Console.WriteLine($"[{notification.NotificationType}] {notification.Message}");
                        }
                        return new SyncHookOutput { Continue = true };
                    }
                }
            }
        }
    }
};
```

#### Hook Input Types

| Input Type                    | Properties                                         |
| ----------------------------- | -------------------------------------------------- |
| `PreToolUseHookInput`         | `ToolName`, `ToolInput`                            |
| `PostToolUseHookInput`        | `ToolName`, `ToolInput`, `ToolResponse`            |
| `PostToolUseFailureHookInput` | `ToolName`, `ToolInput`, `Error`, `IsInterrupt`    |
| `UserPromptSubmitHookInput`   | `Prompt`                                           |
| `StopHookInput`               | `StopHookActive`                                   |
| `SubagentStartHookInput`      | `AgentId`, `AgentType`                             |
| `SubagentStopHookInput`       | `StopHookActive`, `AgentId`, `AgentTranscriptPath` |
| `PreCompactHookInput`         | `Trigger`, `CustomInstructions`                    |
| `PermissionRequestHookInput`  | `ToolName`, `ToolInput`, `PermissionSuggestions`   |
| `SessionStartHookInput`       | `Source`                                           |
| `SessionEndHookInput`         | `Reason`                                           |
| `NotificationHookInput`       | `Message`, `NotificationType`, `Title`             |

All input types also include common fields: `SessionId`, `TranscriptPath`, `Cwd`, `PermissionMode`.

## v1 Behavioral Contract

This section describes the expected runtime behavior of the SDK in v1.

### Lifetimes

- `ClaudeAgentClient` is **stateless**. It does not own long-lived resources.
- `ClaudeAgentSession` **owns the connection** to the Claude CLI and must be disposed when finished.

### Cancellation & disposal

- Disposing a `ClaudeAgentSession` cancels the session's internal cancellation token and initiates shutdown of the underlying transport.
- Any active message enumeration from `ReceiveAsync()` / `ReceiveResponseAsync()` is expected to stop when the session is disposed.

### Streaming & backpressure

- Bidirectional sessions use a **bounded internal buffer** for messages.
- If you do **not** enumerate the receive stream, the SDK may apply backpressure and message processing can appear to stall.
  For interactive scenarios, always run a receive loop (even if you ignore messages).

### Errors

- If the underlying transport/protocol fails, the message stream will typically **fault** (throw) during `await foreach`.
- `OperationCanceledException` is expected when you cancel/dispose a session.
- For diagnostics, `ClaudeAgentSession.TerminalException` may be set after the stream ends.

## API Reference

### ClaudeAgentClient

| Method                                         | Description                                                  |
| ---------------------------------------------- | ------------------------------------------------------------ |
| `QueryAsync(prompt, options?, ct)`             | Execute a one-shot query, streaming responses                |
| `QueryToCompletionAsync(prompt, options?, ct)` | Execute and wait for final result                            |
| `CreateSessionAsync(ct)`                       | Create a bidirectional session (returns `ClaudeAgentSession`) |

### ClaudeAgentSession

| Method                             | Description                           |
| ---------------------------------- | ------------------------------------- |
| `SendAsync(content, sessionId?)`   | Send message in bidirectional mode    |
| `ReceiveAsync(ct)`                 | Receive all messages (continuous)     |
| `ReceiveResponseAsync(ct)`         | Receive until ResultMessage (typical) |
| `InterruptAsync(ct)`               | Send interrupt signal                 |
| `CancelAsync(ct)`                  | Cancel the session                    |
| `SetPermissionModeAsync(mode, ct)` | Change permission mode                |
| `SetModelAsync(model, ct)`         | Change model                          |

### ClaudeAgentOptions

| Property                 | Type                            | Description                                           |
| ------------------------ | ------------------------------- | ----------------------------------------------------- |
| `Model`                  | `string?`                       | Model to use (sonnet, opus, haiku)                    |
| `MaxTurns`               | `int?`                          | Maximum conversation turns                            |
| `SystemPrompt`           | `SystemPromptConfig?`           | System prompt (string, preset, or preset with append) |
| `SettingSources`         | `IReadOnlyList<SettingSource>?` | Sources for loading CLAUDE.md files                   |
| `Tools`                  | `IReadOnlyList<string>?`        | Tools to enable                                       |
| `AllowedTools`           | `IReadOnlyList<string>`         | Additional allowed tools                              |
| `DisallowedTools`        | `IReadOnlyList<string>`         | Tools to disable                                      |
| `WorkingDirectory`       | `string?`                       | Working directory for agent                           |
| `CliPath`                | `string?`                       | Path to Claude CLI (default: search PATH)             |
| `CanUseTool`             | `Func<...>?`                    | Permission callback                                   |
| `Hooks`                  | `IReadOnlyDictionary<...>?`     | Hook configurations                                   |
| `McpServers`             | `IReadOnlyDictionary<...>?`     | MCP server configurations                             |
| `OutputFormat`           | `JsonElement?`                  | Structured output schema                              |
| `PermissionMode`         | `PermissionMode?`               | Permission mode                                       |
| `MaxThinkingTokens`      | `int?`                          | Max tokens for thinking                               |
| `IncludePartialMessages` | `bool`                          | Include streaming partial messages                    |

### SystemPromptConfig Types

| Type                 | Description                                      |
| -------------------- | ------------------------------------------------ |
| `CustomSystemPrompt` | Custom string prompt (replaces default entirely) |
| `PresetSystemPrompt` | Preset configuration with optional append text   |

### SettingSource Values

| Value     | Description                                               |
| --------- | --------------------------------------------------------- |
| `Project` | Load CLAUDE.md from project directory                     |
| `User`    | Load ~/.claude/CLAUDE.md (user-level)                     |
| `Local`   | Load CLAUDE.local.md from project (gitignored local file) |

### Message Types

| Type               | Description                           |
| ------------------ | ------------------------------------- |
| `UserMessage`      | User input message                    |
| `AssistantMessage` | Claude's response with content blocks |
| `SystemMessage`    | System metadata message               |
| `ResultMessage`    | Final result with cost/usage info     |
| `StreamEvent`      | Partial streaming update              |

### Content Blocks

| Type              | Description               |
| ----------------- | ------------------------- |
| `TextBlock`       | Text content              |
| `ThinkingBlock`   | Extended thinking content |
| `ToolUseBlock`    | Tool invocation request   |
| `ToolResultBlock` | Tool execution result     |

## Architecture

```
┌─────────────────────────────────────────┐
│         ClaudeAgentClient               │
│  - QueryAsync() / CreateSessionAsync()  │
├─────────────────────────────────────────┤
│          ClaudeAgentSession             │
│  - SendAsync() / ReceiveAsync()         │
│  - Bidirectional communication          │
├─────────────────────────────────────────┤
│           QueryHandler                  │
│  - Control protocol routing             │
│  - Permission/hook handling             │
│  - MCP tool dispatch                    │
├─────────────────────────────────────────┤
│        SubprocessTransport              │
│  - CLI process management               │
│  - JSONL stdin/stdout I/O               │
├─────────────────────────────────────────┤
│         Claude Code CLI                 │
│  (external binary - handles API calls)  │
└─────────────────────────────────────────┘
```

## Examples

The SDK includes several example projects demonstrating different features and use cases.

### Quick Start Examples

**Claude.AgentSdk.Examples** - Interactive menu with 11 SDK feature demonstrations:

```bash
cd examples/Claude.AgentSdk.Examples
dotnet run              # Shows interactive menu
dotnet run -- 1         # Run specific example by number
```

Examples included:
1. Basic Query - Simple one-shot query
2. Streaming - Stream responses as they arrive
3. Interactive Session - Bidirectional conversation
4. Custom Tools - MCP SDK tools in C#
5. Hooks - PreToolUse/PostToolUse hooks
6. Subagents - Spawning specialized subagents
7. Structured Output - JSON schema responses
8. Permission Handler - Tool permission callbacks
9. System Prompt - Custom and preset prompts
10. Settings Sources - Loading CLAUDE.md files
11. MCP Servers - External MCP server configuration

### Standalone Examples

**HelloWorld** - Basic query with file restriction hooks:
```bash
cd examples/Claude.AgentSdk.HelloWorld
dotnet run                          # Default greeting
dotnet run -- "Your prompt here"    # Custom prompt
```

**SimpleChatApp** - Multi-turn interactive chat:
```bash
cd examples/Claude.AgentSdk.SimpleChatApp
dotnet run    # Interactive REPL with /clear and /exit commands
```

**ResearchAgent** - Multi-agent orchestration with researcher and report-writer subagents:
```bash
cd examples/Claude.AgentSdk.ResearchAgent
dotnet run                    # Interactive mode
dotnet run -- --auto          # Auto-run with default prompt
dotnet run -- "Your topic"    # Research specific topic
```

**ResumeGenerator** - Web search and document generation:
```bash
cd examples/Claude.AgentSdk.ResumeGenerator
dotnet run -- "Person Name"   # Generate resume for a person
```

**EmailAgent** - Custom MCP tools for email management (mock inbox):
```bash
cd examples/Claude.AgentSdk.EmailAgent
dotnet run    # Interactive email assistant
```

**ExcelAgent** - Custom MCP tools for Excel spreadsheet creation:
```bash
cd examples/Claude.AgentSdk.ExcelAgent
dotnet run    # Interactive spreadsheet builder
```

**SignalR** - ASP.NET Core web app with real-time Claude integration:
```bash
cd examples/Claude.AgentSdk.SignalR
dotnet run    # Starts server at http://localhost:5000
```

**SubagentTest** - Diagnostic tool for testing subagent configurations:
```bash
cd examples/Claude.AgentSdk.SubagentTest
dotnet run                    # Standard test
dotnet run -- --diagnostic    # Full diagnostic with hooks
dotnet run -- --cli-args      # Show CLI arguments
```

## License

MIT
