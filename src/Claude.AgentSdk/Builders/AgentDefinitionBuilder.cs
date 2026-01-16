using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Builders;

/// <summary>
///     Fluent builder for configuring <see cref="AgentDefinition"/> instances.
/// </summary>
/// <remarks>
///     <para>
///     This builder provides a more ergonomic way to configure subagent definitions
///     compared to using object initializers.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     var agent = new AgentDefinitionBuilder()
///         .WithDescription("Expert code reviewer")
///         .WithPrompt("You are a code review specialist...")
///         .WithTools(ToolName.Read, ToolName.Grep, ToolName.Glob)
///         .WithModel(ModelIdentifier.Haiku)
///         .Build();
///     </code>
///     </para>
/// </remarks>
public sealed class AgentDefinitionBuilder
{
    private string? _description;
    private string? _prompt;
    private readonly List<string> _tools = new();
    private string? _model;

    /// <summary>
    ///     Sets the description of when to use this agent.
    /// </summary>
    /// <param name="description">Natural language description for Claude to decide when to delegate.</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder WithDescription(string description)
    {
        _description = description ?? throw new ArgumentNullException(nameof(description));
        return this;
    }

    /// <summary>
    ///     Sets the system prompt for this agent.
    /// </summary>
    /// <param name="prompt">The agent's system prompt defining its role and behavior.</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder WithPrompt(string prompt)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        return this;
    }

    /// <summary>
    ///     Sets the allowed tools for this agent using string names.
    /// </summary>
    /// <param name="tools">The tool names to allow.</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder WithTools(params string[] tools)
    {
        _tools.Clear();
        _tools.AddRange(tools);
        return this;
    }

    /// <summary>
    ///     Sets the allowed tools for this agent using strongly-typed names.
    /// </summary>
    /// <param name="tools">The tool names to allow.</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder WithTools(params ToolName[] tools)
    {
        _tools.Clear();
        _tools.AddRange(tools.Select(t => t.Value));
        return this;
    }

    /// <summary>
    ///     Adds tools to the allowed tools list.
    /// </summary>
    /// <param name="tools">The tool names to add.</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder AddTools(params string[] tools)
    {
        _tools.AddRange(tools);
        return this;
    }

    /// <summary>
    ///     Adds tools to the allowed tools list using strongly-typed names.
    /// </summary>
    /// <param name="tools">The tool names to add.</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder AddTools(params ToolName[] tools)
    {
        _tools.AddRange(tools.Select(t => t.Value));
        return this;
    }

    /// <summary>
    ///     Sets the model to use for this agent.
    /// </summary>
    /// <param name="model">The model name (e.g., "sonnet", "opus", "haiku").</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder WithModel(string model)
    {
        _model = model;
        return this;
    }

    /// <summary>
    ///     Sets the model to use for this agent using a strongly-typed identifier.
    /// </summary>
    /// <param name="model">The model identifier.</param>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder WithModel(ModelIdentifier model)
    {
        _model = model.Value;
        return this;
    }

    /// <summary>
    ///     Configures this agent for read-only analysis tasks.
    /// </summary>
    /// <remarks>
    ///     Sets up a common tool configuration for code analysis:
    ///     Read, Grep, Glob for exploring the codebase without modifications.
    /// </remarks>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder AsReadOnlyAnalyzer()
    {
        return WithTools(ToolName.Read, ToolName.Grep, ToolName.Glob);
    }

    /// <summary>
    ///     Configures this agent for code editing tasks.
    /// </summary>
    /// <remarks>
    ///     Sets up tools for both reading and modifying code:
    ///     Read, Write, Edit, Grep, Glob.
    /// </remarks>
    /// <returns>This builder for chaining.</returns>
    public AgentDefinitionBuilder AsCodeEditor()
    {
        return WithTools(ToolName.Read, ToolName.Write, ToolName.Edit, ToolName.Grep, ToolName.Glob);
    }

    /// <summary>
    ///     Builds the <see cref="AgentDefinition"/> instance.
    /// </summary>
    /// <returns>The configured agent definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required properties are not set.</exception>
    public AgentDefinition Build()
    {
        if (string.IsNullOrEmpty(_description))
            throw new InvalidOperationException("Description is required. Call WithDescription() before Build().");

        if (string.IsNullOrEmpty(_prompt))
            throw new InvalidOperationException("Prompt is required. Call WithPrompt() before Build().");

        return new AgentDefinition
        {
            Description = _description,
            Prompt = _prompt,
            Tools = _tools.Count > 0 ? _tools : null,
            Model = _model
        };
    }
}
