using Claude.AgentSdk.Functional;

namespace Claude.AgentSdk.Tests.Functional;

/// <summary>
///     Tests for the Validation applicative functor.
/// </summary>
[UnitTest]
public class ValidationTests
{
    private sealed record CustomError(string Code, string Message);

    [Fact]
    public void Valid_CreatesValidValidation()
    {
        // Act
        var validation = Validation.Valid(42);

        // Assert
        Assert.True(validation.IsValid);
        Assert.False(validation.IsInvalid);
        Assert.Equal(42, validation.Value);
    }

    [Fact]
    public void Invalid_CreatesInvalidValidation()
    {
        // Act
        var validation = Validation.Invalid<int>("error");

        // Assert
        Assert.True(validation.IsInvalid);
        Assert.False(validation.IsValid);
        Assert.Single(validation.Errors);
        Assert.Equal("error", validation.Errors[0]);
    }

    [Fact]
    public void Invalid_WithMultipleErrors_CreatesInvalidValidation()
    {
        // Act
        var validation = Validation<int>.Invalid(new[] { "error1", "error2" });

        // Assert
        Assert.True(validation.IsInvalid);
        Assert.Equal(2, validation.Errors.Count);
        Assert.Equal("error1", validation.Errors[0]);
        Assert.Equal("error2", validation.Errors[1]);
    }

    [Fact]
    public void Valid_WithTypedError_CreatesValidValidation()
    {
        // Act
        var validation = Validation.Valid<int, CustomError>(42);

        // Assert
        Assert.True(validation.IsValid);
        Assert.Equal(42, validation.Value);
    }

    [Fact]
    public void Invalid_WithTypedError_CreatesInvalidValidation()
    {
        // Act
        var error = new CustomError("E001", "Test error");
        var validation = Validation.Invalid<int, CustomError>(error);

        // Assert
        Assert.True(validation.IsInvalid);
        Assert.Single(validation.Errors);
        Assert.Equal("E001", validation.Errors[0].Code);
    }

    [Fact]
    public void Value_WhenValid_ReturnsValue()
    {
        // Arrange
        var validation = Validation.Valid(42);

        // Act & Assert
        Assert.Equal(42, validation.Value);
    }

    [Fact]
    public void Value_WhenInvalid_ThrowsInvalidOperationException()
    {
        // Arrange
        var validation = Validation.Invalid<int>("error");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => validation.Value);
    }

    [Fact]
    public void Errors_WhenInvalid_ReturnsErrors()
    {
        // Arrange
        var validation = Validation<int>.Invalid(new[] { "error1", "error2" });

        // Act & Assert
        Assert.Equal(["error1", "error2"], validation.Errors);
    }

    [Fact]
    public void Errors_WhenValid_ThrowsInvalidOperationException()
    {
        // Arrange
        var validation = Validation.Valid(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => validation.Errors);
    }

    [Fact]
    public void TryGetValue_WhenValid_ReturnsTrueAndValue()
    {
        // Arrange
        var validation = Validation.Valid(42);

        // Act
        var result = validation.TryGetValue(out var value);

        // Assert
        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_WhenInvalid_ReturnsFalse()
    {
        // Arrange
        var validation = Validation.Invalid<int>("error");

        // Act
        var result = validation.TryGetValue(out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetValueOrDefault_WhenValid_ReturnsValue()
    {
        // Arrange
        var validation = Validation.Valid(42);

        // Act
        var value = validation.GetValueOrDefault();

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void GetValueOrDefault_WhenInvalid_ReturnsDefault()
    {
        // Arrange
        var validation = Validation.Invalid<int>("error");

        // Act
        var value = validation.GetValueOrDefault(99);

        // Assert
        Assert.Equal(99, value);
    }

    [Fact]
    public void Match_WhenValid_CallsValidFunc()
    {
        // Arrange
        var validation = Validation.Valid(5);

        // Act
        var result = validation.Match(
            x => x * 2,
            _ => -1);

        // Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public void Match_WhenInvalid_CallsInvalidFunc()
    {
        // Arrange
        var validation = Validation<int>.Invalid(new[] { "e1", "e2" });

        // Act
        var result = validation.Match(
            _ => -1,
            errors => errors.Count);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void Match_WithActions_WhenValid_CallsValidAction()
    {
        // Arrange
        var validation = Validation.Valid(42);
        var validCalled = false;
        var invalidCalled = false;

        // Act
        validation.Match(
            _ => validCalled = true,
            _ => invalidCalled = true);

        // Assert
        Assert.True(validCalled);
        Assert.False(invalidCalled);
    }

    [Fact]
    public void Map_WhenValid_TransformsValue()
    {
        // Arrange
        var validation = Validation.Valid(5);

        // Act
        var result = validation.Map(x => x.ToString());

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("5", result.Value);
    }

    [Fact]
    public void Map_WhenInvalid_PreservesErrors()
    {
        // Arrange
        var validation = Validation.Invalid<int>("error");

        // Act
        var result = validation.Map(x => x.ToString());

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal("error", result.Errors[0]);
    }

    [Fact]
    public void MapErrors_WhenInvalid_TransformsErrors()
    {
        // Arrange
        var validation = Validation<int, string>.Invalid(new[] { "e1", "e2" });

        // Act
        var result = validation.MapErrors(e => e.ToUpper());

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal(["E1", "E2"], result.Errors);
    }

    [Fact]
    public void MapErrors_WhenValid_PreservesValue()
    {
        // Arrange
        var validation = Validation<int, string>.Valid(42);

        // Act
        var result = validation.MapErrors(e => e.ToUpper());

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Bind_WhenValid_ChainsOperation()
    {
        // Arrange
        var validation = Validation.Valid(10);

        // Act
        var result = validation.Bind(x =>
            x > 5 ? Validation.Valid(x * 2) : Validation.Invalid<int>("Too small"));

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Bind_WhenValidButBinderFails_ReturnsInvalid()
    {
        // Arrange
        var validation = Validation.Valid(3);

        // Act
        var result = validation.Bind(x =>
            x > 5 ? Validation.Valid(x * 2) : Validation.Invalid<int>("Too small"));

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal("Too small", result.Errors[0]);
    }

    [Fact]
    public void Bind_WhenInvalid_ShortCircuits()
    {
        // Arrange
        var validation = Validation.Invalid<int>("initial error");
        var binderCalled = false;

        // Act
        var result = validation.Bind(x =>
        {
            binderCalled = true;
            return Validation.Valid(x * 2);
        });

        // Assert
        Assert.True(result.IsInvalid);
        Assert.False(binderCalled);
    }

    [Fact]
    public void Ensure_WhenValidAndPredicatePasses_ReturnsValid()
    {
        // Arrange
        var validation = Validation.Valid(10);

        // Act
        var result = validation.Ensure(x => x > 5, "Must be greater than 5");

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Ensure_WhenValidAndPredicateFails_ReturnsInvalid()
    {
        // Arrange
        var validation = Validation.Valid(3);

        // Act
        var result = validation.Ensure(x => x > 5, "Must be greater than 5");

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal("Must be greater than 5", result.Errors[0]);
    }

    [Fact]
    public void Ensure_WhenInvalid_PreservesErrors()
    {
        // Arrange
        var validation = Validation.Invalid<int>("initial error");

        // Act
        var result = validation.Ensure(x => x > 5, "Must be greater than 5");

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal("initial error", result.Errors[0]);
    }

    [Fact]
    public void Combine_BothValid_ReturnsCombinedValid()
    {
        // Arrange
        var v1 = Validation<int, string>.Valid(1);
        var v2 = Validation<string, string>.Valid("two");

        // Act
        var result = Validation.Combine(v1, v2);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal((1, "two"), result.Value);
    }

    [Fact]
    public void Combine_FirstInvalid_ReturnsInvalidWithFirstErrors()
    {
        // Arrange
        var v1 = Validation<int, string>.Invalid("error1");
        var v2 = Validation<string, string>.Valid("two");

        // Act
        var result = Validation.Combine(v1, v2);

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Single(result.Errors);
        Assert.Equal("error1", result.Errors[0]);
    }

    [Fact]
    public void Combine_BothInvalid_ReturnsInvalidWithAllErrors()
    {
        // Arrange
        var v1 = Validation<int, string>.Invalid("error1");
        var v2 = Validation<string, string>.Invalid("error2");

        // Act
        var result = Validation.Combine(v1, v2);

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("error1", result.Errors[0]);
        Assert.Equal("error2", result.Errors[1]);
    }

    [Fact]
    public void Combine_Three_AllValid_ReturnsCombinedValid()
    {
        // Arrange
        var v1 = Validation<int, string>.Valid(1);
        var v2 = Validation<string, string>.Valid("two");
        var v3 = Validation<double, string>.Valid(3.0);

        // Act
        var result = Validation.Combine(v1, v2, v3);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal((1, "two", 3.0), result.Value);
    }

    [Fact]
    public void Combine_Three_AllInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var v1 = Validation<int, string>.Invalid("error1");
        var v2 = Validation<string, string>.Invalid("error2");
        var v3 = Validation<double, string>.Invalid("error3");

        // Act
        var result = Validation.Combine(v1, v2, v3);

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal(3, result.Errors.Count);
        Assert.Equal(["error1", "error2", "error3"], result.Errors);
    }

    [Fact]
    public void Map2_BothValid_AppliesFunction()
    {
        // Arrange
        var v1 = Validation<string, string>.Valid("Hello");
        var v2 = Validation<string, string>.Valid("World");

        // Act
        var result = Validation.Map2(v1, v2, (a, b) => $"{a} {b}");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Hello World", result.Value);
    }

    [Fact]
    public void Map2_BothInvalid_AccumulatesErrors()
    {
        // Arrange
        var v1 = Validation<string, string>.Invalid("error1");
        var v2 = Validation<string, string>.Invalid("error2");

        // Act
        var result = Validation.Map2(v1, v2, (a, b) => $"{a} {b}");

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Map3_AllValid_AppliesFunction()
    {
        // Arrange
        var v1 = Validation<string, string>.Valid("John");
        var v2 = Validation<string, string>.Valid("john@example.com");
        var v3 = Validation<int, string>.Valid(30);

        // Act
        var result = Validation.Map3(v1, v2, v3, (name, email, age) => new { name, email, age });

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("John", result.Value.name);
        Assert.Equal("john@example.com", result.Value.email);
        Assert.Equal(30, result.Value.age);
    }

    [Fact]
    public void Map3_MultipleInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var v1 = Validation<string, string>.Invalid("Name required");
        var v2 = Validation<string, string>.Invalid("Invalid email");
        var v3 = Validation<int, string>.Invalid("Age must be positive");

        // Act
        var result = Validation.Map3(v1, v2, v3, (name, email, age) => new { name, email, age });

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains("Name required", result.Errors);
        Assert.Contains("Invalid email", result.Errors);
        Assert.Contains("Age must be positive", result.Errors);
    }

    [Fact]
    public void Sequence_AllValid_ReturnsValidWithList()
    {
        // Arrange
        var validations = new[]
        {
            Validation<int, string>.Valid(1),
            Validation<int, string>.Valid(2),
            Validation<int, string>.Valid(3)
        };

        // Act
        var result = validations.Sequence();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal([1, 2, 3], result.Value);
    }

    [Fact]
    public void Sequence_AnyInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var validations = new[]
        {
            Validation<int, string>.Valid(1),
            Validation<int, string>.Invalid("error1"),
            Validation<int, string>.Valid(3),
            Validation<int, string>.Invalid("error2")
        };

        // Act
        var result = validations.Sequence();

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("error1", result.Errors);
        Assert.Contains("error2", result.Errors);
    }

    [Fact]
    public void Traverse_AllSucceed_ReturnsValid()
    {
        // Arrange
        var numbers = new[] { "1", "2", "3" };

        // Act
        var result = numbers.Traverse(s =>
            int.TryParse(s, out var n)
                ? Validation<int, string>.Valid(n)
                : Validation<int, string>.Invalid($"'{s}' is not a number"));

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal([1, 2, 3], result.Value);
    }

    [Fact]
    public void Traverse_MultipleFail_AccumulatesErrors()
    {
        // Arrange
        var numbers = new[] { "1", "invalid", "3", "bad" };

        // Act
        var result = numbers.Traverse(s =>
            int.TryParse(s, out var n)
                ? Validation<int, string>.Valid(n)
                : Validation<int, string>.Invalid($"'{s}' is not a number"));

        // Assert
        Assert.True(result.IsInvalid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("'invalid' is not a number", result.Errors);
        Assert.Contains("'bad' is not a number", result.Errors);
    }

    [Fact]
    public void ToResult_WhenValid_ReturnsSuccess()
    {
        // Arrange
        var validation = Validation<int, string>.Valid(42);

        // Act
        var result = validation.ToResult();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToResult_WhenInvalid_ReturnsFailureWithFirstError()
    {
        // Arrange
        var validation = Validation<int, string>.Invalid(new[] { "error1", "error2" });

        // Act
        var result = validation.ToResult();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("error1", result.Error);
    }

    [Fact]
    public void ToOption_WhenValid_ReturnsSome()
    {
        // Arrange
        var validation = Validation<int, string>.Valid(42);

        // Act
        var option = validation.ToOption();

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void ToOption_WhenInvalid_ReturnsNone()
    {
        // Arrange
        var validation = Validation<int, string>.Invalid("error");

        // Act
        var option = validation.ToOption();

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void ToGeneric_ConvertsSimpleToGeneric()
    {
        // Arrange
        var validation = Validation.Valid(42);

        // Act
        Validation<int, string> generic = validation.ToGeneric();

        // Assert
        Assert.True(generic.IsValid);
        Assert.Equal(42, generic.Value);
    }

    [Fact]
    public void From_WhenPredicatePasses_ReturnsValid()
    {
        // Act
        var validation = Validation.From(10, x => x > 5, "Must be greater than 5");

        // Assert
        Assert.True(validation.IsValid);
        Assert.Equal(10, validation.Value);
    }

    [Fact]
    public void From_WhenPredicateFails_ReturnsInvalid()
    {
        // Act
        var validation = Validation.From(3, x => x > 5, "Must be greater than 5");

        // Assert
        Assert.True(validation.IsInvalid);
        Assert.Equal("Must be greater than 5", validation.Errors[0]);
    }

    [Fact]
    public void FromResult_WhenSuccess_ReturnsValid()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var validation = Validation.FromResult(result);

        // Assert
        Assert.True(validation.IsValid);
        Assert.Equal(42, validation.Value);
    }

    [Fact]
    public void FromResult_WhenFailure_ReturnsInvalid()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var validation = Validation.FromResult(result);

        // Assert
        Assert.True(validation.IsInvalid);
        Assert.Equal("error", validation.Errors[0]);
    }

    [Fact]
    public void Equals_SameValidValue_ReturnsTrue()
    {
        // Arrange
        var v1 = Validation.Valid(42);
        var v2 = Validation.Valid(42);

        // Assert
        Assert.True(v1.Equals(v2));
        Assert.True(v1 == v2);
    }

    [Fact]
    public void Equals_DifferentValidValue_ReturnsFalse()
    {
        // Arrange
        var v1 = Validation.Valid(42);
        var v2 = Validation.Valid(43);

        // Assert
        Assert.False(v1.Equals(v2));
        Assert.True(v1 != v2);
    }

    [Fact]
    public void Equals_SameInvalidErrors_ReturnsTrue()
    {
        // Arrange
        var v1 = Validation<int>.Invalid(new[] { "e1", "e2" });
        var v2 = Validation<int>.Invalid(new[] { "e1", "e2" });

        // Assert
        Assert.True(v1.Equals(v2));
    }

    [Fact]
    public void Equals_ValidAndInvalid_ReturnsFalse()
    {
        // Arrange
        var valid = Validation.Valid(42);
        var invalid = Validation.Invalid<int>("error");

        // Assert
        Assert.False(valid.Equals(invalid));
    }

    [Fact]
    public void ToString_WhenValid_ReturnsValidFormat()
    {
        // Arrange
        var validation = Validation.Valid(42);

        // Act & Assert
        Assert.Equal("Valid(42)", validation.ToString());
    }

    [Fact]
    public void ToString_WhenInvalid_ReturnsInvalidFormat()
    {
        // Arrange
        var validation = Validation<int>.Invalid(new[] { "e1", "e2" });

        // Act & Assert
        Assert.Equal("Invalid([e1, e2])", validation.ToString());
    }
}
