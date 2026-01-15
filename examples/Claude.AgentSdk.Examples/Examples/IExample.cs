namespace Claude.AgentSdk.Examples.Examples;

/// <summary>
/// Interface for all SDK examples.
/// </summary>
public interface IExample
{
    /// <summary>
    /// Display name of the example.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Brief description of what this example demonstrates.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Run the example.
    /// </summary>
    Task RunAsync();
}
