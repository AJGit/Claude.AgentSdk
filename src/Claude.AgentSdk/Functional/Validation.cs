using System.Diagnostics.CodeAnalysis;

// Suppress CA1000: Static members on generic types are the standard pattern for functional types
#pragma warning disable CA1000

namespace Claude.AgentSdk.Functional;

/// <summary>
///     Represents a validation result that can accumulate multiple errors.
///     Unlike Result which short-circuits on the first error, Validation collects all errors.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
/// <typeparam name="TError">The error type.</typeparam>
/// <remarks>
///     <para>
///         Validation is ideal for form validation, configuration validation, or any scenario
///         where you want to report all errors at once rather than one at a time.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     var nameValidation = ValidateName(name);     // Validation&lt;string, string&gt;
///     var emailValidation = ValidateEmail(email);  // Validation&lt;string, string&gt;
///     var ageValidation = ValidateAge(age);        // Validation&lt;int, string&gt;
/// 
///     var result = Validation.Map3(
///         nameValidation, emailValidation, ageValidation,
///         (n, e, a) => new User(n, e, a)
///     );
///     // If any validation fails, result contains ALL error messages
///     </code>
///     </para>
/// </remarks>
public readonly struct Validation<T, TError> : IEquatable<Validation<T, TError>>
{
    private readonly T _value;
    private readonly IReadOnlyList<TError> _errors;

    private Validation(T value)
    {
        _value = value;
        _errors = [];
        IsValid = true;
    }

    private Validation(IReadOnlyList<TError> errors)
    {
        _value = default!;
        _errors = errors;
        IsValid = false;
    }

    /// <summary>
    ///     Gets whether this validation succeeded.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    ///     Gets whether this validation failed.
    /// </summary>
    public bool IsInvalid => !IsValid;

    /// <summary>
    ///     Creates a successful validation.
    /// </summary>
    public static Validation<T, TError> Valid(T value) => new(value);

    /// <summary>
    ///     Creates a failed validation with a single error.
    /// </summary>
    public static Validation<T, TError> Invalid(TError error) => new([error]);

    /// <summary>
    ///     Creates a failed validation with multiple errors.
    /// </summary>
    public static Validation<T, TError> Invalid(IEnumerable<TError> errors) => new(errors.ToList());

    /// <summary>
    ///     Gets the valid value, or throws if invalid.
    /// </summary>
    public T Value => IsValid
        ? _value
        : throw new InvalidOperationException($"Validation failed with {_errors.Count} error(s).");

    /// <summary>
    ///     Gets the errors, or throws if valid.
    /// </summary>
    public IReadOnlyList<TError> Errors => !IsValid
        ? _errors
        : throw new InvalidOperationException("Validation succeeded, no errors available.");

    /// <summary>
    ///     Tries to get the valid value.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return IsValid;
    }

    /// <summary>
    ///     Tries to get the errors.
    /// </summary>
    public bool TryGetErrors([MaybeNullWhen(false)] out IReadOnlyList<TError> errors)
    {
        errors = _errors;
        return !IsValid;
    }

    /// <summary>
    ///     Pattern matches on the validation.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> valid, Func<IReadOnlyList<TError>, TResult> invalid)
    {
        ArgumentNullException.ThrowIfNull(valid);
        ArgumentNullException.ThrowIfNull(invalid);
        return IsValid ? valid(_value) : invalid(_errors);
    }

    /// <summary>
    ///     Pattern matches on the validation with side effects.
    /// </summary>
    public void Match(Action<T> valid, Action<IReadOnlyList<TError>> invalid)
    {
        ArgumentNullException.ThrowIfNull(valid);
        ArgumentNullException.ThrowIfNull(invalid);

        if (IsValid)
        {
            valid(_value);
        }
        else
        {
            invalid(_errors);
        }
    }

    /// <summary>
    ///     Transforms the value if valid.
    /// </summary>
    public Validation<TResult, TError> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsValid
            ? Validation<TResult, TError>.Valid(mapper(_value))
            : Validation<TResult, TError>.Invalid(_errors);
    }

    /// <summary>
    ///     Transforms the errors if invalid.
    /// </summary>
    public Validation<T, TNewError> MapErrors<TNewError>(Func<TError, TNewError> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsValid
            ? Validation<T, TNewError>.Valid(_value)
            : Validation<T, TNewError>.Invalid(_errors.Select(mapper).ToList());
    }

    /// <summary>
    ///     Chains validation-returning operations.
    ///     Note: Unlike applicative, this does NOT accumulate errors - it short-circuits.
    /// </summary>
    public Validation<TResult, TError> Bind<TResult>(Func<T, Validation<TResult, TError>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsValid ? binder(_value) : Validation<TResult, TError>.Invalid(_errors);
    }

    /// <summary>
    ///     Gets the value or a default if invalid.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => IsValid ? _value : defaultValue;

    /// <summary>
    ///     Gets the value or computes a default if invalid.
    /// </summary>
    public T GetValueOrElse(Func<IReadOnlyList<TError>, T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return IsValid ? _value : defaultFactory(_errors);
    }

    /// <summary>
    ///     Converts to a Result (first error only).
    /// </summary>
    public Result<T, TError> ToResult() =>
        IsValid ? Result<T, TError>.Success(_value) : Result<T, TError>.Failure(_errors[0]);

    /// <summary>
    ///     Converts to an Option (discarding errors).
    /// </summary>
    public Option<T> ToOption() =>
        IsValid ? Option.Some(_value) : Option.NoneOf<T>();

    /// <summary>
    ///     Adds a condition that must be true for the value.
    /// </summary>
    public Validation<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (!IsValid)
        {
            return this;
        }

        return predicate(_value) ? this : Invalid(error);
    }

    /// <summary>
    ///     Adds multiple conditions using a validation function.
    /// </summary>
    public Validation<T, TError> And(Func<T, Validation<T, TError>> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        if (!IsValid)
        {
            return this;
        }

        var other = validator(_value);
        if (other.IsValid)
        {
            return this;
        }

        // Combine errors
        return Invalid(_errors.Concat(other._errors).ToList());
    }

    /// <inheritdoc />
    public bool Equals(Validation<T, TError> other)
    {
        if (IsValid != other.IsValid)
        {
            return false;
        }

        if (IsValid)
        {
            return EqualityComparer<T>.Default.Equals(_value, other._value);
        }

        return _errors.SequenceEqual(other._errors);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Validation<T, TError> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => IsValid
        ? HashCode.Combine(true, _value)
        : HashCode.Combine(false, _errors.Count);

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(Validation<T, TError> left, Validation<T, TError> right) =>
        left.Equals(right);

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(Validation<T, TError> left, Validation<T, TError> right) =>
        !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => IsValid
        ? $"Valid({_value})"
        : $"Invalid([{string.Join(", ", _errors)}])";
}

/// <summary>
///     Validation type with string errors (convenience type).
/// </summary>
public readonly struct Validation<T> : IEquatable<Validation<T>>
{
    private readonly Validation<T, string> _inner;

    private Validation(Validation<T, string> inner) => _inner = inner;

    /// <summary>
    ///     Gets whether this validation succeeded.
    /// </summary>
    public bool IsValid => _inner.IsValid;

    /// <summary>
    ///     Gets whether this validation failed.
    /// </summary>
    public bool IsInvalid => _inner.IsInvalid;

    /// <summary>
    ///     Creates a successful validation.
    /// </summary>
    public static Validation<T> Valid(T value) => new(Validation<T, string>.Valid(value));

    /// <summary>
    ///     Creates a failed validation.
    /// </summary>
    public static Validation<T> Invalid(string error) => new(Validation<T, string>.Invalid(error));

    /// <summary>
    ///     Creates a failed validation with multiple errors.
    /// </summary>
    public static Validation<T> Invalid(IEnumerable<string> errors) =>
        new(Validation<T, string>.Invalid(errors));

    /// <summary>
    ///     Gets the valid value.
    /// </summary>
    public T Value => _inner.Value;

    /// <summary>
    ///     Gets the errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _inner.Errors;

    /// <summary>
    ///     Tries to get the valid value.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value) => _inner.TryGetValue(out value);

    /// <summary>
    ///     Pattern matches on the validation.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> valid, Func<IReadOnlyList<string>, TResult> invalid) =>
        _inner.Match(valid, invalid);

    /// <summary>
    ///     Pattern matches on the validation with side effects.
    /// </summary>
    public void Match(Action<T> valid, Action<IReadOnlyList<string>> invalid) =>
        _inner.Match(valid, invalid);

    /// <summary>
    ///     Transforms the value if valid.
    /// </summary>
    public Validation<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new(_inner.Map(mapper));

    /// <summary>
    ///     Chains validation-returning operations.
    /// </summary>
    public Validation<TResult> Bind<TResult>(Func<T, Validation<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _inner.IsValid
            ? binder(_inner.Value)
            : new Validation<TResult>(Validation<TResult, string>.Invalid(_inner.Errors));
    }

    /// <summary>
    ///     Gets the value or a default if invalid.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => _inner.GetValueOrDefault(defaultValue);

    /// <summary>
    ///     Adds a condition that must be true for the value.
    /// </summary>
    public Validation<T> Ensure(Func<T, bool> predicate, string error) =>
        new(_inner.Ensure(predicate, error));

    /// <summary>
    ///     Converts to the generic Validation type.
    /// </summary>
    public Validation<T, string> ToGeneric() => _inner;

    /// <inheritdoc />
    public bool Equals(Validation<T> other) => _inner.Equals(other._inner);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Validation<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _inner.GetHashCode();

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(Validation<T> left, Validation<T> right) => left.Equals(right);

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(Validation<T> left, Validation<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();

    /// <summary>
    ///     Implicit conversion from generic Validation.
    /// </summary>
    public static implicit operator Validation<T>(Validation<T, string> validation) => new(validation);
}

/// <summary>
///     Static helper methods for Validation.
/// </summary>
public static class Validation
{
    /// <summary>
    ///     Creates a valid validation.
    /// </summary>
    public static Validation<T> Valid<T>(T value) => Validation<T>.Valid(value);

    /// <summary>
    ///     Creates an invalid validation.
    /// </summary>
    public static Validation<T> Invalid<T>(string error) => Validation<T>.Invalid(error);

    /// <summary>
    ///     Creates a valid validation with typed error.
    /// </summary>
    public static Validation<T, TError> Valid<T, TError>(T value) => Validation<T, TError>.Valid(value);

    /// <summary>
    ///     Creates an invalid validation with typed error.
    /// </summary>
    public static Validation<T, TError> Invalid<T, TError>(TError error) =>
        Validation<T, TError>.Invalid(error);

    /// <summary>
    ///     Creates a validation from a predicate.
    /// </summary>
    public static Validation<T> From<T>(T value, Func<T, bool> predicate, string errorIfFalse)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return predicate(value) ? Valid(value) : Invalid<T>(errorIfFalse);
    }

    /// <summary>
    ///     Creates a validation from a Result.
    /// </summary>
    public static Validation<T> FromResult<T>(Result<T> result) =>
        result.IsSuccess ? Valid(result.Value) : Invalid<T>(result.Error);

    /// <summary>
    ///     Combines two validations, accumulating errors.
    /// </summary>
    public static Validation<(T1, T2), TError> Combine<T1, T2, TError>(
        Validation<T1, TError> v1,
        Validation<T2, TError> v2)
    {
        var errors = new List<TError>();

        if (v1.IsInvalid)
        {
            errors.AddRange(v1.Errors);
        }

        if (v2.IsInvalid)
        {
            errors.AddRange(v2.Errors);
        }

        return errors.Count > 0
            ? Validation<(T1, T2), TError>.Invalid(errors)
            : Validation<(T1, T2), TError>.Valid((v1.Value, v2.Value));
    }

    /// <summary>
    ///     Combines three validations, accumulating errors.
    /// </summary>
    public static Validation<(T1, T2, T3), TError> Combine<T1, T2, T3, TError>(
        Validation<T1, TError> v1,
        Validation<T2, TError> v2,
        Validation<T3, TError> v3)
    {
        var errors = new List<TError>();

        if (v1.IsInvalid)
        {
            errors.AddRange(v1.Errors);
        }

        if (v2.IsInvalid)
        {
            errors.AddRange(v2.Errors);
        }

        if (v3.IsInvalid)
        {
            errors.AddRange(v3.Errors);
        }

        return errors.Count > 0
            ? Validation<(T1, T2, T3), TError>.Invalid(errors)
            : Validation<(T1, T2, T3), TError>.Valid((v1.Value, v2.Value, v3.Value));
    }

    /// <summary>
    ///     Applies a function to validated values (applicative functor).
    /// </summary>
    public static Validation<TResult, TError> Map2<T1, T2, TResult, TError>(
        Validation<T1, TError> v1,
        Validation<T2, TError> v2,
        Func<T1, T2, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return Combine(v1, v2).Map(t => mapper(t.Item1, t.Item2));
    }

    /// <summary>
    ///     Applies a function to validated values (applicative functor).
    /// </summary>
    public static Validation<TResult, TError> Map3<T1, T2, T3, TResult, TError>(
        Validation<T1, TError> v1,
        Validation<T2, TError> v2,
        Validation<T3, TError> v3,
        Func<T1, T2, T3, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return Combine(v1, v2, v3).Map(t => mapper(t.Item1, t.Item2, t.Item3));
    }

    /// <summary>
    ///     Sequences a collection of validations, accumulating all errors.
    /// </summary>
    public static Validation<IReadOnlyList<T>, TError> Sequence<T, TError>(
        this IEnumerable<Validation<T, TError>> validations)
    {
        var values = new List<T>();
        var errors = new List<TError>();

        foreach (var v in validations)
        {
            if (v.IsValid)
            {
                values.Add(v.Value);
            }
            else
            {
                errors.AddRange(v.Errors);
            }
        }

        return errors.Count > 0
            ? Validation<IReadOnlyList<T>, TError>.Invalid(errors)
            : Validation<IReadOnlyList<T>, TError>.Valid(values);
    }

    /// <summary>
    ///     Traverses a collection, applying a validation function and accumulating errors.
    /// </summary>
    public static Validation<IReadOnlyList<TResult>, TError> Traverse<T, TResult, TError>(
        this IEnumerable<T> source,
        Func<T, Validation<TResult, TError>> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        return source.Select(validator).Sequence();
    }
}
