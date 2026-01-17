using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Tests.Types;

/// <summary>
///     Tests for the ModelIdentifier strongly-typed identifier.
/// </summary>
[UnitTest]
public class ModelIdentifierTests
{
    [Fact]
    public void Constructor_WithValidValue_SetsValue()
    {
        // Act
        var model = new ModelIdentifier("claude-sonnet-4-20250514");

        // Assert
        Assert.Equal("claude-sonnet-4-20250514", model.Value);
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ModelIdentifier(null!));
    }

    [Fact]
    public void Value_WhenDefault_ReturnsEmptyString()
    {
        // Arrange
        ModelIdentifier model = default;

        // Act & Assert
        Assert.Equal(string.Empty, model.Value);
    }

    [Fact]
    public void Sonnet_ReturnsCorrectValue()
    {
        Assert.Equal("sonnet", ModelIdentifier.Sonnet.Value);
    }

    [Fact]
    public void Opus_ReturnsCorrectValue()
    {
        Assert.Equal("opus", ModelIdentifier.Opus.Value);
    }

    [Fact]
    public void Haiku_ReturnsCorrectValue()
    {
        Assert.Equal("haiku", ModelIdentifier.Haiku.Value);
    }

    [Fact]
    public void ClaudeSonnet4_ReturnsCorrectValue()
    {
        Assert.Equal("claude-sonnet-4-20250514", ModelIdentifier.ClaudeSonnet4.Value);
    }

    [Fact]
    public void ClaudeOpus45_ReturnsCorrectValue()
    {
        Assert.Equal("claude-opus-4-5-20251101", ModelIdentifier.ClaudeOpus45.Value);
    }

    [Fact]
    public void ClaudeHaiku35_ReturnsCorrectValue()
    {
        Assert.Equal("claude-3-5-haiku-20241022", ModelIdentifier.ClaudeHaiku35.Value);
    }

    [Fact]
    public void ClaudeSonnet35V2_ReturnsCorrectValue()
    {
        Assert.Equal("claude-3-5-sonnet-20241022", ModelIdentifier.ClaudeSonnet35V2.Value);
    }

    [Fact]
    public void ClaudeSonnet35_ReturnsCorrectValue()
    {
        Assert.Equal("claude-3-5-sonnet-20240620", ModelIdentifier.ClaudeSonnet35.Value);
    }

    [Fact]
    public void ClaudeOpus3_ReturnsCorrectValue()
    {
        Assert.Equal("claude-3-opus-20240229", ModelIdentifier.ClaudeOpus3.Value);
    }

    [Fact]
    public void Custom_ReturnsNewModelIdentifier()
    {
        // Act
        var model = ModelIdentifier.Custom("my-custom-model");

        // Assert
        Assert.Equal("my-custom-model", model.Value);
    }

    [Fact]
    public void FromNullable_WithValue_ReturnsModelIdentifier()
    {
        // Act
        var model = ModelIdentifier.FromNullable("sonnet");

        // Assert
        Assert.NotNull(model);
        Assert.Equal("sonnet", model.Value.Value);
    }

    [Fact]
    public void FromNullable_WithNull_ReturnsNull()
    {
        // Act
        var model = ModelIdentifier.FromNullable(null);

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesModelIdentifier()
    {
        // Act
        ModelIdentifier model = "sonnet";

        // Assert
        Assert.Equal("sonnet", model.Value);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var model = ModelIdentifier.Sonnet;

        // Act
        string value = model;

        // Assert
        Assert.Equal("sonnet", value);
    }

    [Fact]
    public void ImplicitConversion_CanUseInOptions()
    {
        // Arrange & Act
        var options = new ClaudeAgentOptions
        {
            ModelId = ModelIdentifier.Sonnet
        };

        // Assert
        Assert.Equal("sonnet", options.ModelId?.Value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var model1 = new ModelIdentifier("sonnet");
        var model2 = new ModelIdentifier("sonnet");

        // Act & Assert
        Assert.True(model1.Equals(model2));
        Assert.True(model1 == model2);
        Assert.False(model1 != model2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var model1 = new ModelIdentifier("sonnet");
        var model2 = new ModelIdentifier("opus");

        // Act & Assert
        Assert.False(model1.Equals(model2));
        Assert.False(model1 == model2);
        Assert.True(model1 != model2);
    }

    [Fact]
    public void Equals_WithStaticProperty_ReturnsTrue()
    {
        // Arrange
        var model = new ModelIdentifier("sonnet");

        // Act & Assert
        Assert.True(model.Equals(ModelIdentifier.Sonnet));
        Assert.True(model == ModelIdentifier.Sonnet);
    }

    [Fact]
    public void Equals_WithObject_ReturnsCorrectResult()
    {
        // Arrange
        var model = new ModelIdentifier("sonnet");
        object other = new ModelIdentifier("sonnet");
        object different = new ModelIdentifier("opus");

        // Act & Assert
        Assert.True(model.Equals(other));
        Assert.False(model.Equals(different));
        Assert.True(model.Equals("sonnet")); // Implicitly converted to ModelIdentifier
        Assert.False(model.Equals((object?)null));
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var model1 = new ModelIdentifier("sonnet");
        var model2 = new ModelIdentifier("sonnet");

        // Act & Assert
        Assert.Equal(model1.GetHashCode(), model2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WhenDefault_ReturnsZero()
    {
        // Arrange
        ModelIdentifier model = default;

        // Act & Assert
        Assert.Equal(0, model.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var model = new ModelIdentifier("sonnet");

        // Act & Assert
        Assert.Equal("sonnet", model.ToString());
    }

    [Fact]
    public void ToString_WhenDefault_ReturnsEmptyString()
    {
        // Arrange
        ModelIdentifier model = default;

        // Act & Assert
        Assert.Equal(string.Empty, model.ToString());
    }
}
