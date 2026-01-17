using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Claude.AgentSdk.DamageControl.Security;

/// <summary>
///     Loads security configuration from YAML files.
/// </summary>
public static class SecurityConfigLoader
{
    private const string DefaultFileName = "patterns.yaml";

    /// <summary>
    ///     Loads security configuration from the specified path or searches common locations.
    /// </summary>
    /// <param name="configPath">Optional explicit path to the configuration file.</param>
    /// <returns>The loaded configuration, or an empty config if not found.</returns>
    public static SecurityConfig Load(string? configPath = null)
    {
        var path = ResolveConfigPath(configPath);

        if (path is null)
        {
            Console.WriteLine("[DamageControl] Warning: No patterns.yaml found, using empty configuration");
            return new SecurityConfig();
        }

        Console.WriteLine($"[DamageControl] Loading configuration from: {path}");

        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<SecurityConfig>(yaml);
            return config ?? new SecurityConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DamageControl] Error loading configuration: {ex.Message}");
            return new SecurityConfig();
        }
    }

    private static string? ResolveConfigPath(string? explicitPath)
    {
        // 1. Check explicit path
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        // 2. Check working directory
        var workingDirPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName);
        if (File.Exists(workingDirPath))
        {
            return workingDirPath;
        }

        // 3. Check executable directory
        var exeDir = AppContext.BaseDirectory;
        var exeDirPath = Path.Combine(exeDir, DefaultFileName);
        if (File.Exists(exeDirPath))
        {
            return exeDirPath;
        }

        // 4. Check parent directories (useful during development)
        var currentDir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 5; i++) // Check up to 5 levels up
        {
            var parentPath = Path.Combine(currentDir, DefaultFileName);
            if (File.Exists(parentPath))
            {
                return parentPath;
            }
            var parentDir = Directory.GetParent(currentDir);
            if (parentDir is null) break;
            currentDir = parentDir.FullName;
        }

        return null;
    }
}
