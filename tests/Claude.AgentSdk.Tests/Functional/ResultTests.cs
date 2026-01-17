using Claude.AgentSdk.Functional;

namespace Claude.AgentSdk.Tests.Functional;

/// <summary>
///     Tests for the Result monad.
/// </summary>
[UnitTest]
public class ResultTests
{
    #region Creation Tests

    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Act
        var result = Result.Success(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        // Act
        var result = Result.Failure<int>("error message");

        // Assert
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal("error message", result.Error);
    }

    [Fact]
    public void Success_WithTypedError_CreatesSuccessfulResult()
    {
        // Act
        var result = Result.Success<int, Exception>(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_WithTypedError_CreatesFailedResult()
    {
        // Act
        var error = new InvalidOperationException("test");
        var result = Result.Failure<int, Exception>(error);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    #endregion

    #region Value Access Tests

    [Fact]
    public void Value_WhenSuccess_ReturnsValue()
    {
        // Arrange
        var result = Result.Success(42);

        // Act & Assert
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Value_WhenFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_WhenFailure_ReturnsError()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act & Assert
        Assert.Equal("error", result.Error);
    }

    [Fact]
    public void Error_WhenSuccess_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result.Success(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void TryGetValue_WhenSuccess_ReturnsTrueAndValue()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var success = result.TryGetValue(out var value);

        // Assert
        Assert.True(success);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_WhenFailure_ReturnsFalse()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var success = result.TryGetValue(out _);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public void TryGetError_WhenFailure_ReturnsTrueAndError()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var hasError = result.TryGetError(out var error);

        // Assert
        Assert.True(hasError);
        Assert.Equal("error", error);
    }

    [Fact]
    public void GetValueOrDefault_WhenSuccess_ReturnsValue()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var value = result.GetValueOrDefault(0);

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void GetValueOrDefault_WhenFailure_ReturnsDefault()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var value = result.GetValueOrDefault(99);

        // Assert
        Assert.Equal(99, value);
    }

    [Fact]
    public void GetValueOrElse_WhenSuccess_ReturnsValueWithoutCallingFactory()
    {
        // Arrange
        var result = Result.Success(42);
        var factoryCalled = false;

        // Act
        var value = result.GetValueOrElse(_ => { factoryCalled = true; return 99; });

        // Assert
        Assert.Equal(42, value);
        Assert.False(factoryCalled);
    }

    [Fact]
    public void GetValueOrElse_WhenFailure_CallsFactory()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var value = result.GetValueOrElse(err => err.Length);

        // Assert
        Assert.Equal(5, value); // "error".Length
    }

    [Fact]
    public void GetValueOrThrow_WhenSuccess_ReturnsValue()
    {
        // Arrange
        var result = Result.Success(42);

        // Act & Assert
        Assert.Equal(42, result.GetValueOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_WhenFailure_Throws()
    {
        // Arrange
        var result = Result.Failure<int>("error message");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_WithFactory_WhenFailure_ThrowsCustomException()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            result.GetValueOrThrow(err => new ArgumentException(err)));
        Assert.Equal("error", ex.Message);
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_WhenSuccess_CallsSuccessFunc()
    {
        // Arrange
        var result = Result.Success(5);

        // Act
        var output = result.Match(
            success: x => x * 2,
            failure: _ => -1);

        // Assert
        Assert.Equal(10, output);
    }

    [Fact]
    public void Match_WhenFailure_CallsFailureFunc()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var output = result.Match(
            success: x => x * 2,
            failure: err => err.Length);

        // Assert
        Assert.Equal(5, output);
    }

    [Fact]
    public void Match_WithActions_WhenSuccess_CallsSuccessAction()
    {
        // Arrange
        var result = Result.Success(42);
        var successCalled = false;
        var failureCalled = false;

        // Act
        result.Match(
            success: _ => successCalled = true,
            failure: _ => failureCalled = true);

        // Assert
        Assert.True(successCalled);
        Assert.False(failureCalled);
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_WhenSuccess_TransformsValue()
    {
        // Arrange
        var result = Result.Success(5);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal("5", mapped.Value);
    }

    [Fact]
    public void Map_WhenFailure_PreservesError()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal("error", mapped.Error);
    }

    [Fact]
    public async Task MapAsync_WhenSuccess_TransformsValue()
    {
        // Arrange
        var result = Result.Success(5);

        // Act
        var mapped = await result.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(10, mapped.Value);
    }

    [Fact]
    public void MapError_WhenFailure_TransformsError()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var mapped = result.MapError(err => err.ToUpper());

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.Equal("ERROR", mapped.Error);
    }

    [Fact]
    public void MapError_WhenSuccess_PreservesValue()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var mapped = result.MapError(err => err.ToUpper());

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(42, mapped.Value);
    }

    #endregion

    #region Bind Tests

    [Fact]
    public void Bind_WhenSuccess_ChainsOperation()
    {
        // Arrange
        var result = Result.Success(10);
        Result<int> Halve(int x) => x % 2 == 0
            ? Result.Success(x / 2)
            : Result.Failure<int>("Not even");

        // Act
        var bound = result.Bind(Halve);

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public void Bind_WhenSuccessButBinderFails_ReturnsFailure()
    {
        // Arrange
        var result = Result.Success(9);
        Result<int> Halve(int x) => x % 2 == 0
            ? Result.Success(x / 2)
            : Result.Failure<int>("Not even");

        // Act
        var bound = result.Bind(Halve);

        // Assert
        Assert.True(bound.IsFailure);
        Assert.Equal("Not even", bound.Error);
    }

    [Fact]
    public void Bind_WhenFailure_ShortCircuits()
    {
        // Arrange
        var result = Result.Failure<int>("initial error");
        var binderCalled = false;

        // Act
        var bound = result.Bind(x =>
        {
            binderCalled = true;
            return Result.Success(x * 2);
        });

        // Assert
        Assert.True(bound.IsFailure);
        Assert.Equal("initial error", bound.Error);
        Assert.False(binderCalled);
    }

    [Fact]
    public async Task BindAsync_WhenSuccess_ChainsOperation()
    {
        // Arrange
        var result = Result.Success(10);

        // Act
        var bound = await result.BindAsync(async x =>
        {
            await Task.Delay(1);
            return Result.Success(x / 2);
        });

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    #endregion

    #region Do Tests

    [Fact]
    public void Do_WhenSuccess_ExecutesAction()
    {
        // Arrange
        var result = Result.Success(42);
        var capturedValue = 0;

        // Act
        result.Do(x => capturedValue = x);

        // Assert
        Assert.Equal(42, capturedValue);
    }

    [Fact]
    public void Do_WhenFailure_DoesNotExecuteAction()
    {
        // Arrange
        var result = Result.Failure<int>("error");
        var actionCalled = false;

        // Act
        result.Do(_ => actionCalled = true);

        // Assert
        Assert.False(actionCalled);
    }

    [Fact]
    public void DoOnError_WhenFailure_ExecutesAction()
    {
        // Arrange
        var result = Result.Failure<int>("error");
        var capturedError = "";

        // Act
        result.DoOnError(err => capturedError = err);

        // Assert
        Assert.Equal("error", capturedError);
    }

    [Fact]
    public void DoOnError_WhenSuccess_DoesNotExecuteAction()
    {
        // Arrange
        var result = Result.Success(42);
        var actionCalled = false;

        // Act
        result.DoOnError(_ => actionCalled = true);

        // Assert
        Assert.False(actionCalled);
    }

    #endregion

    #region Ensure Tests

    [Fact]
    public void Ensure_WhenSuccessAndPredicatePasses_ReturnsSuccess()
    {
        // Arrange
        var result = Result.Success(10);

        // Act
        var ensured = result.Ensure(x => x > 5, "Value must be greater than 5");

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal(10, ensured.Value);
    }

    [Fact]
    public void Ensure_WhenSuccessAndPredicateFails_ReturnsFailure()
    {
        // Arrange
        var result = Result.Success(3);

        // Act
        var ensured = result.Ensure(x => x > 5, "Value must be greater than 5");

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.Equal("Value must be greater than 5", ensured.Error);
    }

    [Fact]
    public void Ensure_WhenFailure_PreservesError()
    {
        // Arrange
        var result = Result.Failure<int>("initial error");

        // Act
        var ensured = result.Ensure(x => x > 5, "Value must be greater than 5");

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.Equal("initial error", ensured.Error);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ToOption_WhenSuccess_ReturnsSome()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var option = result.ToOption();

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void ToOption_WhenFailure_ReturnsNone()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act
        var option = result.ToOption();

        // Assert
        Assert.True(option.IsNone);
    }

    #endregion

    #region Try Tests

    [Fact]
    public void Try_WhenSucceeds_ReturnsSuccess()
    {
        // Act
        var result = Result.Try(() => 42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Try_WhenThrows_ReturnsFailure()
    {
        // Act
        var result = Result.Try<int>(() => throw new InvalidOperationException("test error"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("test error", result.Error);
    }

    [Fact]
    public async Task TryAsync_WhenSucceeds_ReturnsSuccess()
    {
        // Act
        var result = await Result.TryAsync(async () =>
        {
            await Task.Delay(1);
            return 42;
        });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TryAsync_WhenThrows_ReturnsFailure()
    {
        // Act
        var result = await Result.TryAsync<int>(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("async error");
        });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("async error", result.Error);
    }

    #endregion

    #region Combine Tests

    [Fact]
    public void Combine_AllSuccess_ReturnsCombinedSuccess()
    {
        // Arrange
        var r1 = Result.Success(1);
        var r2 = Result.Success("two");

        // Act
        var combined = Result.Combine(r1, r2);

        // Assert
        Assert.True(combined.IsSuccess);
        Assert.Equal((1, "two"), combined.Value);
    }

    [Fact]
    public void Combine_FirstFails_ReturnsFirstError()
    {
        // Arrange
        var r1 = Result.Failure<int>("first error");
        var r2 = Result.Success("two");

        // Act
        var combined = Result.Combine(r1, r2);

        // Assert
        Assert.True(combined.IsFailure);
        Assert.Equal("first error", combined.Error);
    }

    [Fact]
    public void Combine_SecondFails_ReturnsSecondError()
    {
        // Arrange
        var r1 = Result.Success(1);
        var r2 = Result.Failure<string>("second error");

        // Act
        var combined = Result.Combine(r1, r2);

        // Assert
        Assert.True(combined.IsFailure);
        Assert.Equal("second error", combined.Error);
    }

    [Fact]
    public void Combine_Three_AllSuccess_ReturnsCombinedSuccess()
    {
        // Arrange
        var r1 = Result.Success(1);
        var r2 = Result.Success("two");
        var r3 = Result.Success(3.0);

        // Act
        var combined = Result.Combine(r1, r2, r3);

        // Assert
        Assert.True(combined.IsSuccess);
        Assert.Equal((1, "two", 3.0), combined.Value);
    }

    #endregion

    #region Sequence and Traverse Tests

    [Fact]
    public void Sequence_AllSuccess_ReturnsSuccessWithList()
    {
        // Arrange
        var results = new[]
        {
            Result.Success(1),
            Result.Success(2),
            Result.Success(3)
        };

        // Act
        var sequenced = results.Sequence();

        // Assert
        Assert.True(sequenced.IsSuccess);
        Assert.Equal([1, 2, 3], sequenced.Value);
    }

    [Fact]
    public void Sequence_AnyFailure_ReturnsFirstError()
    {
        // Arrange
        var results = new[]
        {
            Result.Success(1),
            Result.Failure<int>("error"),
            Result.Success(3)
        };

        // Act
        var sequenced = results.Sequence();

        // Assert
        Assert.True(sequenced.IsFailure);
        Assert.Equal("error", sequenced.Error);
    }

    [Fact]
    public void Traverse_AllSucceed_ReturnsSuccess()
    {
        // Arrange
        var numbers = new[] { "1", "2", "3" };

        // Act
        var result = numbers.Traverse(s =>
            int.TryParse(s, out var n)
                ? Result.Success(n)
                : Result.Failure<int>("Not a number"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], result.Value);
    }

    [Fact]
    public void Traverse_AnyFails_ReturnsFirstError()
    {
        // Arrange
        var numbers = new[] { "1", "invalid", "3" };

        // Act
        var result = numbers.Traverse(s =>
            int.TryParse(s, out var n)
                ? Result.Success(n)
                : Result.Failure<int>("Not a number"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Not a number", result.Error);
    }

    #endregion

    #region FromNullable Tests

    [Fact]
    public void FromNullable_Class_WithValue_ReturnsSuccess()
    {
        // Act
        var result = Result.FromNullable("hello", "was null");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void FromNullable_Class_WithNull_ReturnsFailure()
    {
        // Act
        var result = Result.FromNullable((string?)null, "was null");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("was null", result.Error);
    }

    [Fact]
    public void FromNullable_Struct_WithValue_ReturnsSuccess()
    {
        // Act
        var result = Result.FromNullable((int?)42, "was null");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FromNullable_Struct_WithNull_ReturnsFailure()
    {
        // Act
        var result = Result.FromNullable((int?)null, "was null");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("was null", result.Error);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameSuccessValue_ReturnsTrue()
    {
        // Arrange
        var r1 = Result.Success(42);
        var r2 = Result.Success(42);

        // Assert
        Assert.True(r1.Equals(r2));
        Assert.True(r1 == r2);
    }

    [Fact]
    public void Equals_DifferentSuccessValue_ReturnsFalse()
    {
        // Arrange
        var r1 = Result.Success(42);
        var r2 = Result.Success(43);

        // Assert
        Assert.False(r1.Equals(r2));
        Assert.True(r1 != r2);
    }

    [Fact]
    public void Equals_SameFailureError_ReturnsTrue()
    {
        // Arrange
        var r1 = Result.Failure<int>("error");
        var r2 = Result.Failure<int>("error");

        // Assert
        Assert.True(r1.Equals(r2));
    }

    [Fact]
    public void Equals_SuccessAndFailure_ReturnsFalse()
    {
        // Arrange
        var success = Result.Success(42);
        var failure = Result.Failure<int>("error");

        // Assert
        Assert.False(success.Equals(failure));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WhenSuccess_ReturnsSuccessFormat()
    {
        // Arrange
        var result = Result.Success(42);

        // Act & Assert
        Assert.Equal("Success(42)", result.ToString());
    }

    [Fact]
    public void ToString_WhenFailure_ReturnsFailureFormat()
    {
        // Arrange
        var result = Result.Failure<int>("error");

        // Act & Assert
        Assert.Equal("Failure(error)", result.ToString());
    }

    #endregion
}
