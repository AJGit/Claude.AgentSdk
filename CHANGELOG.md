# Changelog

All notable changes to the Claude.AgentSdk project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased] - February 2026 Parity Update

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
