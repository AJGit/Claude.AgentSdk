using System.Diagnostics.CodeAnalysis;

// Suppress CA1000: Static members on generic types are the standard pattern for functional types
#pragma warning disable CA1000

namespace Claude.AgentSdk.Functional;

/// <summary>
///     Represents an optional value that may or may not be present.
///     A type-safe alternative to null that makes the absence of a value explicit.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <remarks>
///     <para>
///     Option&lt;T&gt; forces explicit handling of the "no value" case,
///     eliminating null reference exceptions and making code intent clearer.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     Option&lt;string&gt; FindUser(int id) =>
///         users.TryGetValue(id, out var user) ? Option.Some(user) : Option.None&lt;string&gt;();
///
///     // Pattern matching
///     var greeting = FindUser(123).Match(
///         some: user => $"Hello, {user}!",
///         none: () => "User not found"
///     );
///
///     // Chaining with Map and Bind
///     var upperName = FindUser(123)
///         .Map(u => u.ToUpper())
///         .GetValueOrDefault("UNKNOWN");
///     </code>
///     </para>
/// </remarks>
public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly T _value;
    private readonly bool _hasValue;

    private Option(T value)
    {
        _value = value;
        _hasValue = true;
    }

    /// <summary>
    ///     Gets whether this option contains a value.
    /// </summary>
    public bool IsSome => _hasValue;

    /// <summary>
    ///     Gets whether this option is empty.
    /// </summary>
    public bool IsNone => !_hasValue;

    /// <summary>
    ///     Creates an Option containing the specified value.
    /// </summary>
    public static Option<T> Some(T value) => new(value);

    /// <summary>
    ///     Creates an empty Option.
    /// </summary>
    public static Option<T> None => default;

    /// <summary>
    ///     Gets the value if present, or throws InvalidOperationException.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the option is empty.</exception>
    public T Value => _hasValue
        ? _value
        : throw new InvalidOperationException("Option has no value. Check IsSome before accessing Value.");

    /// <summary>
    ///     Tries to get the value.
    /// </summary>
    /// <param name="value">The value if present.</param>
    /// <returns>True if a value is present.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _hasValue;
    }

    /// <summary>
    ///     Gets the value or a default.
    /// </summary>
    /// <param name="defaultValue">The default value to return if empty.</param>
    /// <returns>The value or the default.</returns>
    public T GetValueOrDefault(T defaultValue = default!) => _hasValue ? _value : defaultValue;

    /// <summary>
    ///     Gets the value or computes a default lazily.
    /// </summary>
    /// <param name="defaultFactory">Factory function to create the default value.</param>
    /// <returns>The value or the computed default.</returns>
    public T GetValueOrElse(Func<T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        return _hasValue ? _value : defaultFactory();
    }

    /// <summary>
    ///     Pattern matches on the option.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="some">Function to apply if value is present.</param>
    /// <param name="none">Function to apply if value is absent.</param>
    /// <returns>The result of the matching function.</returns>
    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);
        return _hasValue ? some(_value) : none();
    }

    /// <summary>
    ///     Pattern matches on the option with side effects.
    /// </summary>
    /// <param name="some">Action to execute if value is present.</param>
    /// <param name="none">Action to execute if value is absent.</param>
    public void Match(Action<T> some, Action none)
    {
        ArgumentNullException.ThrowIfNull(some);
        ArgumentNullException.ThrowIfNull(none);

        if (_hasValue)
            some(_value);
        else
            none();
    }

    /// <summary>
    ///     Transforms the value if present.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="mapper">The transformation function.</param>
    /// <returns>An option containing the transformed value, or None.</returns>
    public Option<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _hasValue ? Option<TResult>.Some(mapper(_value)) : Option<TResult>.None;
    }

    /// <summary>
    ///     Transforms the value if present using an async function.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="mapper">The async transformation function.</param>
    /// <returns>A task containing an option with the transformed value, or None.</returns>
    public async Task<Option<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _hasValue ? Option<TResult>.Some(await mapper(_value).ConfigureAwait(false)) : Option<TResult>.None;
    }

    /// <summary>
    ///     Chains option-returning operations (flatMap/selectMany).
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="binder">The binding function.</param>
    /// <returns>The result of the binding function, or None.</returns>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _hasValue ? binder(_value) : Option<TResult>.None;
    }

    /// <summary>
    ///     Chains async option-returning operations.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="binder">The async binding function.</param>
    /// <returns>A task containing the result of the binding function, or None.</returns>
    public async Task<Option<TResult>> BindAsync<TResult>(Func<T, Task<Option<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _hasValue ? await binder(_value).ConfigureAwait(false) : Option<TResult>.None;
    }

    /// <summary>
    ///     Filters the option based on a predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>This option if it has a value and the predicate returns true, otherwise None.</returns>
    public Option<T> Where(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _hasValue && predicate(_value) ? this : None;
    }

    /// <summary>
    ///     Executes an action if a value is present.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>This option for chaining.</returns>
    public Option<T> Do(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_hasValue)
            action(_value);
        return this;
    }

    /// <summary>
    ///     Returns this option if it has a value, otherwise returns the alternative.
    /// </summary>
    /// <param name="alternative">The alternative option.</param>
    /// <returns>This option or the alternative.</returns>
    public Option<T> Or(Option<T> alternative) => _hasValue ? this : alternative;

    /// <summary>
    ///     Returns this option if it has a value, otherwise computes an alternative lazily.
    /// </summary>
    /// <param name="alternativeFactory">Factory function for the alternative option.</param>
    /// <returns>This option or the computed alternative.</returns>
    public Option<T> OrElse(Func<Option<T>> alternativeFactory)
    {
        ArgumentNullException.ThrowIfNull(alternativeFactory);
        return _hasValue ? this : alternativeFactory();
    }

    /// <summary>
    ///     Converts the option to a nullable reference (for reference types).
    /// </summary>
    /// <returns>The value or null.</returns>
    public T? ToNullableRef() => _hasValue ? _value : default;

    /// <summary>
    ///     Converts the option to an enumerable (empty or single element).
    /// </summary>
    /// <returns>An enumerable containing zero or one element.</returns>
    public IEnumerable<T> ToEnumerable()
    {
        if (_hasValue)
            yield return _value;
    }

    /// <summary>
    ///     Converts the option to a list (empty or single element).
    /// </summary>
    /// <returns>A list containing zero or one element.</returns>
    public List<T> ToList() => _hasValue ? [_value] : [];

    /// <summary>
    ///     Converts the option to an array (empty or single element).
    /// </summary>
    /// <returns>An array containing zero or one element.</returns>
    public T[] ToArray() => _hasValue ? [_value] : [];

    /// <summary>
    ///     Converts to a Result, using the provided error if None.
    /// </summary>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="error">The error to use if None.</param>
    /// <returns>A Result containing the value or the error.</returns>
    public Result<T, TError> ToResult<TError>(TError error) =>
        _hasValue ? Result<T, TError>.Success(_value) : Result<T, TError>.Failure(error);

    /// <summary>
    ///     Converts to a Result, computing the error lazily if None.
    /// </summary>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="errorFactory">Factory function for the error.</param>
    /// <returns>A Result containing the value or the computed error.</returns>
    public Result<T, TError> ToResult<TError>(Func<TError> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return _hasValue ? Result<T, TError>.Success(_value) : Result<T, TError>.Failure(errorFactory());
    }

    #region Equality

    /// <inheritdoc />
    public bool Equals(Option<T> other)
    {
        if (_hasValue != other._hasValue)
            return false;
        if (!_hasValue)
            return true;
        return EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Option<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _hasValue ? _value?.GetHashCode() ?? 0 : 0;

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() => _hasValue ? $"Some({_value})" : "None";

    /// <summary>
    ///     Implicitly converts a value to an Option containing that value.
    /// </summary>
    public static implicit operator Option<T>(T value) =>
        value is null ? None : Some(value);

    /// <summary>
    ///     Implicitly converts None&lt;T&gt; to Option&lt;T&gt;.
    /// </summary>
    public static implicit operator Option<T>(Option.NoneType _) => None;
}

/// <summary>
///     Static helper methods for creating Option values.
/// </summary>
public static class Option
{
    /// <summary>
    ///     Represents the None value for implicit conversion.
    /// </summary>
    public readonly struct NoneType
    {
        /// <inheritdoc />
        public override string ToString() => "None";
    }

    /// <summary>
    ///     The None value that can be implicitly converted to any Option&lt;T&gt;.
    /// </summary>
    public static NoneType None => default;

    /// <summary>
    ///     Creates an Option containing the specified value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>An Option containing the value.</returns>
    public static Option<T> Some<T>(T value) => Option<T>.Some(value);

    /// <summary>
    ///     Creates an empty Option of the specified type.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>An empty Option.</returns>
    public static Option<T> NoneOf<T>() => Option<T>.None;

    /// <summary>
    ///     Creates an Option from a nullable value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The nullable value.</param>
    /// <returns>Some if value is not null, otherwise None.</returns>
    public static Option<T> FromNullable<T>(T? value) where T : class =>
        value is not null ? Some(value) : NoneOf<T>();

    /// <summary>
    ///     Creates an Option from a nullable struct.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The nullable struct.</param>
    /// <returns>Some if value has a value, otherwise None.</returns>
    public static Option<T> FromNullable<T>(T? value) where T : struct =>
        value.HasValue ? Some(value.Value) : NoneOf<T>();

    /// <summary>
    ///     Tries to get a value from a dictionary, returning an Option.
    /// </summary>
    public static Option<TValue> TryGetValue<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key) =>
        dictionary.TryGetValue(key, out var value) ? Some(value) : NoneOf<TValue>();

    /// <summary>
    ///     Tries to parse a string, returning an Option.
    /// </summary>
    public static Option<int> TryParseInt(string? s) =>
        int.TryParse(s, out var result) ? Some(result) : NoneOf<int>();

    /// <summary>
    ///     Tries to parse a string, returning an Option.
    /// </summary>
    public static Option<double> TryParseDouble(string? s) =>
        double.TryParse(s, out var result) ? Some(result) : NoneOf<double>();

    /// <summary>
    ///     Tries to parse a string, returning an Option.
    /// </summary>
    public static Option<bool> TryParseBool(string? s) =>
        bool.TryParse(s, out var result) ? Some(result) : NoneOf<bool>();

    /// <summary>
    ///     Tries to parse a string, returning an Option.
    /// </summary>
    public static Option<Guid> TryParseGuid(string? s) =>
        Guid.TryParse(s, out var result) ? Some(result) : NoneOf<Guid>();

    /// <summary>
    ///     Tries to parse a string to an enum, returning an Option.
    /// </summary>
    public static Option<TEnum> TryParseEnum<TEnum>(string? s) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(s, ignoreCase: true, out var result) ? Some(result) : NoneOf<TEnum>();

    /// <summary>
    ///     Converts an enumerable of Options to an Option of enumerable,
    ///     returning None if any element is None.
    /// </summary>
    public static Option<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Option<T>> options)
    {
        var list = new List<T>();
        foreach (var option in options)
        {
            if (option.IsNone)
                return NoneOf<IReadOnlyList<T>>();
            list.Add(option.Value);
        }
        return Some<IReadOnlyList<T>>(list);
    }

    /// <summary>
    ///     Traverses an enumerable, applying a function that returns an Option,
    ///     and collects all Some results. Returns None if any result is None.
    /// </summary>
    public static Option<IReadOnlyList<TResult>> Traverse<T, TResult>(
        this IEnumerable<T> source,
        Func<T, Option<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var list = new List<TResult>();
        foreach (var item in source)
        {
            var result = mapper(item);
            if (result.IsNone)
                return NoneOf<IReadOnlyList<TResult>>();
            list.Add(result.Value);
        }
        return Some<IReadOnlyList<TResult>>(list);
    }
}
