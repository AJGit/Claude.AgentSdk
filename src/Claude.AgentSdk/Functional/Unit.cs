// ReSharper disable ConvertToAutoProperty

#pragma warning disable CA1805 // Member is explicitly initialized to its default value

namespace Claude.AgentSdk.Functional;

/// <summary>
///     Represents the absence of a value, similar to void but usable as a type parameter.
///     Used in functional programming contexts where a return type is required but no value is meaningful.
/// </summary>
/// <remarks>
///     <para>
///         Unit is useful when working with generic types that require a type parameter,
///         but you don't have a meaningful value to return (like void).
///     </para>
///     <para>
///         Example usage:
///         <code>
///     // Instead of Task (void), use Task&lt;Unit&gt; for consistency
///     public Task&lt;Unit&gt; DoSomethingAsync()
///     {
///         // ... do work ...
///         return Unit.Task;
///     }
/// 
///     // Works with Result&lt;T&gt; for operations that don't return a value
///     public Result&lt;Unit&gt; ValidateInput(string input)
///     {
///         if (string.IsNullOrEmpty(input))
///             return Result.Failure&lt;Unit&gt;("Input cannot be empty");
///         return Result.Success(Unit.Value);
///     }
///     </code>
///     </para>
/// </remarks>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    /// <summary>
    ///     The single instance of Unit.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    ///     A completed task returning Unit.
    /// </summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <summary>
    ///     A completed ValueTask returning Unit.
    /// </summary>
    public static readonly ValueTask<Unit> ValueTask = new(Value);

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>
    ///     Equality operator - all Unit values are equal.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    ///     Inequality operator - all Unit values are equal.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;

    /// <summary>
    ///     Implicitly converts from ValueTuple to Unit.
    /// </summary>
    public static implicit operator Unit(ValueTuple _) => Value;

    /// <summary>
    ///     Implicitly converts from Unit to ValueTuple.
    /// </summary>
    public static implicit operator ValueTuple(Unit _) => default;
}
