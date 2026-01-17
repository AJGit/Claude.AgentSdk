using Claude.AgentSdk.Functional;

namespace Claude.AgentSdk.Tests.Functional;

/// <summary>
///     Tests for the Option monad.
/// </summary>
[UnitTest]
public class OptionTests
{
    [Fact]
    public void Some_CreatesOptionWithValue()
    {
        // Act
        var option = Option.Some(42);

        // Assert
        Assert.True(option.IsSome);
        Assert.False(option.IsNone);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void None_CreatesEmptyOption()
    {
        // Act
        var option = Option<int>.None;

        // Assert
        Assert.True(option.IsNone);
        Assert.False(option.IsSome);
    }

    [Fact]
    public void NoneOf_CreatesEmptyOption()
    {
        // Act
        var option = Option.NoneOf<string>();

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void ImplicitConversion_FromNonNullValue_CreatesSome()
    {
        // Act
        Option<string> option = "hello";

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal("hello", option.Value);
    }

    [Fact]
    public void ImplicitConversion_FromNull_CreatesNone()
    {
        // Act
        Option<string> option = null!;

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void ImplicitConversion_FromNoneType_CreatesNone()
    {
        // Act
        Option<int> option = Option.None;

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void Value_WhenSome_ReturnsValue()
    {
        // Arrange
        var option = Option.Some("test");

        // Act & Assert
        Assert.Equal("test", option.Value);
    }

    [Fact]
    public void Value_WhenNone_ThrowsInvalidOperationException()
    {
        // Arrange
        var option = Option<int>.None;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => option.Value);
    }

    [Fact]
    public void TryGetValue_WhenSome_ReturnsTrueAndValue()
    {
        // Arrange
        var option = Option.Some(42);

        // Act
        var result = option.TryGetValue(out var value);

        // Assert
        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_WhenNone_ReturnsFalse()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.TryGetValue(out var value);

        // Assert
        Assert.False(result);
        Assert.Equal(0, value);
    }

    [Fact]
    public void GetValueOrDefault_WhenSome_ReturnsValue()
    {
        // Arrange
        var option = Option.Some(42);

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetValueOrDefault_WhenNone_ReturnsDefault()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.GetValueOrDefault(99);

        // Assert
        Assert.Equal(99, result);
    }

    [Fact]
    public void GetValueOrElse_WhenSome_ReturnsValueWithoutCallingFactory()
    {
        // Arrange
        var option = Option.Some(42);
        var factoryCalled = false;

        // Act
        var result = option.GetValueOrElse(() =>
        {
            factoryCalled = true;
            return 99;
        });

        // Assert
        Assert.Equal(42, result);
        Assert.False(factoryCalled);
    }

    [Fact]
    public void GetValueOrElse_WhenNone_CallsFactory()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.GetValueOrElse(() => 99);

        // Assert
        Assert.Equal(99, result);
    }

    [Fact]
    public void Match_WhenSome_CallsSomeFunc()
    {
        // Arrange
        var option = Option.Some(5);

        // Act
        var result = option.Match(
            x => x * 2,
            () => 0);

        // Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public void Match_WhenNone_CallsNoneFunc()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.Match(
            x => x * 2,
            () => -1);

        // Assert
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Match_WithActions_WhenSome_CallsSomeAction()
    {
        // Arrange
        var option = Option.Some(42);
        var someCalled = false;
        var noneCalled = false;

        // Act
        option.Match(
            _ => someCalled = true,
            () => noneCalled = true);

        // Assert
        Assert.True(someCalled);
        Assert.False(noneCalled);
    }

    [Fact]
    public void Match_WithActions_WhenNone_CallsNoneAction()
    {
        // Arrange
        var option = Option<int>.None;
        var someCalled = false;
        var noneCalled = false;

        // Act
        option.Match(
            _ => someCalled = true,
            () => noneCalled = true);

        // Assert
        Assert.False(someCalled);
        Assert.True(noneCalled);
    }

    [Fact]
    public void Map_WhenSome_TransformsValue()
    {
        // Arrange
        var option = Option.Some(5);

        // Act
        var result = option.Map(x => x.ToString());

        // Assert
        Assert.True(result.IsSome);
        Assert.Equal("5", result.Value);
    }

    [Fact]
    public void Map_WhenNone_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.Map(x => x.ToString());

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task MapAsync_WhenSome_TransformsValue()
    {
        // Arrange
        var option = Option.Some(5);

        // Act
        var result = await option.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Assert
        Assert.True(result.IsSome);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public async Task MapAsync_WhenNone_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None;
        var mapperCalled = false;

        // Act
        var result = await option.MapAsync(async x =>
        {
            mapperCalled = true;
            await Task.Delay(1);
            return x * 2;
        });

        // Assert
        Assert.True(result.IsNone);
        Assert.False(mapperCalled);
    }

    [Fact]
    public void Bind_WhenSome_ChainsOperation()
    {
        // Arrange
        var option = Option.Some(10);

        Option<int> Halve(int x)
        {
            return x % 2 == 0 ? Option.Some(x / 2) : Option<int>.None;
        }

        // Act
        var result = option.Bind(Halve);

        // Assert
        Assert.True(result.IsSome);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Bind_WhenSomeButBinderReturnsNone_ReturnsNone()
    {
        // Arrange
        var option = Option.Some(9);

        Option<int> Halve(int x)
        {
            return x % 2 == 0 ? Option.Some(x / 2) : Option<int>.None;
        }

        // Act
        var result = option.Bind(Halve);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public void Bind_WhenNone_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None;
        var binderCalled = false;

        // Act
        var result = option.Bind(x =>
        {
            binderCalled = true;
            return Option.Some(x * 2);
        });

        // Assert
        Assert.True(result.IsNone);
        Assert.False(binderCalled);
    }

    [Fact]
    public async Task BindAsync_WhenSome_ChainsOperation()
    {
        // Arrange
        var option = Option.Some(10);

        // Act
        var result = await option.BindAsync(async x =>
        {
            await Task.Delay(1);
            return Option.Some(x / 2);
        });

        // Assert
        Assert.True(result.IsSome);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Where_WhenSomeAndPredicatePasses_ReturnsSame()
    {
        // Arrange
        var option = Option.Some(10);

        // Act
        var result = option.Where(x => x > 5);

        // Assert
        Assert.True(result.IsSome);
        Assert.Equal(10, result.Value);
    }

    [Fact]
    public void Where_WhenSomeAndPredicateFails_ReturnsNone()
    {
        // Arrange
        var option = Option.Some(3);

        // Act
        var result = option.Where(x => x > 5);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public void Where_WhenNone_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.Where(x => x > 5);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public void Do_WhenSome_ExecutesAction()
    {
        // Arrange
        var option = Option.Some(42);
        var capturedValue = 0;

        // Act
        var result = option.Do(x => capturedValue = x);

        // Assert
        Assert.Equal(42, capturedValue);
        Assert.Equal(option, result);
    }

    [Fact]
    public void Do_WhenNone_DoesNotExecuteAction()
    {
        // Arrange
        var option = Option<int>.None;
        var actionCalled = false;

        // Act
        option.Do(_ => actionCalled = true);

        // Assert
        Assert.False(actionCalled);
    }

    [Fact]
    public void Or_WhenSome_ReturnsOriginal()
    {
        // Arrange
        var option = Option.Some(1);
        var alternative = Option.Some(2);

        // Act
        var result = option.Or(alternative);

        // Assert
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void Or_WhenNone_ReturnsAlternative()
    {
        // Arrange
        var option = Option<int>.None;
        var alternative = Option.Some(2);

        // Act
        var result = option.Or(alternative);

        // Assert
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void OrElse_WhenSome_DoesNotCallFactory()
    {
        // Arrange
        var option = Option.Some(1);
        var factoryCalled = false;

        // Act
        var result = option.OrElse(() =>
        {
            factoryCalled = true;
            return Option.Some(2);
        });

        // Assert
        Assert.Equal(1, result.Value);
        Assert.False(factoryCalled);
    }

    [Fact]
    public void OrElse_WhenNone_CallsFactory()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.OrElse(() => Option.Some(2));

        // Assert
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void ToNullableRef_WhenSome_ReturnsValue()
    {
        // Arrange
        var option = Option.Some("hello");

        // Act
        var result = option.ToNullableRef();

        // Assert
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ToNullableRef_WhenNone_ReturnsNull()
    {
        // Arrange
        var option = Option<string>.None;

        // Act
        var result = option.ToNullableRef();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToEnumerable_WhenSome_ReturnsSingleElement()
    {
        // Arrange
        var option = Option.Some(42);

        // Act
        var result = option.ToEnumerable().ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(42, result[0]);
    }

    [Fact]
    public void ToEnumerable_WhenNone_ReturnsEmpty()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.ToEnumerable().ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ToList_WhenSome_ReturnsSingleElementList()
    {
        // Arrange
        var option = Option.Some(42);

        // Act
        var result = option.ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(42, result[0]);
    }

    [Fact]
    public void ToArray_WhenSome_ReturnsSingleElementArray()
    {
        // Arrange
        var option = Option.Some(42);

        // Act
        var result = option.ToArray();

        // Assert
        Assert.Single(result);
        Assert.Equal(42, result[0]);
    }

    [Fact]
    public void ToResult_WhenSome_ReturnsSuccess()
    {
        // Arrange
        var option = Option.Some(42);

        // Act
        var result = option.ToResult("not found");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToResult_WhenNone_ReturnsFailure()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.ToResult("not found");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("not found", result.Error);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        // Arrange
        var option1 = Option.Some(42);
        var option2 = Option.Some(42);

        // Assert
        Assert.True(option1.Equals(option2));
        Assert.True(option1 == option2);
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        // Arrange
        var option1 = Option.Some(42);
        var option2 = Option.Some(43);

        // Assert
        Assert.False(option1.Equals(option2));
        Assert.True(option1 != option2);
    }

    [Fact]
    public void Equals_BothNone_ReturnsTrue()
    {
        // Arrange
        var option1 = Option<int>.None;
        var option2 = Option<int>.None;

        // Assert
        Assert.True(option1.Equals(option2));
    }

    [Fact]
    public void Equals_SomeAndNone_ReturnsFalse()
    {
        // Arrange
        var some = Option.Some(42);
        var none = Option<int>.None;

        // Assert
        Assert.False(some.Equals(none));
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        // Arrange
        var option1 = Option.Some(42);
        var option2 = Option.Some(42);

        // Assert
        Assert.Equal(option1.GetHashCode(), option2.GetHashCode());
    }

    [Fact]
    public void ToString_WhenSome_ReturnsSomeFormat()
    {
        // Arrange
        var option = Option.Some(42);

        // Act & Assert
        Assert.Equal("Some(42)", option.ToString());
    }

    [Fact]
    public void ToString_WhenNone_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None;

        // Act & Assert
        Assert.Equal("None", option.ToString());
    }

    [Fact]
    public void FromNullable_Class_WithValue_ReturnsSome()
    {
        // Act
        var option = Option.FromNullable("hello");

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal("hello", option.Value);
    }

    [Fact]
    public void FromNullable_Class_WithNull_ReturnsNone()
    {
        // Act
        var option = Option.FromNullable((string?)null);

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void FromNullable_Struct_WithValue_ReturnsSome()
    {
        // Act
        var option = Option.FromNullable((int?)42);

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void FromNullable_Struct_WithNull_ReturnsNone()
    {
        // Act
        var option = Option.FromNullable((int?)null);

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void TryParseInt_ValidInput_ReturnsSome()
    {
        // Act
        var option = Option.TryParseInt("42");

        // Assert
        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void TryParseInt_InvalidInput_ReturnsNone()
    {
        // Act
        var option = Option.TryParseInt("not a number");

        // Assert
        Assert.True(option.IsNone);
    }

    [Fact]
    public void Sequence_AllSome_ReturnsSomeWithList()
    {
        // Arrange
        var options = new[]
        {
            Option.Some(1),
            Option.Some(2),
            Option.Some(3)
        };

        // Act
        var result = options.Sequence();

        // Assert
        Assert.True(result.IsSome);
        Assert.Equal([1, 2, 3], result.Value);
    }

    [Fact]
    public void Sequence_AnyNone_ReturnsNone()
    {
        // Arrange
        var options = new[]
        {
            Option.Some(1),
            Option<int>.None,
            Option.Some(3)
        };

        // Act
        var result = options.Sequence();

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public void Traverse_AllSucceed_ReturnsSome()
    {
        // Arrange
        var numbers = new[] { "1", "2", "3" };

        // Act
        var result = numbers.Traverse(Option.TryParseInt);

        // Assert
        Assert.True(result.IsSome);
        Assert.Equal([1, 2, 3], result.Value);
    }

    [Fact]
    public void Traverse_AnyFails_ReturnsNone()
    {
        // Arrange
        var numbers = new[] { "1", "invalid", "3" };

        // Act
        var result = numbers.Traverse(Option.TryParseInt);

        // Assert
        Assert.True(result.IsNone);
    }
}
