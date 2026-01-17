# AJGit.Claude.AgentSdk.Extensions.DependencyInjection

ASP.NET Core dependency injection extensions for [Claude.AgentSdk](https://www.nuget.org/packages/AJGit.Claude.AgentSdk).

## Installation

```bash
dotnet add package AJGit.Claude.AgentSdk.Extensions.DependencyInjection
```

## Features

- `AddClaudeAgent()` extension methods for `IServiceCollection`
- Configuration binding from `appsettings.json` via `IOptions<T>`
- Named agent instances for multi-agent scenarios
- MCP tool server registration with DI
- Health checks for agent availability
- OpenTelemetry metrics and tracing

## Quick Start

### Basic Registration

```csharp
services.AddClaudeAgent(options =>
{
    options.Model = "sonnet";
    options.MaxTurns = 10;
    options.AllowedTools = ["Read", "Write", "Bash"];
});
```

### Configuration from appsettings.json

```csharp
services.AddClaudeAgent(configuration.GetSection("Claude"));
```

```json
{
  "Claude": {
    "Model": "sonnet",
    "MaxTurns": 10,
    "MaxBudgetUsd": 1.0,
    "WorkingDirectory": "C:/Projects/MyApp",
    "AllowedTools": ["Read", "Write", "Bash"],
    "SystemPrompt": "You are a helpful assistant.",
    "PermissionMode": "AcceptEdits"
  }
}
```

### Named Instances

```csharp
// Register multiple agents with different configurations
services.AddClaudeAgent("analyzer", options =>
{
    options.Model = "sonnet";
    options.SystemPrompt = "You analyze code for issues.";
});

services.AddClaudeAgent("generator", options =>
{
    options.Model = "opus";
    options.SystemPrompt = "You generate high-quality code.";
});

// Resolve via factory
public class MyService
{
    private readonly IClaudeAgentClientFactory _factory;

    public MyService(IClaudeAgentClientFactory factory)
    {
        _factory = factory;
    }

    public async Task AnalyzeAsync()
    {
        var analyzer = _factory.CreateClient("analyzer");
        // ...
    }
}
```

### MCP Tool Servers

```csharp
services.AddClaudeAgent(options => options.Model = "sonnet")
    .AddMcpServer("tools", myToolServer)
    .AddMcpServer<MyToolServer>("custom");
```

### Health Checks

```csharp
services.AddHealthChecks()
    .AddClaudeAgentCheck();
```

## Dependencies

This package depends on:
- `AJGit.Claude.AgentSdk` (core SDK)
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Options.ConfigurationExtensions`
- `Microsoft.Extensions.Hosting.Abstractions`
- `Microsoft.Extensions.Diagnostics.HealthChecks`

## License

MIT
