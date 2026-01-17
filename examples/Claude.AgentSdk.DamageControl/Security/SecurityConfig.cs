using YamlDotNet.Serialization;

namespace Claude.AgentSdk.DamageControl.Security;

/// <summary>
///     Represents a bash command pattern with optional ask confirmation.
/// </summary>
public sealed record BashPattern
{
    /// <summary>
    ///     The regex pattern to match against bash commands.
    /// </summary>
    [YamlMember(Alias = "pattern")]
    public string Pattern { get; init; } = "";

    /// <summary>
    ///     The reason why this pattern is blocked or flagged.
    /// </summary>
    [YamlMember(Alias = "reason")]
    public string Reason { get; init; } = "";

    /// <summary>
    ///     If true, trigger a confirmation dialog instead of blocking.
    /// </summary>
    [YamlMember(Alias = "ask")]
    public bool Ask { get; init; } = false;
}

/// <summary>
///     Security configuration loaded from patterns.yaml.
/// </summary>
public sealed record SecurityConfig
{
    /// <summary>
    ///     Patterns to match against bash commands.
    /// </summary>
    [YamlMember(Alias = "bashToolPatterns")]
    public List<BashPattern> BashToolPatterns { get; init; } = [];

    /// <summary>
    ///     Paths that cannot be accessed at all (no read, write, or any operation).
    ///     Typically contains secrets and credentials.
    /// </summary>
    [YamlMember(Alias = "zeroAccessPaths")]
    public List<string> ZeroAccessPaths { get; init; } = [];

    /// <summary>
    ///     Paths that can be read but not written or edited.
    ///     Includes lock files, system directories, and build artifacts.
    /// </summary>
    [YamlMember(Alias = "readOnlyPaths")]
    public List<string> ReadOnlyPaths { get; init; } = [];

    /// <summary>
    ///     Paths that can be read and written but not deleted.
    ///     Protects important files like LICENSE, README, and .git directory.
    /// </summary>
    [YamlMember(Alias = "noDeletePaths")]
    public List<string> NoDeletePaths { get; init; } = [];
}
