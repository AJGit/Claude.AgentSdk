using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Tests.Builders;

/// <summary>
///     Tests for the AgentDefinitionBuilder fluent builder.
/// </summary>
[UnitTest]
public class AgentDefinitionBuilderTests
{
    #region Basic Building Tests

    [Fact]
    public void Build_WithRequiredProperties_CreatesAgentDefinition()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Code review specialist")
            .WithPrompt("You are an expert code reviewer.")
            .Build();

        // Assert
        Assert.Equal("Code review specialist", agent.Description);
        Assert.Equal("You are an expert code reviewer.", agent.Prompt);
    }

    [Fact]
    public void Build_WithoutDescription_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new AgentDefinitionBuilder()
            .WithPrompt("Some prompt");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Description is required", ex.Message);
    }

    [Fact]
    public void Build_WithoutPrompt_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new AgentDefinitionBuilder()
            .WithDescription("Some description");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Prompt is required", ex.Message);
    }

    [Fact]
    public void WithDescription_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentDefinitionBuilder().WithDescription(null!));
    }

    [Fact]
    public void WithPrompt_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentDefinitionBuilder().WithPrompt(null!));
    }

    #endregion

    #region Tools Configuration Tests

    [Fact]
    public void WithTools_StringArray_SetsTools()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .WithTools("Read", "Grep", "Glob")
            .Build();

        // Assert
        Assert.Equal(["Read", "Grep", "Glob"], agent.Tools);
    }

    [Fact]
    public void WithTools_ToolNameArray_SetsTools()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .WithTools(ToolName.Read, ToolName.Grep, ToolName.Glob)
            .Build();

        // Assert
        Assert.Equal(["Read", "Grep", "Glob"], agent.Tools);
    }

    [Fact]
    public void WithTools_Overwrites_PreviousTools()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .WithTools("Read", "Write")
            .WithTools("Bash")
            .Build();

        // Assert
        Assert.Equal(["Bash"], agent.Tools);
    }

    [Fact]
    public void AddTools_StringArray_AppendsTools()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .WithTools("Read")
            .AddTools("Grep", "Glob")
            .Build();

        // Assert
        Assert.Equal(["Read", "Grep", "Glob"], agent.Tools);
    }

    [Fact]
    public void AddTools_ToolNameArray_AppendsTools()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .WithTools(ToolName.Read)
            .AddTools(ToolName.Grep, ToolName.Glob)
            .Build();

        // Assert
        Assert.Equal(["Read", "Grep", "Glob"], agent.Tools);
    }

    [Fact]
    public void Build_WithNoTools_SetsToolsToNull()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .Build();

        // Assert
        Assert.Null(agent.Tools);
    }

    #endregion

    #region Model Configuration Tests

    [Fact]
    public void WithModel_String_SetsModel()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .WithModel("haiku")
            .Build();

        // Assert
        Assert.Equal("haiku", agent.Model);
    }

    [Fact]
    public void WithModel_ModelIdentifier_SetsModel()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .WithModel(ModelIdentifier.Haiku)
            .Build();

        // Assert
        Assert.Equal("haiku", agent.Model);
    }

    [Fact]
    public void Build_WithNoModel_SetsModelToNull()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Test agent")
            .WithPrompt("Test prompt")
            .Build();

        // Assert
        Assert.Null(agent.Model);
    }

    #endregion

    #region Preset Configuration Tests

    [Fact]
    public void AsReadOnlyAnalyzer_SetsReadOnlyTools()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Code analyzer")
            .WithPrompt("You analyze code.")
            .AsReadOnlyAnalyzer()
            .Build();

        // Assert
        Assert.Equal(["Read", "Grep", "Glob"], agent.Tools);
    }

    [Fact]
    public void AsCodeEditor_SetsEditorTools()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Code editor")
            .WithPrompt("You edit code.")
            .AsCodeEditor()
            .Build();

        // Assert
        Assert.Equal(["Read", "Write", "Edit", "Grep", "Glob"], agent.Tools);
    }

    #endregion

    #region Chaining Tests

    [Fact]
    public void MethodChaining_ReturnsBuilder()
    {
        // Act
        var builder = new AgentDefinitionBuilder()
            .WithDescription("Test")
            .WithPrompt("Prompt")
            .WithTools(ToolName.Read)
            .AddTools(ToolName.Grep)
            .WithModel(ModelIdentifier.Haiku);

        // Assert
        Assert.IsType<AgentDefinitionBuilder>(builder);
    }

    [Fact]
    public void Build_WithAllOptions_CreatesCompleteAgent()
    {
        // Act
        var agent = new AgentDefinitionBuilder()
            .WithDescription("Expert code reviewer for C# projects")
            .WithPrompt("You are an expert C# code reviewer. Focus on best practices and performance.")
            .WithTools(ToolName.Read, ToolName.Grep, ToolName.Glob)
            .WithModel(ModelIdentifier.Sonnet)
            .Build();

        // Assert
        Assert.Equal("Expert code reviewer for C# projects", agent.Description);
        Assert.Equal("You are an expert C# code reviewer. Focus on best practices and performance.", agent.Prompt);
        Assert.Equal(["Read", "Grep", "Glob"], agent.Tools);
        Assert.Equal("sonnet", agent.Model);
    }

    #endregion
}
