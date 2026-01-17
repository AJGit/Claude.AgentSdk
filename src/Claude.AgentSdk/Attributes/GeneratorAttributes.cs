using System.Runtime.Serialization;

namespace Claude.AgentSdk.Attributes;

/// <summary>
///     Naming strategy for enum string serialization.
/// </summary>
public enum EnumNamingStrategy
{
    /// <summary>Use the exact enum member name.</summary>
    Exact,

    /// <summary>Convert to snake_case (e.g., StreamEvent → stream_event).</summary>
    SnakeCase,

    /// <summary>Convert to kebab-case (e.g., NeedsAuth → needs-auth).</summary>
    KebabCase,

    /// <summary>Convert to lowercase (e.g., User → user).</summary>
    LowerCase
}

/// <summary>
///     Marks an enum for compile-time string mapping generation via source generator.
///     When applied, the generator creates ToJsonString() extension and Parse/TryParse methods.
/// </summary>
/// <remarks>
///     Use <see cref="EnumMemberAttribute" /> on individual enum values to specify
///     custom string representations. Without it, the naming strategy is applied.
/// </remarks>
/// <example>
///     <code>
///     [GenerateEnumStrings]
///     public enum MessageType
///     {
///         [EnumMember(Value = "user")] User,
///         [EnumMember(Value = "assistant")] Assistant,
///         [EnumMember(Value = "stream_event")] StreamEvent
///     }
/// 
///     // Generated:
///     // MessageType.User.ToJsonString() → "user"
///     // EnumStringMappings.ParseMessageType("user") → MessageType.User
///     // EnumStringMappings.TryParseMessageType("user", out var result) → true
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class GenerateEnumStringsAttribute : Attribute
{
    /// <summary>
    ///     Default naming strategy for enum values without explicit [EnumMember] attributes.
    /// </summary>
    public EnumNamingStrategy DefaultNaming { get; init; } = EnumNamingStrategy.SnakeCase;
}

/// <summary>
///     Marks a class for compile-time tool registration via source generator.
///     When applied, the generator creates an extension method for registering
///     all [ClaudeTool] methods without reflection.
/// </summary>
/// <example>
///     <code>
///     [GenerateToolRegistration]
///     public class MyTools
///     {
///         [ClaudeTool("search", "Search for items")]
///         public string Search(string query, int limit = 10) => "results";
///     }
/// 
///     // Generated: MyToolsToolRegistrationExtensions.RegisterToolsCompiled(server, instance)
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateToolRegistrationAttribute : Attribute
{
}

/// <summary>
///     Marks a type for compile-time JSON schema generation via source generator.
///     When applied, the generator creates a static schema string and helper method.
/// </summary>
/// <example>
///     <code>
///     [GenerateSchema]
///     public record SearchInput(string Query, int Limit = 10);
/// 
///     // Generated: SearchInputSchemaExtensions.GetSchema()
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateSchemaAttribute : Attribute
{
}

/// <summary>
///     Marks a class or property as a Claude agent definition for compile-time validation and code generation.
/// </summary>
/// <remarks>
///     Can be applied to:
///     - Classes: Marks the class as an agent container
///     - Properties: Marks a string property as providing the agent's prompt (for declarative registration)
/// </remarks>
/// <example>
///     <code>
///     // Class-based usage:
///     [ClaudeAgent("MyAgent", Description = "An agent that does things")]
///     public class MyAgent { }
/// 
///     // Property-based usage (with GenerateAgentRegistration):
///     [GenerateAgentRegistration]
///     public class MyAgents
///     {
///         [ClaudeAgent("code-reviewer", Description = "Expert code reviewer")]
///         [AgentTools("Read", "Grep", "Glob")]
///         public static string CodeReviewerPrompt => "You are a code review specialist...";
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class ClaudeAgentAttribute : Attribute
{
    /// <summary>
    ///     Creates a new ClaudeAgent attribute.
    /// </summary>
    /// <param name="name">The unique name for the agent.</param>
    public ClaudeAgentAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    ///     The unique name for the agent.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Description of what the agent does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     The model to use for this agent (e.g., "claude-sonnet-4-20250514").
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
///     Adds metadata to tool parameters for enhanced schema generation and documentation.
/// </summary>
/// <example>
///     <code>
///     [ClaudeTool("search", "Search for items")]
///     public string Search(
///         [ToolParameter(Description = "The search query", Example = "cats")]
///         string query,
///         [ToolParameter(Description = "Max results", MinValue = 1, MaxValue = 100)]
///         int limit = 10) => "results";
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class ToolParameterAttribute : Attribute
{
    /// <summary>
    ///     Description of the parameter for the schema.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Example value for the parameter.
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    ///     Allowed values (for enum-like parameters).
    /// </summary>
    public string[]? AllowedValues { get; init; }

    /// <summary>
    ///     Minimum value for numeric parameters.
    /// </summary>
    public double? MinValue { get; init; }

    /// <summary>
    ///     Maximum value for numeric parameters.
    /// </summary>
    public double? MaxValue { get; init; }

    /// <summary>
    ///     Minimum length for string parameters.
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    ///     Maximum length for string parameters.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    ///     Regex pattern for string validation.
    /// </summary>
    public string? Pattern { get; init; }
}

/// <summary>
///     Marks a type hierarchy for compile-time Match pattern generation via source generator.
///     When applied, the generator creates functional Match&lt;T&gt; extension methods for exhaustive pattern matching.
/// </summary>
/// <remarks>
///     <para>
///         The generated Match methods provide a functional approach to handling discriminated unions,
///         similar to F# match expressions or Rust's match statements.
///     </para>
///     <para>
///         The type must use <see cref="JsonDerivedTypeAttribute" /> to define the derived types.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     [GenerateMatch]
///     [JsonDerivedType(typeof(UserMessage), "user")]
///     [JsonDerivedType(typeof(AssistantMessage), "assistant")]
///     public abstract record Message;
/// 
///     // Generated usage:
///     var result = message.Match(
///         user: u => u.Content,
///         assistant: a => a.Model,
///         defaultCase: () => "unknown"
///     );
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateMatchAttribute : Attribute
{
}

/// <summary>
///     Marks a class for compile-time hook registration generation via source generator.
///     When applied, the generator creates a GetHooksCompiled() extension method that returns
///     a dictionary of hooks keyed by <see cref="HookEvent" />.
/// </summary>
/// <example>
///     <code>
///     [GenerateHookRegistration]
///     public class MyHooks
///     {
///         [HookHandler(HookEvent.PreToolUse, Matcher = "Bash")]
///         public Task&lt;HookOutput&gt; ValidateBash(HookInput input, string? toolUseId,
///             HookContext ctx, CancellationToken ct) { ... }
///     }
/// 
///     // Generated usage:
///     var hooks = new MyHooks();
///     var options = new ClaudeAgentOptions { Hooks = hooks.GetHooksCompiled() };
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateHookRegistrationAttribute : Attribute
{
}

/// <summary>
///     Marks a method as a hook handler for a specific hook event.
///     The method must match the HookCallback delegate signature from Claude.AgentSdk.Protocol.
/// </summary>
/// <remarks>
///     <para>
///         Multiple [HookHandler] attributes can be applied to the same method to handle
///         different hook events or matchers.
///     </para>
///     <para>
///         Method signature must be:
///         <code>Task&lt;HookOutput&gt; MethodName(HookInput input, string? toolUseId, HookContext ctx, CancellationToken ct)</code>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class HookHandlerAttribute : Attribute
{
    /// <summary>
    ///     Creates a new HookHandler attribute for the specified hook event.
    /// </summary>
    /// <param name="hookEvent">The hook event this handler responds to.</param>
    public HookHandlerAttribute(HookEvent hookEvent)
    {
        HookEvent = hookEvent;
    }

    /// <summary>
    ///     The hook event this handler responds to.
    /// </summary>
    public HookEvent HookEvent { get; }

    /// <summary>
    ///     Pattern to match (e.g., tool name like "Bash" or "Write|Edit").
    ///     If null, the handler matches all events of the specified type.
    /// </summary>
    public string? Matcher { get; init; }

    /// <summary>
    ///     Timeout in seconds for this hook. If null, uses the default timeout.
    /// </summary>
    public double Timeout { get; init; }
}

/// <summary>
///     Marks a class for compile-time agent registration generation via source generator.
///     When applied, the generator creates a GetAgentsCompiled() extension method that returns
///     a dictionary of agent definitions.
/// </summary>
/// <example>
///     <code>
///     [GenerateAgentRegistration]
///     public class MyAgents
///     {
///         [ClaudeAgent("code-reviewer", Description = "Expert code reviewer")]
///         [AgentTools("Read", "Grep", "Glob")]
///         public static string CodeReviewerPrompt => "You are a code review specialist...";
///     }
/// 
///     // Generated usage:
///     var agents = new MyAgents().GetAgentsCompiled();
///     var options = new ClaudeAgentOptions { Agents = agents };
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateAgentRegistrationAttribute : Attribute
{
}

/// <summary>
///     Specifies the tools available to an agent.
///     Must be applied alongside <see cref="ClaudeAgentAttribute" />.
/// </summary>
/// <remarks>
///     Do NOT include "Task" in a subagent's tools - subagents cannot spawn their own subagents.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AgentToolsAttribute : Attribute
{
    /// <summary>
    ///     Creates a new AgentTools attribute.
    /// </summary>
    /// <param name="tools">The tool names available to this agent.</param>
    public AgentToolsAttribute(params string[] tools)
    {
        Tools = tools;
    }

    /// <summary>
    ///     The tool names available to this agent.
    /// </summary>
    public string[] Tools { get; }
}

/// <summary>
///     Hook event types supported by the SDK (duplicated from Protocol for attribute usage).
/// </summary>
public enum HookEvent
{
    /// <summary>Fires before a tool executes.</summary>
    PreToolUse,

    /// <summary>Fires after a tool executes successfully.</summary>
    PostToolUse,

    /// <summary>Fires when a tool execution fails.</summary>
    PostToolUseFailure,

    /// <summary>Fires when a user prompt is submitted.</summary>
    UserPromptSubmit,

    /// <summary>Fires when the agent execution stops.</summary>
    Stop,

    /// <summary>Fires when a subagent starts.</summary>
    SubagentStart,

    /// <summary>Fires when a subagent completes.</summary>
    SubagentStop,

    /// <summary>Fires before conversation compaction.</summary>
    PreCompact,

    /// <summary>Fires when a permission dialog would be displayed.</summary>
    PermissionRequest,

    /// <summary>Fires when a session starts.</summary>
    SessionStart,

    /// <summary>Fires when a session ends.</summary>
    SessionEnd,

    /// <summary>Fires for agent status notifications.</summary>
    Notification
}
