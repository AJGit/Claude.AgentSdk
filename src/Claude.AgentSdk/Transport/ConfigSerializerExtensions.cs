namespace Claude.AgentSdk.Transport;

/// <summary>
///     Extension methods for serializing configuration types to CLI-compatible dictionaries.
/// </summary>
/// <remarks>
/// </remarks>
internal static class ConfigSerializerExtensions
{
    extension(SandboxSettings settings)
    {
        /// <summary>
        ///     Converts sandbox settings to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object> ToCliDictionary()
        {
            var config = new Dictionary<string, object>();

            if (settings.IsEnabled)
            {
                config["enabled"] = true;
            }

            if (settings.AutoAllowBashIfSandboxed)
            {
                config["autoAllowBashIfSandboxed"] = true;
            }

            if (settings.ExcludedCommands is { Count: > 0 })
            {
                config["excludedCommands"] = settings.ExcludedCommands;
            }

            if (settings.AllowUnsandboxedCommands)
            {
                config["allowUnsandboxedCommands"] = true;
            }

            if (settings.EnableWeakerNestedSandbox)
            {
                config["enableWeakerNestedSandbox"] = true;
            }

            if (settings.Network is { } network)
            {
                var networkConfig = network.ToCliDictionary();
                if (networkConfig.Count > 0)
                {
                    config["network"] = networkConfig;
                }
            }

            if (settings.IgnoreViolations is { } violations)
            {
                var violationsConfig = violations.ToCliDictionary();
                if (violationsConfig.Count > 0)
                {
                    config["ignoreViolations"] = violationsConfig;
                }
            }

            return config;
        }
    }

    extension(NetworkSandboxSettings network)
    {
        /// <summary>
        ///     Converts network sandbox settings to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object> ToCliDictionary()
        {
            var config = new Dictionary<string, object>();

            if (network.AllowLocalBinding)
            {
                config["allowLocalBinding"] = true;
            }

            if (network.AllowUnixSockets is { Count: > 0 })
            {
                config["allowUnixSockets"] = network.AllowUnixSockets;
            }

            if (network.AllowAllUnixSockets)
            {
                config["allowAllUnixSockets"] = true;
            }

            if (network.HttpProxyPort.HasValue)
            {
                config["httpProxyPort"] = network.HttpProxyPort.Value;
            }

            if (network.SocksProxyPort.HasValue)
            {
                config["socksProxyPort"] = network.SocksProxyPort.Value;
            }

            return config;
        }
    }

    extension(SandboxIgnoreViolations violations)
    {
        /// <summary>
        ///     Converts sandbox ignore violations to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object> ToCliDictionary()
        {
            var config = new Dictionary<string, object>();

            if (violations.File is { Count: > 0 })
            {
                config["file"] = violations.File;
            }

            if (violations.Network is { Count: > 0 })
            {
                config["network"] = violations.Network;
            }

            return config;
        }
    }

    extension(McpServerConfig config)
    {
        /// <summary>
        ///     Converts MCP server config to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object>? ToCliDictionary()
        {
            return config switch
            {
                McpStdioServerConfig stdio => stdio.ToCliDictionary(),
                McpSseServerConfig sse => sse.ToCliDictionary(),
                McpHttpServerConfig http => http.ToCliDictionary(),
                McpSdkServerConfig sdk => sdk.ToCliDictionary(),
                _ => null
            };
        }
    }

    extension(McpStdioServerConfig stdio)
    {
        /// <summary>
        ///     Converts MCP stdio server config to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object> ToCliDictionary()
        {
            var config = new Dictionary<string, object> { ["command"] = stdio.Command };

            if (stdio.Args is { Count: > 0 })
            {
                config["args"] = stdio.Args;
            }

            if (stdio.Env is { Count: > 0 })
            {
                config["env"] = stdio.Env;
            }

            return config;
        }
    }

    extension(McpSseServerConfig sse)
    {
        /// <summary>
        ///     Converts MCP SSE server config to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object> ToCliDictionary()
        {
            var config = new Dictionary<string, object>
            {
                ["type"] = "sse",
                ["url"] = sse.Url
            };

            if (sse.Headers is { Count: > 0 })
            {
                config["headers"] = sse.Headers;
            }

            return config;
        }
    }

    extension(McpHttpServerConfig http)
    {
        /// <summary>
        ///     Converts MCP HTTP server config to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object> ToCliDictionary()
        {
            var config = new Dictionary<string, object>
            {
                ["type"] = "http",
                ["url"] = http.Url
            };

            if (http.Headers is { Count: > 0 })
            {
                config["headers"] = http.Headers;
            }

            return config;
        }
    }

    extension(McpSdkServerConfig sdk)
    {
        /// <summary>
        ///     Converts MCP SDK server config to a CLI-compatible dictionary.
        /// </summary>
        public Dictionary<string, object> ToCliDictionary()
        {
            return new Dictionary<string, object>
            {
                ["type"] = "sdk",
                ["name"] = sdk.Name
            };
        }
    }
}
