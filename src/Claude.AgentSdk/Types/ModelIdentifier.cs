namespace Claude.AgentSdk.Types;

/// <summary>
///     Strongly-typed model identifier that replaces magic string model names.
///     Provides type safety while maintaining backward compatibility via implicit conversions.
/// </summary>
/// <remarks>
///     <para>
///     Common aliases like "sonnet", "opus", "haiku" are supported alongside
///     specific version identifiers like "claude-sonnet-4-20250514".
///     </para>
///     <para>
///     Implicit conversions allow seamless migration from string-based model names:
///     <code>
///     // Old way (still works)
///     options.Model = "sonnet";
///
///     // New strongly-typed way
///     options.ModelId = ModelIdentifier.Sonnet;
///     </code>
///     </para>
/// </remarks>
public readonly struct ModelIdentifier : IEquatable<ModelIdentifier>
{
    private readonly string _value;

    /// <summary>
    ///     Creates a model identifier from a string value.
    /// </summary>
    /// <param name="value">The model identifier string.</param>
    public ModelIdentifier(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    ///     Gets the underlying string value of the model identifier.
    /// </summary>
    public string Value => _value ?? string.Empty;

    #region Common Aliases

    /// <summary>
    ///     Claude Sonnet model (latest version).
    /// </summary>
    public static ModelIdentifier Sonnet => new("sonnet");

    /// <summary>
    ///     Claude Opus model (latest version).
    /// </summary>
    public static ModelIdentifier Opus => new("opus");

    /// <summary>
    ///     Claude Haiku model (latest version).
    /// </summary>
    public static ModelIdentifier Haiku => new("haiku");

    #endregion

    #region Specific Versions

    /// <summary>
    ///     Claude Sonnet 4 (May 2025).
    /// </summary>
    public static ModelIdentifier ClaudeSonnet4 => new("claude-sonnet-4-20250514");

    /// <summary>
    ///     Claude Opus 4.5 (November 2025).
    /// </summary>
    public static ModelIdentifier ClaudeOpus45 => new("claude-opus-4-5-20251101");

    /// <summary>
    ///     Claude Haiku 3.5 (October 2024).
    /// </summary>
    public static ModelIdentifier ClaudeHaiku35 => new("claude-3-5-haiku-20241022");

    /// <summary>
    ///     Claude Sonnet 3.5 v2 (October 2024).
    /// </summary>
    public static ModelIdentifier ClaudeSonnet35V2 => new("claude-3-5-sonnet-20241022");

    /// <summary>
    ///     Claude Sonnet 3.5 (June 2024).
    /// </summary>
    public static ModelIdentifier ClaudeSonnet35 => new("claude-3-5-sonnet-20240620");

    /// <summary>
    ///     Claude Opus 3 (February 2024).
    /// </summary>
    public static ModelIdentifier ClaudeOpus3 => new("claude-3-opus-20240229");

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates a model identifier for a custom model ID.
    /// </summary>
    /// <param name="modelId">The custom model identifier string.</param>
    /// <returns>A new ModelIdentifier instance.</returns>
    public static ModelIdentifier Custom(string modelId) => new(modelId);

    /// <summary>
    ///     Creates a model identifier from a nullable string.
    /// </summary>
    /// <param name="modelId">The model identifier string, or null.</param>
    /// <returns>A ModelIdentifier if the string is not null, otherwise null.</returns>
    public static ModelIdentifier? FromNullable(string? modelId) =>
        modelId is null ? (ModelIdentifier?)null : new ModelIdentifier(modelId);

    #endregion

    #region Implicit Conversions

    /// <summary>
    ///     Implicitly converts a string to a ModelIdentifier for backward compatibility.
    /// </summary>
    public static implicit operator ModelIdentifier(string value) => new(value);

    /// <summary>
    ///     Implicitly converts a ModelIdentifier to a string.
    /// </summary>
    public static implicit operator string(ModelIdentifier model) => model.Value;

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(ModelIdentifier other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ModelIdentifier other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value?.GetHashCode() ?? 0;

    /// <summary>
    ///     Compares two model identifiers for equality.
    /// </summary>
    public static bool operator ==(ModelIdentifier left, ModelIdentifier right) =>
        left.Equals(right);

    /// <summary>
    ///     Compares two model identifiers for inequality.
    /// </summary>
    public static bool operator !=(ModelIdentifier left, ModelIdentifier right) =>
        !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;
}
