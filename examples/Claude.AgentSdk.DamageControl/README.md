# Claude.AgentSdk.DamageControl

A sample project demonstrating defense-in-depth protection for Claude agent sessions using the AgentSDK's PreToolUse hooks.

## Attribution

This sample is a C# port of the [claude-code-damage-control](https://github.com/disler/claude-code-damage-control) project by [IndyDevDan (disler)](https://github.com/disler). The security patterns (`patterns.yaml`) are copied from the original repository.

> Note: There are know issues with this approach, see the original repo for details.

## Overview

DamageControl provides multiple layers of protection by:

1. **Blocking dangerous bash commands** - `rm -rf`, `git reset --hard`, `terraform destroy`, etc.
2. **Protecting sensitive paths** with three access levels:
   - **Zero Access**: No operations allowed (e.g., `~/.ssh/`, `~/.aws/`, `.env` files)
   - **Read Only**: Read allowed, modifications blocked (e.g., `/etc/`, lock files)
   - **No Delete**: All operations except delete (e.g., `.git/`, `LICENSE`, `README.md`)
3. **Triggering confirmation dialogs** for risky-but-valid operations ("ask" patterns)

## Important: Dual Permission System

When running with a Claude agent, there are **two layers of protection**:

1. **DamageControl Hooks** - Your custom security patterns (this project)
2. **Claude Code's Built-in Permission System** - Claude's own safety checks

If DamageControl **allows** a command, Claude's built-in system may still prompt for confirmation. This is by design - defense in depth.

```
User: rm -rf ./file.txt
  ↓
DamageControl: BLOCKED (rm with recursive flags)
  ↓
Claude retries: rm ./file.txt
  ↓
DamageControl: ALLOWED (no dangerous flags)
  ↓
Claude's Permission System: "Do you want to run this command?" (additional safety layer)
```

To reduce Claude's built-in prompts during testing, you can adjust `PermissionMode`:

```csharp
var options = new ClaudeAgentOptions
{
    PermissionMode = PermissionMode.AcceptEdits,  // Less restrictive
    // or PermissionMode.Dangerously              // Bypass built-in checks (use with caution!)
    Hooks = damageControl.GetHooksCompiled()
};
```

## Usage

### Interactive Test Mode

Run the sample and select option `[1]` to test commands without running Claude:

```
> rm -rf /tmp/test
  [X] BLOCKED: rm with recursive or force flags

> git status
  [OK] ALLOWED

> git checkout -- .
  [?] CONFIRMATION REQUIRED: Discards all uncommitted changes
```

### With Claude Agent

```csharp
using Claude.AgentSdk;
using Claude.AgentSdk.DamageControl.Hooks;
using Claude.AgentSdk.DamageControl.Security;

// Load security configuration
var config = SecurityConfigLoader.Load();

// Create hooks
var damageControl = new DamageControlHooks(config);

// Configure agent with hooks
var options = new ClaudeAgentOptions
{
    SystemPrompt = "You are a helpful assistant.",
    Hooks = damageControl.GetHooksCompiled()
};

await using var client = new ClaudeAgentClient(options);
await foreach (var message in client.QueryAsync("Your prompt here"))
{
    // Handle messages
}
```

## Project Structure

```
Claude.AgentSdk.DamageControl/
├── Program.cs                    # Entry point with interactive demo
├── patterns.yaml                 # Security patterns (from original repo)
├── Hooks/
│   └── DamageControlHooks.cs     # PreToolUse hook handlers
└── Security/
    ├── CheckResult.cs            # Block/Ask/Allow result type
    ├── CommandChecker.cs         # Bash command pattern matching
    ├── PathMatcher.cs            # File path protection matching
    ├── SecurityConfig.cs         # YAML configuration model
    └── SecurityConfigLoader.cs   # Configuration loading
```

## Security Patterns

The `patterns.yaml` file contains 100+ patterns organized by category:

### Bash Command Patterns
- Destructive file operations (`rm -rf`, `sudo rm`)
- Permission changes (`chmod 777`, `chown -R root`)
- Git destructive operations (`git reset --hard`, `git push --force`)
- Cloud CLI (`aws`, `gcloud`, `firebase`, `vercel`, `netlify`)
- Infrastructure as Code (`terraform destroy`, `pulumi destroy`)
- Database operations (`DROP TABLE`, `TRUNCATE`, `FLUSHALL`)
- Container operations (`docker system prune`, `kubectl delete namespace`)

### Path Protection
- **Zero Access**: `.env`, `~/.ssh/`, `~/.aws/`, `*.pem`, `*.tfstate`
- **Read Only**: `/etc/`, `package-lock.json`, `node_modules/`, `*.min.js`
- **No Delete**: `LICENSE`, `README.md`, `.git/`, `.github/`, `Dockerfile`

## Customization

Edit `patterns.yaml` to add or modify security patterns:

```yaml
bashToolPatterns:
  - pattern: '\bmy-dangerous-command\b'
    reason: Custom dangerous command
    ask: false  # true = confirmation, false = block

zeroAccessPaths:
  - "my-secrets/"
  - "*.secret"

readOnlyPaths:
  - "generated/"

noDeletePaths:
  - "important-file.txt"
```

## How It Works

The sample uses the `[GenerateHookRegistration]` source generator to create hook registrations at compile time:

```csharp
[GenerateHookRegistration]
public partial class DamageControlHooks
{
    [HookHandler(HookEvent.PreToolUse, Matcher = "Bash")]
    public Task<HookOutput> OnBashToolUse(HookInput input, ...) { ... }

    [HookHandler(HookEvent.PreToolUse, Matcher = "Edit")]
    public Task<HookOutput> OnEditToolUse(HookInput input, ...) { ... }
}
```

The generated `GetHooksCompiled()` method returns a dictionary that can be passed directly to `ClaudeAgentOptions.Hooks`.

## License

This sample follows the license of the Claude.AgentSdk project. The security patterns are derived from the [claude-code-damage-control](https://github.com/disler/claude-code-damage-control) project by [disler](https://github.com/disler).
