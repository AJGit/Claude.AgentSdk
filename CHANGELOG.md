# Changelog

All notable changes to the Claude.AgentSdk project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased] - September 2026 Parity Update

Brings the C# SDK to feature parity with the Python SDK v0.2.160 (from v0.1.33). CLI flags were verified
against Claude Code CLI 2.1.281.

### Added

#### Options
- `PermissionMode.Auto` (model classifier approves/denies each tool call)
- `Thinking` (`ThinkingConfig.Adaptive()/Enabled(n)/Disabled`, optional `ThinkingDisplay`) → `--thinking` / `--thinking-display`; takes precedence over `MaxThinkingTokens`
- `Effort` (`EffortLevel`: low, medium, high, xhigh, max) → `--effort`
- `TaskBudget` → `--task-budget`
- `Skills` (`SkillsConfig.All`, `SkillsConfig.Only(...)`, `SkillsConfig.None`): adds `Skill` rules to allowed tools, defaults setting sources to user+project, sends the allowlist via initialize; skill names are validated to prevent `--allowedTools` injection
- `Settings` (settings file path or inline JSON) → `--settings`, merged with sandbox settings
- `IncludeHookEvents`, `ForwardSubagentText`, `VerbatimPrompts` (marks user messages `client_composed` so `@path` and slash commands are not expanded), `ResumeDropsTurn`, `InitializeTimeout`
- `FileSystemPrompt` / `SystemPromptConfig.FromFile()` → `--system-prompt-file`; `PresetSystemPrompt.ExcludeDynamicSections` and `Snapshot`; `CustomSystemPrompt.Snapshot`
- `AgentDefinition`: `InitialPrompt`, `Memory`, `Background`, `Effort`, `PermissionMode`, `McpServerNames`
- `NetworkSandboxSettings.DeniedDomains`, `AllowMachLookup` (and `AllowedDomains`/`AllowManagedDomainsOnly` are now actually serialized)

#### Messages
- `RateLimitEvent` (`rate_limit_event`) and `ConversationResetMessage` (`conversation_reset`)
- `ServerToolUseBlock` (`server_tool_use`) and `ServerToolResultBlock` (`advisor_tool_result`); unknown content block types are skipped instead of dropping the whole message
- `MessageOrigin` on `UserMessage.Origin` and `ResultMessage.Origin`
- `ResultMessage`: `ModelUsage` (typed per-model usage), `PermissionDenials`, `DeferredToolUse`, `Errors`, `ApiErrorStatus`, `Uuid`, `TerminalReason`
- `AssistantMessage`: top-level `ParentToolUseId`, `Error`, `SessionId`, `Uuid`; `AssistantMessageContent.Id`, `StopReason`, `Usage`
- `UserMessage`: top-level `Uuid`, `ParentToolUseId`, `ToolUseResult`, `SessionId`
- `Message.RawJson` with the full raw payload
- Typed system payloads `TaskStartedData`, `TaskProgressData`, `TaskUpdatedData`, `SessionStateChangedData`, `TaskUsage`; `TaskNotificationData` gains `ToolUseId`, `Usage`, `Uuid`, `SessionId`; `TaskStatuses.IsTerminal()`
- `SystemMessageSubtype`: `TaskStarted`, `TaskProgress`, `TaskUpdated`, `SessionStateChanged`

#### Session & protocol
- `ClaudeAgentSession.GetContextUsageAsync()` / `GetContextUsageTypedAsync()` (`ContextUsage`), `StopTaskAsync()`, `ServerInfo` (initialize response)
- `SetModelAsync(null)` resets to the default model
- `ToolPermissionRequest`: `ToolUseId`, `AgentId`, `DecisionReason`, `Title`, `DisplayName`, `Description`; `Suggestions` are now parsed
- `PermissionUpdate.ToWireFormat()` / `FromWireFormat()`
- Hook inputs: `AgentId` / `AgentType` on tool-lifecycle and PermissionRequest hooks; Setup, TeammateIdle and TaskCompleted inputs are now dispatched
- Typed hook-specific outputs (`PreToolUseHookSpecificOutput` with `PermissionDecision` incl. "defer", `PostToolUseHookSpecificOutput.UpdatedToolOutput`, `PermissionRequestHookSpecificOutput`, ...) with `ToJsonElement()`
- `ResultException` (structured error-result payload when the CLI exits after an error result) and `ControlRequestException`
- MCP tools: `ToolAnnotations.MaxResultSizeChars` (sent as `_meta["anthropic/maxResultSizeChars"]`), `ToolResult.Image()` / `ToolResultImageContent`, `ping`, JSON-RPC notification acknowledgement
- `Claude.AgentSdk.Sessions.ClaudeSessions`: `ListSessions`, `GetSessionInfo`, `GetSessionMessages`, `ListSubagents`, `GetSubagentMessages`, `RenameSession`, `TagSession`, `DeleteSession` (cascades to subagent transcripts), `ForkSession`, `ProjectKeyForDirectory`

#### Session store
- `ClaudeAgentOptions.SessionStore` (`ISessionStore`), `SessionStoreFlush` (`SessionStoreFlushMode.Batched`/`Eager`) and `SessionStoreLoadTimeout`: transcripts are mirrored to the store via `--session-mirror` / `transcript_mirror` frames (flushed before each result is yielded and at shutdown; 500-entry / 1 MiB overflow flushes; 3 append attempts with backoff, timeouts not retried); dropped batches surface as a `mirror_error` system message (`MirrorErrorData`, `SystemMessageSubtype.MirrorError`)
- Store-backed resume: with `Resume` or `ContinueConversation`, the session (plus subagent transcripts and `.meta.json` sidecars) is loaded into a temporary `CLAUDE_CONFIG_DIR`, seeded with the caller's `.credentials.json` (refresh token redacted; macOS Keychain fallback), `.claude.json` and user settings (plugin declarations and `env.CLAUDE_CONFIG_DIR` stripped); the directory is removed after the CLI exits. Unsafe subpaths from the store are skipped; store failures raise `SessionStoreException`
- `ISessionStore` (required append/load) with optional capability interfaces `ISessionStoreListing`, `ISessionStoreSummaries`, `ISessionStoreDeletion`, `ISessionStoreSubkeys`; `SessionKey` (with `FromTranscriptPath`), `SessionStoreListEntry`, `SessionSummaryEntry`
- `InMemorySessionStore` reference implementation; `SessionSummary.Fold()` for incrementally maintained summaries (same `data` keys as the Python/TypeScript SDKs)
- Store-backed helpers: `ListSessionsFromStoreAsync` (summary fast path with gap-fill), `GetSessionInfoFromStoreAsync`, `GetSessionMessagesFromStoreAsync`, `ListSubagentsFromStoreAsync`, `GetSubagentMessagesFromStoreAsync`, `RenameSessionViaStoreAsync`, `TagSessionViaStoreAsync`, `DeleteSessionViaStoreAsync`, `ForkSessionViaStoreAsync`, `ImportSessionToStoreAsync`
- `Claude.AgentSdk.Testing.SessionStoreConformance.RunAsync()`: the 14 adapter contracts, for third-party store authors
- Validation: `ContinueConversation` with a store requires `ISessionStoreListing`; a store cannot be combined with `EnableFileCheckpointing`

### Changed

- One-shot queries now always use the streaming protocol (initialize + prompt over stdin, never `--print`), so hooks, `CanUseTool` and agents work with `QueryAsync`. The stream stays open for follow-up turns from background agents until the CLI reports `idle` (bounded by `CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS`, default 10 minutes)
- Incoming control requests are handled concurrently and honor `control_cancel_request`
- CLI command line matches the Python SDK: `--resume=`, `--session-id=`, `--resume-session-at=` use the `=` form (with a Windows cmd.exe metacharacter guard); dash-leading `ExtraArgs` values use `--flag=value`; null system prompt passes `--system-prompt ""`; the preset only passes `--append-system-prompt`; `ToolsPreset` → `--tools default`; `--setting-sources=` (an empty list disables all sources); `--fork-session` is forwarded whenever set
- Agents are sent via the initialize request instead of `--agents`; plugins via `--plugin-dir` instead of `--plugins`; sandbox via `--settings {"sandbox": ...}` instead of `--sandbox`/`--sandbox-config` (the old flags were rejected by the CLI)
- `CanUseTool` automatically sets `--permission-prompt-tool stdio`; combining it with `PermissionPromptToolName` throws `ArgumentException`; a warning is logged when `AllowedTools`/bypassPermissions shadow the callback
- Control subtypes fixed to the CLI's names: `mcp_status`, `mcp_reconnect`/`mcp_toggle` (with `serverName`), `mcp_set_servers`; supported commands/models/account come from the initialize response
- Permission responses use `updatedInput` (original input echoed when not changed) and camelCase `updatedPermissions`
- Error control responses (instead of silent allow/continue) for: `can_use_tool` without a callback, unknown hook callback ids, hook callbacks that throw, unsupported subtypes; `control_response` errors now fail the pending call
- Control request results return the inner response payload (fixes `RewindFilesAsync`, `SetMcpServersAsync` typed results); `GetMcpServerStatusTypedAsync` unwraps `{"mcpServers": [...]}`; `McpServerStatusInfo.Tools` is `IReadOnlyList<McpToolInfo>`
- `SystemMessage.GetData<T>()` reads top-level fields (the CLI does not nest them under `data`)
- Transport: non-JSON stdout lines are skipped; a non-zero CLI exit raises `CliProcessException`; `OnStderr` exceptions no longer stop stderr delivery; CLI children are killed on host exit; `CLAUDECODE` is removed from the child env; `CLAUDE_CODE_ENTRYPOINT` is overridable; `CLAUDE_AGENT_SDK_VERSION` and W3C `TRACEPARENT`/`TRACESTATE` (from `Activity.Current`) are set
- Windows: a native `claude.exe` anywhere on PATH (or `~/.local/bin`) is preferred over npm's `claude.cmd`; batch shims run via `node cli.js` or are refused (the `cmd.exe /c` fallback was removed, CVE-2024-27980 class)
- `NoHooks` is applied via `disableAllHooks` settings and by not registering SDK hooks; `AdditionalDataPaths` maps to `--add-dir`; `User` is no longer forwarded (`--user`, `--no-hooks` and `--additional-data-path` are not CLI options and made the CLI exit)
- Default control request timeout is 60 seconds (was 30)

### Not ported

- The Python repo's example S3 / Redis / Postgres store adapters (not shipped in the Python package either)
- Python-specific internals (anyio/trio compatibility, `mcp` 2.x bridge, bundled CLI discovery, CLI version check)

## February 2026 Parity Update

Brings the C# SDK to feature parity with the Python SDK (v0.1.33) and TypeScript SDK (v0.2.37).

### Added

#### Hook System
- New `HookEvent.Setup` event that fires on init/maintenance triggers
- New `HookEvent.TeammateIdle` event that fires when a teammate becomes idle
- New `HookEvent.TaskCompleted` event that fires when a task completes
- `SetupHookInput` record with `Trigger` property ("init" | "maintenance")
- `TeammateIdleHookInput` record with `TeammateName` and `TeamName` properties
- `TaskCompletedHookInput` record with `TaskId`, `TaskSubject`, `TaskDescription`, `TeammateName`, and `TeamName` properties
- `ToolUseId` field on `PreToolUseHookInput`, `PostToolUseHookInput`, and `PostToolUseFailureHookInput`
- `AgentType` field on `SubagentStopHookInput`
- `AgentType` and `Model` fields on `SessionStartHookInput`
- 7 hook-specific output records: `PreToolUseHookSpecificOutput`, `PostToolUseHookSpecificOutput`, `NotificationHookSpecificOutput`, `SessionStartHookSpecificOutput`, `SetupHookSpecificOutput`, `SubagentStartHookSpecificOutput`, `PostToolUseFailureHookSpecificOutput`

#### Message Types
- `ToolProgressMessage` - real-time tool execution progress updates with `ToolUseId`, `ToolName`, `ElapsedTimeSeconds`
- `ToolUseSummaryMessage` - summarizes preceding tool uses with `Summary` and `PrecedingToolUseIds`
- `AuthStatusMessage` - authentication status with `IsAuthenticating`, `Output`, and `Error`
- `MessageType.ToolProgress`, `MessageType.ToolUseSummary`, `MessageType.AuthStatus` enum values
- `StopReason` field on `ResultMessage`
- `WebSearchRequests` field on `UsageInfo`
- `ToolUseResult` field on `UserMessageContent`

#### System Message Data Records (new file: `Messages/SystemMessageData.cs`)
- `StatusMessageData` with `Status` and `PermissionMode`
- `HookStartedData` with `HookId`, `HookName`, `HookEvent`
- `HookProgressData` with `HookId`, `HookName`, `HookEvent`, `Stdout`, `Stderr`, `Output`
- `HookResponseData` with `HookId`, `HookName`, `HookEvent`, `Output`, `Stdout`, `Stderr`, `ExitCode`, `Outcome`
- `TaskNotificationData` with `TaskId`, `Status`, `OutputFile`, `Summary`
- `FilesPersistedData` with nested `PersistedFile` and `FailedFile` records
- `GetData<T>()` extension method on `SystemMessage` for typed data extraction

#### System Message Subtypes
- `SystemMessageSubtype.Status`
- `SystemMessageSubtype.HookStarted`
- `SystemMessageSubtype.HookProgress`
- `SystemMessageSubtype.HookResponse`
- `SystemMessageSubtype.TaskNotification`
- `SystemMessageSubtype.FilesPersisted`

#### Result Message Subtypes
- `ResultMessageSubtype.ErrorDuringExecution`
- `ResultMessageSubtype.ErrorMaxTurns`
- `ResultMessageSubtype.ErrorMaxBudget`
- `ResultMessageSubtype.ErrorMaxStructuredOutputRetries`

#### Agent Definition Enhancements
- `Skills` field (`IReadOnlyList<string>?`)
- `MaxTurns` field (`int?`)
- `DisallowedTools` field (`IReadOnlyList<string>?`)
- `McpServers` field (`IReadOnlyDictionary<string, McpServerConfig>?`)
- `CriticalSystemReminderExperimental` field (`string?`)
- Corresponding builder methods: `WithSkills()`, `WithMaxTurns()`, `WithDisallowedTools()`, `WithMcpServers()`, `WithCriticalSystemReminder()`

#### Options & Session
- `ClaudeAgentOptions.SessionId` - custom UUID for conversations
- `ClaudeAgentOptions.Debug` - enable debug logging
- `ClaudeAgentOptions.DebugFile` - path for debug output
- `ClaudeAgentOptions.PersistSession` - persist session state
- Corresponding builder methods: `WithSessionId()`, `WithDebug()`, `WithDebugFile()`, `WithPersistSession()`
- `PermissionMode.Delegate` enum value

#### Session Methods
- `ClaudeAgentSession.SetMaxThinkingTokensAsync()` - dynamically set max thinking tokens
- `ClaudeAgentSession.ReconnectMcpServerAsync()` - reconnect a disconnected MCP server
- `ClaudeAgentSession.ToggleMcpServerAsync()` - enable/disable an MCP server
- `ClaudeAgentSession.SetMcpServersAsync()` - replace MCP server configuration, returns `McpSetServersResult`

#### MCP Enhancements
- `ToolAnnotations` record with `ReadOnlyHint`, `DestructiveHint`, `IdempotentHint`, `OpenWorldHint`
- `Annotations` property on `ToolDefinition`
- `annotations` parameter on `ToolHelpers.Tool()` overloads
- `annotations` parameter on `SdkMcpTool.Create()` overloads
- Annotation properties on `ClaudeToolAttribute` (`ReadOnlyHint`, `DestructiveHint`, `IdempotentHint`, `OpenWorldHint`)
- `McpServerStatusType.Disabled` enum value
- `Config`, `Scope`, `Tools` fields on `McpServerStatus`
- `Error`, `Config`, `Scope`, `Tools` fields on `McpServerStatusInfo`
- `McpSetServersResult` record with `Added`, `Removed`, `Errors` lists

#### Control Protocol
- `ControlSubtypeEnum.ReconnectMcpServer`
- `ControlSubtypeEnum.ToggleMcpServer`
- `ControlSubtypeEnum.SetMcpServers`
- `ReconnectMcpServerRequestBody`, `ToggleMcpServerRequestBody`, `SetMcpServersRequestBody` request records

#### Sandbox
- `NetworkSandboxSettings.AllowedDomains` (`IReadOnlyList<string>?`)
- `NetworkSandboxSettings.AllowManagedDomainsOnly` (`bool`)
- `SandboxSettings.Ripgrep` (`RipgrepConfig?`)
- `RipgrepConfig` record with `Command` and `Args`

### Changed

- `ClaudeAgentSession.RewindFilesAsync()` now returns `Task<RewindFilesResult>` instead of `Task`, providing structured rewind outcome with `CanRewind`, `Error`, `FilesChanged`, `Insertions`, `Deletions`

### Project Tooling

- Added `CLAUDE.md` with project conventions and upgrade methodology
- Added `/upgrade` slash command (`.claude/skills/upgrade/SKILL.md`) for monthly parity sync
