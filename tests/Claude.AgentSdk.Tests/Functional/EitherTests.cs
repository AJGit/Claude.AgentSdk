using Claude.AgentSdk.Functional;

namespace Claude.AgentSdk.Tests.Functional;

/// <summary>
///     Tests for the Either discriminated union.
/// </summary>
[UnitTest]
public class EitherTests
{
    #region Creation Tests

    [Fact]
    public void Left_CreatesLeftValue()
    {
        // Act
        var either = Either<string, int>.Left("error");

        // Assert
        Assert.True(either.IsLeft);
        Assert.False(either.IsRight);
        Assert.Equal("error", either.LeftValue);
    }

    [Fact]
    public void Right_CreatesRightValue()
    {
        // Act
        var either = Either<string, int>.Right(42);

        // Assert
        Assert.True(either.IsRight);
        Assert.False(either.IsLeft);
        Assert.Equal(42, either.RightValue);
    }

    [Fact]
    public void StaticLeft_CreatesLeftValue()
    {
        // Act
        var either = Either.Left<string, int>("error");

        // Assert
        Assert.True(either.IsLeft);
        Assert.Equal("error", either.LeftValue);
    }

    [Fact]
    public void StaticRight_CreatesRightValue()
    {
        // Act
        var either = Either.Right<string, int>(42);

        // Assert
        Assert.True(either.IsRight);
        Assert.Equal(42, either.RightValue);
    }

    [Fact]
    public void ImplicitConversion_FromLeftValue_CreatesLeft()
    {
        // Act
        Either<string, int> either = Either.Left("error");

        // Assert
        Assert.True(either.IsLeft);
        Assert.Equal("error", either.LeftValue);
    }

    [Fact]
    public void ImplicitConversion_FromRightValue_CreatesRight()
    {
        // Act
        Either<string, int> either = Either.Right(42);

        // Assert
        Assert.True(either.IsRight);
        Assert.Equal(42, either.RightValue);
    }

    #endregion

    #region Value Access Tests

    [Fact]
    public void LeftValue_WhenLeft_ReturnsValue()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act & Assert
        Assert.Equal("error", either.LeftValue);
    }

    [Fact]
    public void LeftValue_WhenRight_ThrowsInvalidOperationException()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => either.LeftValue);
    }

    [Fact]
    public void RightValue_WhenRight_ReturnsValue()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act & Assert
        Assert.Equal(42, either.RightValue);
    }

    [Fact]
    public void RightValue_WhenLeft_ThrowsInvalidOperationException()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => either.RightValue);
    }

    [Fact]
    public void TryGetLeft_WhenLeft_ReturnsTrueAndValue()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var result = either.TryGetLeft(out var value);

        // Assert
        Assert.True(result);
        Assert.Equal("error", value);
    }

    [Fact]
    public void TryGetLeft_WhenRight_ReturnsFalse()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var result = either.TryGetLeft(out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryGetRight_WhenRight_ReturnsTrueAndValue()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var result = either.TryGetRight(out var value);

        // Assert
        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetRight_WhenLeft_ReturnsFalse()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var result = either.TryGetRight(out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetRightOrDefault_WhenRight_ReturnsValue()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var value = either.GetRightOrDefault(0);

        // Assert
        Assert.Equal(42, value);
    }

    [Fact]
    public void GetRightOrDefault_WhenLeft_ReturnsDefault()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var value = either.GetRightOrDefault(99);

        // Assert
        Assert.Equal(99, value);
    }

    [Fact]
    public void GetLeftOrDefault_WhenLeft_ReturnsValue()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var value = either.GetLeftOrDefault("default");

        // Assert
        Assert.Equal("error", value);
    }

    [Fact]
    public void GetRightOrElse_WhenRight_ReturnsValueWithoutCallingFactory()
    {
        // Arrange
        var either = Either<string, int>.Right(42);
        var factoryCalled = false;

        // Act
        var value = either.GetRightOrElse(_ => { factoryCalled = true; return 99; });

        // Assert
        Assert.Equal(42, value);
        Assert.False(factoryCalled);
    }

    [Fact]
    public void GetRightOrElse_WhenLeft_CallsFactory()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var value = either.GetRightOrElse(left => left.Length);

        // Assert
        Assert.Equal(5, value); // "error".Length
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_WhenRight_CallsRightFunc()
    {
        // Arrange
        var either = Either<string, int>.Right(5);

        // Act
        var result = either.Match(
            left: _ => -1,
            right: x => x * 2);

        // Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public void Match_WhenLeft_CallsLeftFunc()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var result = either.Match(
            left: left => left.Length,
            right: _ => -1);

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public void Match_WithActions_WhenRight_CallsRightAction()
    {
        // Arrange
        var either = Either<string, int>.Right(42);
        var leftCalled = false;
        var rightCalled = false;

        // Act
        either.Match(
            left: _ => leftCalled = true,
            right: _ => rightCalled = true);

        // Assert
        Assert.False(leftCalled);
        Assert.True(rightCalled);
    }

    [Fact]
    public void Match_WithActions_WhenLeft_CallsLeftAction()
    {
        // Arrange
        var either = Either<string, int>.Left("error");
        var leftCalled = false;
        var rightCalled = false;

        // Act
        either.Match(
            left: _ => leftCalled = true,
            right: _ => rightCalled = true);

        // Assert
        Assert.True(leftCalled);
        Assert.False(rightCalled);
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_WhenRight_TransformsValue()
    {
        // Arrange
        var either = Either<string, int>.Right(5);

        // Act
        var result = either.Map(x => x.ToString());

        // Assert
        Assert.True(result.IsRight);
        Assert.Equal("5", result.RightValue);
    }

    [Fact]
    public void Map_WhenLeft_PreservesLeft()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var result = either.Map(x => x.ToString());

        // Assert
        Assert.True(result.IsLeft);
        Assert.Equal("error", result.LeftValue);
    }

    [Fact]
    public async Task MapAsync_WhenRight_TransformsValue()
    {
        // Arrange
        var either = Either<string, int>.Right(5);

        // Act
        var result = await either.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Assert
        Assert.True(result.IsRight);
        Assert.Equal(10, result.RightValue);
    }

    [Fact]
    public void MapLeft_WhenLeft_TransformsValue()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var result = either.MapLeft(x => x.ToUpper());

        // Assert
        Assert.True(result.IsLeft);
        Assert.Equal("ERROR", result.LeftValue);
    }

    [Fact]
    public void MapLeft_WhenRight_PreservesRight()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var result = either.MapLeft(x => x.ToUpper());

        // Assert
        Assert.True(result.IsRight);
        Assert.Equal(42, result.RightValue);
    }

    [Fact]
    public void BiMap_TransformsBothSides()
    {
        // Arrange
        var leftEither = Either<string, int>.Left("error");
        var rightEither = Either<string, int>.Right(5);

        // Act
        var leftResult = leftEither.BiMap(
            leftMapper: s => s.ToUpper(),
            rightMapper: n => n * 2);
        var rightResult = rightEither.BiMap(
            leftMapper: s => s.ToUpper(),
            rightMapper: n => n * 2);

        // Assert
        Assert.True(leftResult.IsLeft);
        Assert.Equal("ERROR", leftResult.LeftValue);
        Assert.True(rightResult.IsRight);
        Assert.Equal(10, rightResult.RightValue);
    }

    #endregion

    #region Bind Tests

    [Fact]
    public void Bind_WhenRight_ChainsOperation()
    {
        // Arrange
        var either = Either<string, int>.Right(10);
        Either<string, int> Halve(int x) => x % 2 == 0
            ? Either<string, int>.Right(x / 2)
            : Either<string, int>.Left("Not even");

        // Act
        var result = either.Bind(Halve);

        // Assert
        Assert.True(result.IsRight);
        Assert.Equal(5, result.RightValue);
    }

    [Fact]
    public void Bind_WhenRightButBinderReturnsLeft_ReturnsLeft()
    {
        // Arrange
        var either = Either<string, int>.Right(9);
        Either<string, int> Halve(int x) => x % 2 == 0
            ? Either<string, int>.Right(x / 2)
            : Either<string, int>.Left("Not even");

        // Act
        var result = either.Bind(Halve);

        // Assert
        Assert.True(result.IsLeft);
        Assert.Equal("Not even", result.LeftValue);
    }

    [Fact]
    public void Bind_WhenLeft_ShortCircuits()
    {
        // Arrange
        var either = Either<string, int>.Left("error");
        var binderCalled = false;

        // Act
        var result = either.Bind(x =>
        {
            binderCalled = true;
            return Either<string, int>.Right(x * 2);
        });

        // Assert
        Assert.True(result.IsLeft);
        Assert.Equal("error", result.LeftValue);
        Assert.False(binderCalled);
    }

    [Fact]
    public async Task BindAsync_WhenRight_ChainsOperation()
    {
        // Arrange
        var either = Either<string, int>.Right(10);

        // Act
        var result = await either.BindAsync(async x =>
        {
            await Task.Delay(1);
            return Either<string, int>.Right(x / 2);
        });

        // Assert
        Assert.True(result.IsRight);
        Assert.Equal(5, result.RightValue);
    }

    #endregion

    #region Do Tests

    [Fact]
    public void DoRight_WhenRight_ExecutesAction()
    {
        // Arrange
        var either = Either<string, int>.Right(42);
        var capturedValue = 0;

        // Act
        either.DoRight(x => capturedValue = x);

        // Assert
        Assert.Equal(42, capturedValue);
    }

    [Fact]
    public void DoRight_WhenLeft_DoesNotExecuteAction()
    {
        // Arrange
        var either = Either<string, int>.Left("error");
        var actionCalled = false;

        // Act
        either.DoRight(_ => actionCalled = true);

        // Assert
        Assert.False(actionCalled);
    }

    [Fact]
    public void DoLeft_WhenLeft_ExecutesAction()
    {
        // Arrange
        var either = Either<string, int>.Left("error");
        var capturedValue = "";

        // Act
        either.DoLeft(x => capturedValue = x);

        // Assert
        Assert.Equal("error", capturedValue);
    }

    [Fact]
    public void DoLeft_WhenRight_DoesNotExecuteAction()
    {
        // Arrange
        var either = Either<string, int>.Right(42);
        var actionCalled = false;

        // Act
        either.DoLeft(_ => actionCalled = true);

        // Assert
        Assert.False(actionCalled);
    }

    #endregion

    #region Swap Tests

    [Fact]
    public void Swap_WhenLeft_BecomesRight()
    {
        // Arrange
        var either = Either<string, int>.Left("value");

        // Act
        var swapped = either.Swap();

        // Assert
        Assert.True(swapped.IsRight);
        Assert.Equal("value", swapped.RightValue);
    }

    [Fact]
    public void Swap_WhenRight_BecomesLeft()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var swapped = either.Swap();

        // Assert
        Assert.True(swapped.IsLeft);
        Assert.Equal(42, swapped.LeftValue);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ToOption_WhenRight_ReturnsSome()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var option = either.ToOption();

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void ToOption_WhenLeft_ReturnsNone()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var option = either.ToOption();

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void ToLeftOption_WhenLeft_ReturnsSome()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var option = either.ToLeftOption();

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal("error", option.Value);
    }

    [Fact]
    public void ToLeftOption_WhenRight_ReturnsNone()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var option = either.ToLeftOption();

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void ToResult_WhenRight_ReturnsSuccess()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act
        var result = either.ToResult();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToResult_WhenLeft_ReturnsFailure()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act
        var result = either.ToResult();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("error", result.Error);
    }

    #endregion

    #region Merge Tests

    [Fact]
    public void Merge_WhenSameType_ReturnsValue()
    {
        // Arrange
        var leftEither = Either<string, string>.Left("left value");
        var rightEither = Either<string, string>.Right("right value");

        // Act
        var leftMerged = leftEither.Merge();
        var rightMerged = rightEither.Merge();

        // Assert
        Assert.Equal("left value", leftMerged);
        Assert.Equal("right value", rightMerged);
    }

    #endregion

    #region Sequence and Traverse Tests

    [Fact]
    public void Sequence_AllRight_ReturnsRightWithList()
    {
        // Arrange
        var eithers = new[]
        {
            Either<string, int>.Right(1),
            Either<string, int>.Right(2),
            Either<string, int>.Right(3)
        };

        // Act
        var result = eithers.Sequence();

        // Assert
        Assert.True(result.IsRight);
        Assert.Equal([1, 2, 3], result.RightValue);
    }

    [Fact]
    public void Sequence_AnyLeft_ReturnsFirstLeft()
    {
        // Arrange
        var eithers = new[]
        {
            Either<string, int>.Right(1),
            Either<string, int>.Left("error"),
            Either<string, int>.Right(3)
        };

        // Act
        var result = eithers.Sequence();

        // Assert
        Assert.True(result.IsLeft);
        Assert.Equal("error", result.LeftValue);
    }

    [Fact]
    public void Traverse_AllSucceed_ReturnsRight()
    {
        // Arrange
        var numbers = new[] { "1", "2", "3" };

        // Act
        var result = numbers.Traverse(s =>
            int.TryParse(s, out var n)
                ? Either<string, int>.Right(n)
                : Either<string, int>.Left("Not a number"));

        // Assert
        Assert.True(result.IsRight);
        Assert.Equal([1, 2, 3], result.RightValue);
    }

    [Fact]
    public void Partition_SeparatesLeftsAndRights()
    {
        // Arrange
        var eithers = new[]
        {
            Either<string, int>.Right(1),
            Either<string, int>.Left("error1"),
            Either<string, int>.Right(2),
            Either<string, int>.Left("error2")
        };

        // Act
        var (lefts, rights) = eithers.Partition();

        // Assert
        Assert.Equal(["error1", "error2"], lefts);
        Assert.Equal([1, 2], rights);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameRight_ReturnsTrue()
    {
        // Arrange
        var e1 = Either<string, int>.Right(42);
        var e2 = Either<string, int>.Right(42);

        // Assert
        Assert.True(e1.Equals(e2));
        Assert.True(e1 == e2);
    }

    [Fact]
    public void Equals_DifferentRight_ReturnsFalse()
    {
        // Arrange
        var e1 = Either<string, int>.Right(42);
        var e2 = Either<string, int>.Right(43);

        // Assert
        Assert.False(e1.Equals(e2));
        Assert.True(e1 != e2);
    }

    [Fact]
    public void Equals_SameLeft_ReturnsTrue()
    {
        // Arrange
        var e1 = Either<string, int>.Left("error");
        var e2 = Either<string, int>.Left("error");

        // Assert
        Assert.True(e1.Equals(e2));
    }

    [Fact]
    public void Equals_LeftAndRight_ReturnsFalse()
    {
        // Arrange
        var left = Either<string, int>.Left("42");
        var right = Either<string, int>.Right(42);

        // Assert
        Assert.False(left.Equals(right));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WhenRight_ReturnsRightFormat()
    {
        // Arrange
        var either = Either<string, int>.Right(42);

        // Act & Assert
        Assert.Equal("Right(42)", either.ToString());
    }

    [Fact]
    public void ToString_WhenLeft_ReturnsLeftFormat()
    {
        // Arrange
        var either = Either<string, int>.Left("error");

        // Act & Assert
        Assert.Equal("Left(error)", either.ToString());
    }

    #endregion
}
