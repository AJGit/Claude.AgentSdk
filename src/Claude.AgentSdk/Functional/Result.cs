using System.Diagnostics.CodeAnalysis;

// Suppress CA1000: Static members on generic types are the standard pattern for functional types
#pragma warning disable CA1000

namespace Claude.AgentSdk.Functional;

/// <summary>
///     Represents the result of an operation that can succeed with a value or fail with an error.
///     A type-safe alternative to exceptions for expected failure cases.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
/// <typeparam name="TError">The error type.</typeparam>
/// <remarks>
///     <para>
///     Result&lt;T, TError&gt; makes error handling explicit in the type system,
///     forcing callers to handle both success and failure cases.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     Result&lt;User, ValidationError&gt; CreateUser(string email) =>
///         IsValidEmail(email)
///             ? Result.Success&lt;User, ValidationError&gt;(new User(email))
///             : Result.Failure&lt;User, ValidationError&gt;(new ValidationError("Invalid email"));
///
///     // Pattern matching
///     var message = CreateUser(email).Match(
///         success: user => $"Welcome, {user.Email}!",
///         failure: error => $"Error: {error.Message}"
///     );
///     </code>
///     </para>
/// </remarks>
public readonly struct Result<T, TError> : IEquatable<Result<T, TError>>
{
    private readonly T _value;
    private readonly TError _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _error = default!;
        _isSuccess = true;
    }

    private Result(TError error, bool _)
    {
        _value = default!;
        _error = error;
        _isSuccess = false;
    }

    /// <summary>
    ///     Gets whether this result represents success.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    ///     Gets whether this result represents failure.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    public static Result<T, TError> Success(T value) => new(value);

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    public static Result<T, TError> Failure(TError error) => new(error, false);

    /// <summary>
    ///     Gets the success value, or throws if this is a failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this is a failure.</exception>
    public T Value => _isSuccess
        ? _value
        : throw new InvalidOperationException($"Result is a failure: {_error}");

    /// <summary>
    ///     Gets the error, or throws if this is a success.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this is a success.</exception>
    public TError Error => !_isSuccess
        ? _error
        : throw new InvalidOperationException("Result is a success, no error available.");

    /// <summary>
    ///     Tries to get the success value.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _isSuccess;
    }

    /// <summary>
    ///     Tries to get the error.
    /// </summary>
    public bool TryGetError([MaybeNullWhen(false)] out TError error)
    {
        error = _error;
        return !_isSuccess;
    }

    /// <summary>
    ///     Pattern matches on the result.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> success, Func<TError, TResult> failure)
    {
        ArgumentNullException.ThrowIfNull(success);
        ArgumentNullException.ThrowIfNull(failure);
        return _isSuccess ? success(_value) : failure(_error);
    }

    /// <summary>
    ///     Pattern matches on the result with side effects.
    /// </summary>
    public void Match(Action<T> success, Action<TError> failure)
    {
        ArgumentNullException.ThrowIfNull(success);
        ArgumentNullException.ThrowIfNull(failure);

        if (_isSuccess)
            success(_value);
        else
            failure(_error);
    }

    /// <summary>
    ///     Transforms the success value if present.
    /// </summary>
    public Result<TResult, TError> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess
            ? Result<TResult, TError>.Success(mapper(_value))
            : Result<TResult, TError>.Failure(_error);
    }

    /// <summary>
    ///     Transforms the success value if present (async).
    /// </summary>
    public async Task<Result<TResult, TError>> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess
            ? Result<TResult, TError>.Success(await mapper(_value).ConfigureAwait(false))
            : Result<TResult, TError>.Failure(_error);
    }

    /// <summary>
    ///     Transforms the error if present.
    /// </summary>
    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess
            ? Result<T, TNewError>.Success(_value)
            : Result<T, TNewError>.Failure(mapper(_error));
    }

    /// <summary>
    ///     Chains result-returning operations (flatMap/selectMany).
    /// </summary>
    public Result<TResult, TError> Bind<TResult>(Func<T, Result<TResult, TError>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder(_value) : Result<TResult, TError>.Failure(_error);
    }

    /// <summary>
    ///     Chains result-returning operations (async).
    /// </summary>
    public async Task<Result<TResult, TError>> BindAsync<TResult>(
        Func<T, Task<Result<TResult, TError>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess
            ? await binder(_value).ConfigureAwait(false)
            : Result<TResult, TError>.Failure(_error);
    }

    /// <summary>
    ///     Gets the value or a default if this is a failure.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => _isSuccess ? _value : defaultValue;

    /// <summary>
    ///     Gets the value or computes a default lazily if this is a failure.
    /// </summary>
    public T GetValueOrElse(Func<TError, T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return _isSuccess ? _value : defaultFactory(_error);
    }

    /// <summary>
    ///     Executes an action on success.
    /// </summary>
    public Result<T, TError> Do(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_isSuccess)
            action(_value);
        return this;
    }

    /// <summary>
    ///     Executes an action on failure.
    /// </summary>
    public Result<T, TError> DoOnError(Action<TError> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_isSuccess)
            action(_error);
        return this;
    }

    /// <summary>
    ///     Returns this result if success, otherwise returns the alternative.
    /// </summary>
    public Result<T, TError> Or(Result<T, TError> alternative) => _isSuccess ? this : alternative;

    /// <summary>
    ///     Returns this result if success, otherwise computes an alternative lazily.
    /// </summary>
    public Result<T, TError> OrElse(Func<TError, Result<T, TError>> alternativeFactory)
    {
        ArgumentNullException.ThrowIfNull(alternativeFactory);
        return _isSuccess ? this : alternativeFactory(_error);
    }

    /// <summary>
    ///     Converts to an Option, discarding the error.
    /// </summary>
    public Option<T> ToOption() => _isSuccess ? Option.Some(_value) : Option.NoneOf<T>();

    /// <summary>
    ///     Converts to an Option of the error.
    /// </summary>
    public Option<TError> ToErrorOption() => !_isSuccess ? Option.Some(_error) : Option.NoneOf<TError>();

    /// <summary>
    ///     Throws an exception if this is a failure.
    /// </summary>
    /// <param name="exceptionFactory">Factory to create the exception from the error.</param>
    /// <returns>The success value.</returns>
    public T GetValueOrThrow(Func<TError, Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);
        if (_isSuccess)
            return _value;
        throw exceptionFactory(_error);
    }

    /// <summary>
    ///     Ensures the value satisfies a condition.
    /// </summary>
    public Result<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (!_isSuccess)
            return this;
        return predicate(_value) ? this : Failure(error);
    }

    /// <summary>
    ///     Ensures the value satisfies a condition (lazy error).
    /// </summary>
    public Result<T, TError> Ensure(Func<T, bool> predicate, Func<T, TError> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (!_isSuccess)
            return this;
        return predicate(_value) ? this : Failure(errorFactory(_value));
    }

    #region Equality

    /// <inheritdoc />
    public bool Equals(Result<T, TError> other)
    {
        if (_isSuccess != other._isSuccess)
            return false;

        return _isSuccess
            ? EqualityComparer<T>.Default.Equals(_value, other._value)
            : EqualityComparer<TError>.Default.Equals(_error, other._error);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T, TError> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _isSuccess
        ? HashCode.Combine(true, _value)
        : HashCode.Combine(false, _error);

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(Result<T, TError> left, Result<T, TError> right) =>
        left.Equals(right);

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(Result<T, TError> left, Result<T, TError> right) =>
        !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() => _isSuccess
        ? $"Success({_value})"
        : $"Failure({_error})";
}

/// <summary>
///     Result type with string error messages (convenience type).
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly Result<T, string> _inner;

    private Result(Result<T, string> inner) => _inner = inner;

    /// <summary>
    ///     Gets whether this result represents success.
    /// </summary>
    public bool IsSuccess => _inner.IsSuccess;

    /// <summary>
    ///     Gets whether this result represents failure.
    /// </summary>
    public bool IsFailure => _inner.IsFailure;

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    public static Result<T> Success(T value) => new(Result<T, string>.Success(value));

    /// <summary>
    ///     Creates a failed result.
    /// </summary>
    public static Result<T> Failure(string error) => new(Result<T, string>.Failure(error));

    /// <summary>
    ///     Gets the success value, or throws if this is a failure.
    /// </summary>
    public T Value => _inner.Value;

    /// <summary>
    ///     Gets the error message, or throws if this is a success.
    /// </summary>
    public string Error => _inner.Error;

    /// <summary>
    ///     Tries to get the success value.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value) => _inner.TryGetValue(out value);

    /// <summary>
    ///     Tries to get the error.
    /// </summary>
    public bool TryGetError([MaybeNullWhen(false)] out string error) => _inner.TryGetError(out error);

    /// <summary>
    ///     Pattern matches on the result.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> success, Func<string, TResult> failure) =>
        _inner.Match(success, failure);

    /// <summary>
    ///     Pattern matches on the result with side effects.
    /// </summary>
    public void Match(Action<T> success, Action<string> failure) =>
        _inner.Match(success, failure);

    /// <summary>
    ///     Transforms the success value if present.
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new(_inner.Map(mapper));

    /// <summary>
    ///     Transforms the success value if present (async).
    /// </summary>
    public async Task<Result<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> mapper) =>
        new(await _inner.MapAsync(mapper).ConfigureAwait(false));

    /// <summary>
    ///     Transforms the error if present.
    /// </summary>
    public Result<T> MapError(Func<string, string> mapper) =>
        new(_inner.MapError(mapper));

    /// <summary>
    ///     Chains result-returning operations.
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _inner.IsSuccess
            ? binder(_inner.Value)
            : Result<TResult>.Failure(_inner.Error);
    }

    /// <summary>
    ///     Chains result-returning operations (async).
    /// </summary>
    public async Task<Result<TResult>> BindAsync<TResult>(Func<T, Task<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _inner.IsSuccess
            ? await binder(_inner.Value).ConfigureAwait(false)
            : Result<TResult>.Failure(_inner.Error);
    }

    /// <summary>
    ///     Gets the value or a default if this is a failure.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => _inner.GetValueOrDefault(defaultValue);

    /// <summary>
    ///     Gets the value or computes a default lazily if this is a failure.
    /// </summary>
    public T GetValueOrElse(Func<string, T> defaultFactory) => _inner.GetValueOrElse(defaultFactory);

    /// <summary>
    ///     Executes an action on success.
    /// </summary>
    public Result<T> Do(Action<T> action)
    {
        _inner.Do(action);
        return this;
    }

    /// <summary>
    ///     Executes an action on failure.
    /// </summary>
    public Result<T> DoOnError(Action<string> action)
    {
        _inner.DoOnError(action);
        return this;
    }

    /// <summary>
    ///     Converts to an Option, discarding the error.
    /// </summary>
    public Option<T> ToOption() => _inner.ToOption();

    /// <summary>
    ///     Converts to the generic Result type.
    /// </summary>
    public Result<T, string> ToGeneric() => _inner;

    /// <summary>
    ///     Throws an exception if this is a failure.
    /// </summary>
    public T GetValueOrThrow() =>
        _inner.IsSuccess ? _inner.Value : throw new InvalidOperationException(_inner.Error);

    /// <summary>
    ///     Throws a custom exception if this is a failure.
    /// </summary>
    public T GetValueOrThrow(Func<string, Exception> exceptionFactory) =>
        _inner.GetValueOrThrow(exceptionFactory);

    /// <summary>
    ///     Ensures the value satisfies a condition.
    /// </summary>
    public Result<T> Ensure(Func<T, bool> predicate, string error) =>
        new(_inner.Ensure(predicate, error));

    #region Equality

    /// <inheritdoc />
    public bool Equals(Result<T> other) => _inner.Equals(other._inner);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _inner.GetHashCode();

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();

    /// <summary>
    ///     Implicit conversion from Result&lt;T, string&gt;.
    /// </summary>
    public static implicit operator Result<T>(Result<T, string> result) => new(result);
}

/// <summary>
///     Static helper methods for creating Result values.
/// </summary>
public static class Result
{
    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>
    ///     Creates a failed result with a string error.
    /// </summary>
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);

    /// <summary>
    ///     Creates a successful result with a custom error type.
    /// </summary>
    public static Result<T, TError> Success<T, TError>(T value) => Result<T, TError>.Success(value);

    /// <summary>
    ///     Creates a failed result with a custom error type.
    /// </summary>
    public static Result<T, TError> Failure<T, TError>(TError error) => Result<T, TError>.Failure(error);

    /// <summary>
    ///     Creates a Result from a nullable value.
    /// </summary>
    public static Result<T> FromNullable<T>(T? value, string errorIfNull) where T : class =>
        value is not null ? Success(value) : Failure<T>(errorIfNull);

    /// <summary>
    ///     Creates a Result from a nullable struct.
    /// </summary>
    public static Result<T> FromNullable<T>(T? value, string errorIfNull) where T : struct =>
        value.HasValue ? Success(value.Value) : Failure<T>(errorIfNull);

    /// <summary>
    ///     Executes a function and catches exceptions as failures.
    /// </summary>
    public static Result<T> Try<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Success(func());
        }
        catch (Exception ex)
        {
            return Failure<T>(ex.Message);
        }
    }

    /// <summary>
    ///     Executes a function and catches exceptions as failures (with custom error type).
    /// </summary>
    public static Result<T, TError> Try<T, TError>(Func<T> func, Func<Exception, TError> errorMapper)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(errorMapper);
        try
        {
            return Success<T, TError>(func());
        }
        catch (Exception ex)
        {
            return Failure<T, TError>(errorMapper(ex));
        }
    }

    /// <summary>
    ///     Executes an async function and catches exceptions as failures.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Success(await func().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            return Failure<T>(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an async function and catches exceptions as failures (with custom error type).
    /// </summary>
    public static async Task<Result<T, TError>> TryAsync<T, TError>(
        Func<Task<T>> func,
        Func<Exception, TError> errorMapper)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(errorMapper);
        try
        {
            return Success<T, TError>(await func().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            return Failure<T, TError>(errorMapper(ex));
        }
    }

    /// <summary>
    ///     Combines multiple results. Returns failure if any result is a failure.
    /// </summary>
    public static Result<(T1, T2)> Combine<T1, T2>(Result<T1> r1, Result<T2> r2)
    {
        if (r1.IsFailure) return Failure<(T1, T2)>(r1.Error);
        if (r2.IsFailure) return Failure<(T1, T2)>(r2.Error);
        return Success((r1.Value, r2.Value));
    }

    /// <summary>
    ///     Combines multiple results. Returns failure if any result is a failure.
    /// </summary>
    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(Result<T1> r1, Result<T2> r2, Result<T3> r3)
    {
        if (r1.IsFailure) return Failure<(T1, T2, T3)>(r1.Error);
        if (r2.IsFailure) return Failure<(T1, T2, T3)>(r2.Error);
        if (r3.IsFailure) return Failure<(T1, T2, T3)>(r3.Error);
        return Success((r1.Value, r2.Value, r3.Value));
    }

    /// <summary>
    ///     Converts an enumerable of Results to a Result of enumerable.
    ///     Returns failure with the first error if any result is a failure.
    /// </summary>
    public static Result<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        var list = new List<T>();
        foreach (var result in results)
        {
            if (result.IsFailure)
                return Failure<IReadOnlyList<T>>(result.Error);
            list.Add(result.Value);
        }
        return Success<IReadOnlyList<T>>(list);
    }

    /// <summary>
    ///     Converts an enumerable of Results to a Result of enumerable.
    ///     Returns failure with all errors if any result is a failure.
    /// </summary>
    public static Result<IReadOnlyList<T>, IReadOnlyList<TError>> SequenceAll<T, TError>(
        this IEnumerable<Result<T, TError>> results)
    {
        var successes = new List<T>();
        var errors = new List<TError>();

        foreach (var result in results)
        {
            if (result.IsSuccess)
                successes.Add(result.Value);
            else
                errors.Add(result.Error);
        }

        return errors.Count > 0
            ? Failure<IReadOnlyList<T>, IReadOnlyList<TError>>(errors)
            : Success<IReadOnlyList<T>, IReadOnlyList<TError>>(successes);
    }

    /// <summary>
    ///     Traverses an enumerable, applying a function that returns a Result,
    ///     and collects all Success results. Returns Failure if any result fails.
    /// </summary>
    public static Result<IReadOnlyList<TResult>> Traverse<T, TResult>(
        this IEnumerable<T> source,
        Func<T, Result<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var list = new List<TResult>();
        foreach (var item in source)
        {
            var result = mapper(item);
            if (result.IsFailure)
                return Failure<IReadOnlyList<TResult>>(result.Error);
            list.Add(result.Value);
        }
        return Success<IReadOnlyList<TResult>>(list);
    }
}
