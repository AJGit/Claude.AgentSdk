namespace Claude.AgentSdk.Tests;

/// <summary>
///     Test category trait names for filtering tests.
/// </summary>
/// <remarks>
///     <para>
///         Use these constants with xUnit's Trait attribute to categorize tests:
///     </para>
///     <code>
///     [Trait(TestCategories.Category, TestCategories.E2E)]
///     public class MyE2ETests { }
///     </code>
///     <para>
///         Run filtered tests from command line:
///     </para>
///     <code>
///     # Run only unit tests (exclude E2E)
///     dotnet test --filter "Category!=E2E"
/// 
///     # Run only E2E tests
///     dotnet test --filter "Category=E2E"
/// 
///     # Run only integration tests
///     dotnet test --filter "Category=Integration"
///     </code>
/// </remarks>
public static class TestCategories
{
    /// <summary>
    ///     The trait name for categorizing tests.
    /// </summary>
    public const string Category = "Category";

    /// <summary>
    ///     End-to-end tests that require the Claude CLI to be installed and configured.
    ///     These tests are slower and may incur API costs.
    /// </summary>
    public const string E2E = "E2E";

    /// <summary>
    ///     Integration tests that test multiple components together but don't require external dependencies.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    ///     Unit tests that test individual components in isolation.
    /// </summary>
    public const string Unit = "Unit";
}

/// <summary>
///     Marks a test class or method as an E2E test requiring the Claude CLI.
/// </summary>
/// <remarks>
///     E2E tests are excluded by default in CI pipelines. Run them explicitly with:
///     <code>dotnet test --filter "Category=E2E"</code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class E2ETestAttribute : Attribute, ITraitAttribute
{
    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits()
    {
        yield return new KeyValuePair<string, string>(TestCategories.Category, TestCategories.E2E);
    }
}

/// <summary>
///     Marks a test class or method as an integration test.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IntegrationTestAttribute : Attribute, ITraitAttribute
{
    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits()
    {
        yield return new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Integration);
    }
}

/// <summary>
///     Marks a test class or method as a unit test.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UnitTestAttribute : Attribute, ITraitAttribute
{
    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits()
    {
        yield return new KeyValuePair<string, string>(TestCategories.Category, TestCategories.Unit);
    }
}

/// <summary>
///     Interface for trait attributes used by xUnit.
/// </summary>
public interface ITraitAttribute
{
    /// <summary>
    ///     Gets the traits for this attribute.
    /// </summary>
    IEnumerable<KeyValuePair<string, string>> GetTraits();
}
