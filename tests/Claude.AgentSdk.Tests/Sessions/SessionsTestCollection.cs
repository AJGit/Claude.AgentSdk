namespace Claude.AgentSdk.Tests.Sessions;

/// <summary>
///     Groups all <c>Sessions</c> tests into a single, non-parallelized xUnit collection.
/// </summary>
/// <remarks>
///     These tests point <c>CLAUDE_CONFIG_DIR</c> at a per-test temp directory via
///     <see cref="ClaudeConfigDirFixture" />, which mutates a process-wide environment
///     variable. Running them in parallel (xUnit's default across test classes) would let
///     concurrent tests stomp on each other's config dir, so this collection disables
///     parallelization for every class tagged with <c>[Collection(Name)]</c>.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SessionsTestCollection
{
    /// <summary>The xUnit collection name shared by all Sessions test classes.</summary>
    public const string Name = "Sessions (sequential — mutates CLAUDE_CONFIG_DIR)";
}

/// <summary>
///     Creates a temporary <c>~/.claude/projects</c> directory tree and points
///     <c>CLAUDE_CONFIG_DIR</c> at it for the lifetime of a single test. Restores the previous
///     environment variable value on dispose.
/// </summary>
public sealed class ClaudeConfigDirFixture : IDisposable
{
    private readonly string? _previousConfigDir;

    public ClaudeConfigDirFixture()
    {
        ConfigDir = Path.Combine(Path.GetTempPath(), "claude-agentsdk-tests-" + Guid.NewGuid().ToString("N"));
        ProjectsDir = Path.Combine(ConfigDir, "projects");
        Directory.CreateDirectory(ProjectsDir);

        _previousConfigDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", ConfigDir);
    }

    /// <summary>The temporary <c>~/.claude</c>-equivalent directory.</summary>
    public string ConfigDir { get; }

    /// <summary>The temporary <c>~/.claude/projects</c>-equivalent directory.</summary>
    public string ProjectsDir { get; }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _previousConfigDir);
        try
        {
            Directory.Delete(ConfigDir, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
