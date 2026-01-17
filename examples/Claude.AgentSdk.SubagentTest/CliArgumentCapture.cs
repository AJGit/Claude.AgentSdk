/// <summary>
/// Utility to capture and display the exact CLI arguments that would be passed to Claude CLI.
/// This helps diagnose configuration issues by showing what the SDK actually sends.
/// </summary>

using System.Text.Json;

namespace Claude.AgentSdk.SubagentTest;

public static class CliArgumentCapture
{
    /// <summary>
    ///     Reconstructs the CLI arguments that would be built from the given options.
    ///     This mirrors the logic in SubprocessTransport.BuildArguments().
    /// </summary>
    public static List<string> BuildExpectedArguments(ClaudeAgentOptions options, string? prompt = null)
    {
        List<string> args =
        [
            "--output-format", "stream-json",
            "--verbose"
        ];

        // Add prompt if provided (one-shot mode)
        if (!string.IsNullOrEmpty(prompt))
        {
            args.Add("--print");
            args.Add(prompt);
        }
        else
        {
            // Bidirectional mode
            args.Add("--input-format");
            args.Add("stream-json");
        }

        // Model
        if (!string.IsNullOrEmpty(options.Model))
        {
            args.Add("--model");
            args.Add(options.Model);
        }

        // Tools (can be list or preset)
        switch (options.Tools)
        {
            case ToolsList toolsList:
                if (toolsList.Tools.Count == 0)
                {
                    args.Add("--tools");
                    args.Add("");
                }
                else
                {
                    args.Add("--tools");
                    args.Add(string.Join(",", toolsList.Tools));
                }

                break;

            case ToolsPreset toolsPreset:
                args.Add("--tools");
                args.Add(JsonSerializer.Serialize(new { type = "preset", preset = toolsPreset.Preset }));
                break;
        }

        if (options.AllowedTools.Count > 0)
        {
            args.Add("--allowedTools");
            args.Add(string.Join(",", options.AllowedTools));
        }

        if (options.DisallowedTools.Count > 0)
        {
            args.Add("--disallowedTools");
            args.Add(string.Join(",", options.DisallowedTools));
        }

        // System prompt
        switch (options.SystemPrompt)
        {
            case CustomSystemPrompt custom:
                args.Add("--system-prompt");
                args.Add(custom.Prompt);
                break;

            case PresetSystemPrompt preset:
                args.Add("--system-prompt");
                args.Add(preset.Preset);
                if (!string.IsNullOrEmpty(preset.Append))
                {
                    args.Add("--append-system-prompt");
                    args.Add(preset.Append);
                }

                break;
        }

        // Permission mode
        if (options.PermissionMode.HasValue)
        {
            args.Add("--permission-mode");
            args.Add(options.PermissionMode.Value switch
            {
                PermissionMode.Default => "default",
                PermissionMode.AcceptEdits => "acceptEdits",
                PermissionMode.Plan => "plan",
                PermissionMode.BypassPermissions => "bypassPermissions",
                _ => "default"
            });
        }

        // Max turns
        if (options.MaxTurns.HasValue)
        {
            args.Add("--max-turns");
            args.Add(options.MaxTurns.Value.ToString());
        }

        // Agents (subagent definitions)
        if (options.Agents is { Count: > 0 })
        {
            Dictionary<string, object> agentsConfig = new();

            foreach ((string name, AgentDefinition agent) in options.Agents)
            {
                Dictionary<string, object> agentConfig = new()
                {
                    ["description"] = agent.Description,
                    ["prompt"] = agent.Prompt
                };

                if (agent.Tools is { Count: > 0 })
                {
                    agentConfig["tools"] = agent.Tools;
                }

                if (!string.IsNullOrEmpty(agent.Model))
                {
                    agentConfig["model"] = agent.Model;
                }

                agentsConfig[name] = agentConfig;
            }

            string agentsJson =
                JsonSerializer.Serialize(agentsConfig, new JsonSerializerOptions { WriteIndented = false });
            args.Add("--agents");
            args.Add(agentsJson);
        }

        return args;
    }

    /// <summary>
    ///     Formats CLI arguments for display, with special handling for JSON payloads.
    /// </summary>
    public static void PrintCliArguments(List<string> args, Action<string> log)
    {
        log("╔═══════════════════════════════════════════════════════╗");
        log("║           RECONSTRUCTED CLI ARGUMENTS                 ║");
        log("╚═══════════════════════════════════════════════════════╝");
        log("");
        log("claude \\");

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string continuation = i < args.Count - 1 ? " \\" : "";

            // Check if this is a flag that takes a value
            if (arg.StartsWith("--") && i + 1 < args.Count)
            {
                string nextArg = args[i + 1];
                i++; // Skip the value in next iteration

                // Special handling for long values
                if (nextArg.Length > 80)
                {
                    if (arg == "--agents" || arg == "--system-prompt")
                    {
                        log($"  {arg} \\");

                        // For JSON, try to pretty print
                        if (nextArg.StartsWith("{"))
                        {
                            try
                            {
                                JsonElement parsed = JsonSerializer.Deserialize<JsonElement>(nextArg);
                                string pretty = JsonSerializer.Serialize(parsed,
                                    new JsonSerializerOptions { WriteIndented = true });
                                foreach (string line in pretty.Split('\n'))
                                {
                                    log($"    {line}");
                                }
                            }
                            catch
                            {
                                log($"    '{nextArg.Substring(0, Math.Min(100, nextArg.Length))}...'");
                            }
                        }
                        else
                        {
                            // Truncate long system prompts
                            log($"    '{nextArg.Substring(0, Math.Min(100, nextArg.Length))}...'");
                        }

                        log($"  {continuation}");
                    }
                    else
                    {
                        log($"  {arg} '{nextArg.Substring(0, Math.Min(60, nextArg.Length))}...'{continuation}");
                    }
                }
                else
                {
                    log($"  {arg} {nextArg}{continuation}");
                }
            }
            else
            {
                log($"  {arg}{continuation}");
            }
        }

        log("");
    }

    /// <summary>
    ///     Compares what C# SDK sends vs what Python SDK would send for the same logical config.
    /// </summary>
    public static void CompareWithPythonExpected(ClaudeAgentOptions options, Action<string> log)
    {
        log("");
        log("╔═══════════════════════════════════════════════════════╗");
        log("║        C# vs PYTHON SDK ARGUMENT COMPARISON           ║");
        log("╚═══════════════════════════════════════════════════════╝");
        log("");

        // C# uses Tools property -> --tools flag
        // Python uses tools property -> --tools flag (same)
        // Both use allowed_tools/AllowedTools -> --allowedTools flag

        log("C# SDK Configuration:");
        if (options.Tools is ToolsList toolsList)
        {
            log($"  options.Tools = new ToolsList([{string.Join(", ", toolsList.Tools.Select(t => $"\"{t}\""))}])");
            log($"  → CLI: --tools {string.Join(",", toolsList.Tools)}");
        }
        else
        {
            log("  options.Tools = null");
            log("  → CLI: (no --tools flag, uses defaults)");
        }

        if (options.AllowedTools.Count > 0)
        {
            log($"  options.AllowedTools = [{string.Join(", ", options.AllowedTools.Select(t => $"\"{t}\""))}]");
            log($"  → CLI: --allowedTools {string.Join(",", options.AllowedTools)}");
        }
        else
        {
            log("  options.AllowedTools = []");
            log("  → CLI: (no --allowedTools flag)");
        }

        log("");
        log("Equivalent Python SDK Configuration:");
        if (options.Tools is ToolsList pythonToolsList)
        {
            log("  options = ClaudeAgentOptions(");
            log($"      tools=[{string.Join(", ", pythonToolsList.Tools.Select(t => $"\"{t}\""))}],");
            log($"      allowed_tools=[{string.Join(", ", options.AllowedTools.Select(t => $"\"{t}\""))}],");
            log("      agents={...}");
            log("  )");
        }
        else
        {
            log("  options = ClaudeAgentOptions(");
            log("      # tools not set - uses defaults");
            log($"      allowed_tools=[{string.Join(", ", options.AllowedTools.Select(t => $"\"{t}\""))}],");
            log("      agents={...}");
            log("  )");
        }

        log("");
        log("⚠️  KEY INSIGHT:");
        log("   Python SDK docs show: allowed_tools=[\"Read\", \"Task\", ...]");
        log("   This maps to --allowedTools flag, NOT --tools flag.");
        log("   ");
        log("   The --tools flag sets the BASE tool set (replaces defaults).");
        log("   The --allowedTools flag ADDS to the current tool set.");
        log("");
    }
}
