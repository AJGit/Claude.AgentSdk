using System.Diagnostics.CodeAnalysis;

// Suppress CA1000: Static members on generic types are the standard pattern for functional types
#pragma warning disable CA1000

namespace Claude.AgentSdk.Functional;

/// <summary>
///     Represents a value that can be one of two types: Left or Right.
///     By convention, Left represents failure/error and Right represents success.
/// </summary>
/// <typeparam name="TLeft">The left (typically error) type.</typeparam>
/// <typeparam name="TRight">The right (typically success) type.</typeparam>
/// <remarks>
///     <para>
///         Either is a discriminated union that forces explicit handling of both cases.
///         It's more general than Result as both sides are "equal" (no implicit success/failure semantics).
///     </para>
///     <para>
///         Example usage:
///         <code>
///     Either&lt;string, int&gt; ParseInt(string s) =>
///         int.TryParse(s, out var result)
///             ? Either.Right&lt;string, int&gt;(result)
///             : Either.Left&lt;string, int&gt;($"Cannot parse '{s}' as int");
/// 
///     var result = ParseInt("42").Match(
///         left: err => $"Error: {err}",
///         right: num => $"Number: {num}"
///     );
///     </code>
///     </para>
/// </remarks>
public readonly struct Either<TLeft, TRight> : IEquatable<Either<TLeft, TRight>>
{
    private readonly TLeft _left;
    private readonly TRight _right;

    private Either(TLeft left)
    {
        _left = left;
        _right = default!;
        IsRight = false;
    }

    private Either(TRight right, bool _)
    {
        _left = default!;
        _right = right;
        IsRight = true;
    }

    /// <summary>
    ///     Gets whether this is a Left value.
    /// </summary>
    public bool IsLeft => !IsRight;

    /// <summary>
    ///     Gets whether this is a Right value.
    /// </summary>
    public bool IsRight { get; }

    /// <summary>
    ///     Creates a Left value.
    /// </summary>
    public static Either<TLeft, TRight> Left(TLeft value) => new(value);

    /// <summary>
    ///     Creates a Right value.
    /// </summary>
    public static Either<TLeft, TRight> Right(TRight value) => new(value, true);

    /// <summary>
    ///     Gets the Left value, or throws if this is Right.
    /// </summary>
    public TLeft LeftValue => !IsRight
        ? _left
        : throw new InvalidOperationException("Either is Right, not Left.");

    /// <summary>
    ///     Gets the Right value, or throws if this is Left.
    /// </summary>
    public TRight RightValue => IsRight
        ? _right
        : throw new InvalidOperationException("Either is Left, not Right.");

    /// <summary>
    ///     Tries to get the Left value.
    /// </summary>
    public bool TryGetLeft([MaybeNullWhen(false)] out TLeft value)
    {
        value = _left;
        return !IsRight;
    }

    /// <summary>
    ///     Tries to get the Right value.
    /// </summary>
    public bool TryGetRight([MaybeNullWhen(false)] out TRight value)
    {
        value = _right;
        return IsRight;
    }

    /// <summary>
    ///     Pattern matches on the Either.
    /// </summary>
    public TResult Match<TResult>(Func<TLeft, TResult> left, Func<TRight, TResult> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return IsRight ? right(_right) : left(_left);
    }

    /// <summary>
    ///     Pattern matches on the Either with side effects.
    /// </summary>
    public void Match(Action<TLeft> left, Action<TRight> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (IsRight)
        {
            right(_right);
        }
        else
        {
            left(_left);
        }
    }

    /// <summary>
    ///     Transforms the Right value if present.
    /// </summary>
    public Either<TLeft, TResult> Map<TResult>(Func<TRight, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsRight
            ? Either<TLeft, TResult>.Right(mapper(_right))
            : Either<TLeft, TResult>.Left(_left);
    }

    /// <summary>
    ///     Transforms the Right value if present (async).
    /// </summary>
    public async Task<Either<TLeft, TResult>> MapAsync<TResult>(Func<TRight, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsRight
            ? Either<TLeft, TResult>.Right(await mapper(_right).ConfigureAwait(false))
            : Either<TLeft, TResult>.Left(_left);
    }

    /// <summary>
    ///     Transforms the Left value if present.
    /// </summary>
    public Either<TNewLeft, TRight> MapLeft<TNewLeft>(Func<TLeft, TNewLeft> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsRight
            ? Either<TNewLeft, TRight>.Right(_right)
            : Either<TNewLeft, TRight>.Left(mapper(_left));
    }

    /// <summary>
    ///     Transforms both values.
    /// </summary>
    public Either<TNewLeft, TNewRight> BiMap<TNewLeft, TNewRight>(
        Func<TLeft, TNewLeft> leftMapper,
        Func<TRight, TNewRight> rightMapper)
    {
        ArgumentNullException.ThrowIfNull(leftMapper);
        ArgumentNullException.ThrowIfNull(rightMapper);
        return IsRight
            ? Either<TNewLeft, TNewRight>.Right(rightMapper(_right))
            : Either<TNewLeft, TNewRight>.Left(leftMapper(_left));
    }

    /// <summary>
    ///     Chains Either-returning operations on the Right value.
    /// </summary>
    public Either<TLeft, TResult> Bind<TResult>(Func<TRight, Either<TLeft, TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsRight ? binder(_right) : Either<TLeft, TResult>.Left(_left);
    }

    /// <summary>
    ///     Chains Either-returning operations on the Right value (async).
    /// </summary>
    public async Task<Either<TLeft, TResult>> BindAsync<TResult>(
        Func<TRight, Task<Either<TLeft, TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsRight
            ? await binder(_right).ConfigureAwait(false)
            : Either<TLeft, TResult>.Left(_left);
    }

    /// <summary>
    ///     Gets the Right value or a default if this is Left.
    /// </summary>
    public TRight GetRightOrDefault(TRight defaultValue = default!) =>
        IsRight ? _right : defaultValue;

    /// <summary>
    ///     Gets the Left value or a default if this is Right.
    /// </summary>
    public TLeft GetLeftOrDefault(TLeft defaultValue = default!) =>
        !IsRight ? _left : defaultValue;

    /// <summary>
    ///     Gets the Right value or computes a default lazily.
    /// </summary>
    public TRight GetRightOrElse(Func<TLeft, TRight> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return IsRight ? _right : defaultFactory(_left);
    }

    /// <summary>
    ///     Executes an action on Right.
    /// </summary>
    public Either<TLeft, TRight> DoRight(Action<TRight> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsRight)
        {
            action(_right);
        }

        return this;
    }

    /// <summary>
    ///     Executes an action on Left.
    /// </summary>
    public Either<TLeft, TRight> DoLeft(Action<TLeft> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!IsRight)
        {
            action(_left);
        }

        return this;
    }

    /// <summary>
    ///     Swaps Left and Right.
    /// </summary>
    public Either<TRight, TLeft> Swap() =>
        IsRight ? Either<TRight, TLeft>.Left(_right) : Either<TRight, TLeft>.Right(_left);

    /// <summary>
    ///     Converts to an Option of the Right value.
    /// </summary>
    public Option<TRight> ToOption() =>
        IsRight ? Option.Some(_right) : Option.NoneOf<TRight>();

    /// <summary>
    ///     Converts to an Option of the Left value.
    /// </summary>
    public Option<TLeft> ToLeftOption() =>
        !IsRight ? Option.Some(_left) : Option.NoneOf<TLeft>();

    /// <summary>
    ///     Converts to a Result (Left becomes failure, Right becomes success).
    /// </summary>
    public Result<TRight, TLeft> ToResult() =>
        IsRight ? Result<TRight, TLeft>.Success(_right) : Result<TRight, TLeft>.Failure(_left);

    /// <summary>
    ///     Merges Left and Right when they're the same type.
    ///     Only valid when TLeft and TRight are the same type.
    /// </summary>
    public TRight Merge()
    {
        if (!typeof(TLeft).IsAssignableTo(typeof(TRight)))
        {
            throw new InvalidOperationException(
                $"Cannot merge Either<{typeof(TLeft).Name}, {typeof(TRight).Name}> - types are not compatible.");
        }

        return IsRight ? _right : (TRight)(object)_left!;
    }

    /// <inheritdoc />
    public bool Equals(Either<TLeft, TRight> other)
    {
        if (IsRight != other.IsRight)
        {
            return false;
        }

        return IsRight
            ? EqualityComparer<TRight>.Default.Equals(_right, other._right)
            : EqualityComparer<TLeft>.Default.Equals(_left, other._left);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Either<TLeft, TRight> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => IsRight
        ? HashCode.Combine(true, _right)
        : HashCode.Combine(false, _left);

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(Either<TLeft, TRight> left, Either<TLeft, TRight> right) =>
        left.Equals(right);

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(Either<TLeft, TRight> left, Either<TLeft, TRight> right) =>
        !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => IsRight
        ? $"Right({_right})"
        : $"Left({_left})";

    /// <summary>
    ///     Implicit conversion from Right value.
    /// </summary>
    public static implicit operator Either<TLeft, TRight>(Either.RightValue<TRight> right) =>
        Right(right.Value);

    /// <summary>
    ///     Implicit conversion from Left value.
    /// </summary>
    public static implicit operator Either<TLeft, TRight>(Either.LeftValue<TLeft> left) =>
        Left(left.Value);
}

/// <summary>
///     Static helper methods for creating Either values.
/// </summary>
public static class Either
{
    /// <summary>
    ///     Creates a Left value that can be implicitly converted to any Either.
    /// </summary>
    public static LeftValue<TLeft> Left<TLeft>(TLeft value) => new(value);

    /// <summary>
    ///     Creates a Right value that can be implicitly converted to any Either.
    /// </summary>
    public static RightValue<TRight> Right<TRight>(TRight value) => new(value);

    /// <summary>
    ///     Creates a Left value with explicit types.
    /// </summary>
    public static Either<TLeft, TRight> Left<TLeft, TRight>(TLeft value) =>
        Either<TLeft, TRight>.Left(value);

    /// <summary>
    ///     Creates a Right value with explicit types.
    /// </summary>
    public static Either<TLeft, TRight> Right<TLeft, TRight>(TRight value) =>
        Either<TLeft, TRight>.Right(value);

    /// <summary>
    ///     Converts an enumerable of Eithers to an Either of enumerable.
    ///     Returns Left with the first Left value if any is Left.
    /// </summary>
    public static Either<TLeft, IReadOnlyList<TRight>> Sequence<TLeft, TRight>(
        this IEnumerable<Either<TLeft, TRight>> eitherItems)
    {
        var list = new List<TRight>();
        foreach (var either in eitherItems)
        {
            if (either.IsLeft)
            {
                return Left<TLeft, IReadOnlyList<TRight>>(either.LeftValue);
            }

            list.Add(either.RightValue);
        }

        return Right<TLeft, IReadOnlyList<TRight>>(list);
    }

    /// <summary>
    ///     Traverses an enumerable, applying a function that returns an Either,
    ///     and collects all Right results. Returns Left if any result is Left.
    /// </summary>
    public static Either<TLeft, IReadOnlyList<TResult>> Traverse<T, TLeft, TResult>(
        this IEnumerable<T> source,
        Func<T, Either<TLeft, TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var list = new List<TResult>();
        foreach (var item in source)
        {
            var result = mapper(item);
            if (result.IsLeft)
            {
                return Left<TLeft, IReadOnlyList<TResult>>(result.LeftValue);
            }

            list.Add(result.RightValue);
        }

        return Right<TLeft, IReadOnlyList<TResult>>(list);
    }

    /// <summary>
    ///     Partitions an enumerable of Eithers into Lefts and Rights.
    /// </summary>
    public static (IReadOnlyList<TLeft> Lefts, IReadOnlyList<TRight> Rights) Partition<TLeft, TRight>(
        this IEnumerable<Either<TLeft, TRight>> eitherItems)
    {
        var lefts = new List<TLeft>();
        var rights = new List<TRight>();

        foreach (var either in eitherItems)
        {
            if (either.IsLeft)
            {
                lefts.Add(either.LeftValue);
            }
            else
            {
                rights.Add(either.RightValue);
            }
        }

        return (lefts, rights);
    }

    /// <summary>
    ///     Wrapper for Left values to enable implicit conversion.
    /// </summary>
    public readonly struct LeftValue<T>
    {
        internal T Value { get; }
        internal LeftValue(T value) => Value = value;
    }

    /// <summary>
    ///     Wrapper for Right values to enable implicit conversion.
    /// </summary>
    public readonly struct RightValue<T>
    {
        internal T Value { get; }
        internal RightValue(T value) => Value = value;
    }
}
