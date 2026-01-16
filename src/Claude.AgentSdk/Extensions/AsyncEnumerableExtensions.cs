using System.Runtime.CompilerServices;
using Claude.AgentSdk.Functional;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Types;

namespace Claude.AgentSdk.Extensions;

/// <summary>
///     LINQ-style extension methods for IAsyncEnumerable&lt;Message&gt; streams.
///     Enables fluent processing of Claude agent message streams.
/// </summary>
/// <remarks>
///     <para>
///     These extensions provide a fluent API for filtering, transforming, and aggregating
///     message streams from Claude agent sessions.
///     </para>
///     <para>
///     Example usage:
///     <code>
///     await foreach (var text in session.StreamAsync(prompt)
///         .OfType&lt;AssistantMessage&gt;()
///         .SelectText()
///         .WhereNotEmpty())
///     {
///         Console.Write(text);
///     }
///
///     // Or aggregate all text:
///     var fullResponse = await session.StreamAsync(prompt)
///         .OfType&lt;AssistantMessage&gt;()
///         .AggregateTextAsync();
///     </code>
///     </para>
/// </remarks>
public static class AsyncEnumerableExtensions
{
    #region Filtering

    /// <summary>
    ///     Filters elements by type.
    /// </summary>
    public static async IAsyncEnumerable<T> OfType<T>(
        this IAsyncEnumerable<object> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (item is T typed)
                yield return typed;
        }
    }

    /// <summary>
    ///     Filters messages by type.
    /// </summary>
    public static async IAsyncEnumerable<T> OfType<T>(
        this IAsyncEnumerable<Message> source,
        [EnumeratorCancellation] CancellationToken ct = default)
        where T : Message
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (item is T typed)
                yield return typed;
        }
    }

    /// <summary>
    ///     Filters based on a predicate.
    /// </summary>
    public static async IAsyncEnumerable<T> Where<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item))
                yield return item;
        }
    }

    /// <summary>
    ///     Filters based on an async predicate.
    /// </summary>
    public static async IAsyncEnumerable<T> WhereAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, Task<bool>> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (await predicate(item).ConfigureAwait(false))
                yield return item;
        }
    }

    /// <summary>
    ///     Filters out null values.
    /// </summary>
    public static async IAsyncEnumerable<T> WhereNotNull<T>(
        this IAsyncEnumerable<T?> source,
        [EnumeratorCancellation] CancellationToken ct = default)
        where T : class
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (item is not null)
                yield return item;
        }
    }

    /// <summary>
    ///     Filters out empty strings.
    /// </summary>
    public static async IAsyncEnumerable<string> WhereNotEmpty(
        this IAsyncEnumerable<string?> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(item))
                yield return item;
        }
    }

    /// <summary>
    ///     Takes the first n elements.
    /// </summary>
    public static async IAsyncEnumerable<T> Take<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (count <= 0)
            yield break;

        var taken = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item;
            if (++taken >= count)
                yield break;
        }
    }

    /// <summary>
    ///     Skips the first n elements.
    /// </summary>
    public static async IAsyncEnumerable<T> Skip<T>(
        this IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var skipped = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (skipped >= count)
                yield return item;
            else
                skipped++;
        }
    }

    /// <summary>
    ///     Takes elements while a condition is true.
    /// </summary>
    public static async IAsyncEnumerable<T> TakeWhile<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (!predicate(item))
                yield break;
            yield return item;
        }
    }

    /// <summary>
    ///     Skips elements while a condition is true.
    /// </summary>
    public static async IAsyncEnumerable<T> SkipWhile<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var yielding = false;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (!yielding && !predicate(item))
                yielding = true;

            if (yielding)
                yield return item;
        }
    }

    /// <summary>
    ///     Returns distinct elements.
    /// </summary>
    public static async IAsyncEnumerable<T> Distinct<T>(
        this IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var seen = new HashSet<T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (seen.Add(item))
                yield return item;
        }
    }

    /// <summary>
    ///     Returns distinct elements by key.
    /// </summary>
    public static async IAsyncEnumerable<T> DistinctBy<T, TKey>(
        this IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        var seen = new HashSet<TKey>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (seen.Add(keySelector(item)))
                yield return item;
        }
    }

    #endregion

    #region Transformation

    /// <summary>
    ///     Projects each element.
    /// </summary>
    public static async IAsyncEnumerable<TResult> Select<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, TResult> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return selector(item);
        }
    }

    /// <summary>
    ///     Projects each element with an async selector.
    /// </summary>
    public static async IAsyncEnumerable<TResult> SelectAsync<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, Task<TResult>> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return await selector(item).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Projects each element to a sequence and flattens.
    /// </summary>
    public static async IAsyncEnumerable<TResult> SelectMany<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, IEnumerable<TResult>> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            foreach (var inner in selector(item))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    ///     Projects each element to an async sequence and flattens.
    /// </summary>
    public static async IAsyncEnumerable<TResult> SelectMany<T, TResult>(
        this IAsyncEnumerable<T> source,
        Func<T, IAsyncEnumerable<TResult>> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selector);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            await foreach (var inner in selector(item).WithCancellation(ct).ConfigureAwait(false))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    ///     Casts each element to a type.
    /// </summary>
    public static async IAsyncEnumerable<TResult> Cast<TResult>(
        this IAsyncEnumerable<object> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return (TResult)item;
        }
    }

    #endregion

    #region Message-Specific Transformations

    /// <summary>
    ///     Extracts text from assistant messages.
    /// </summary>
    public static async IAsyncEnumerable<string> SelectText(
        this IAsyncEnumerable<AssistantMessage> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return message.GetText();
        }
    }

    /// <summary>
    ///     Extracts tool uses from assistant messages.
    /// </summary>
    public static async IAsyncEnumerable<ToolUseBlock> SelectToolUses(
        this IAsyncEnumerable<AssistantMessage> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in source.WithCancellation(ct).ConfigureAwait(false))
        {
            foreach (var toolUse in message.GetToolUses())
            {
                yield return toolUse;
            }
        }
    }

    /// <summary>
    ///     Filters to tool uses of a specific tool.
    /// </summary>
    public static async IAsyncEnumerable<ToolUseBlock> WhereTool(
        this IAsyncEnumerable<ToolUseBlock> source,
        ToolName toolName,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var toolUse in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (toolUse.IsTool(toolName))
                yield return toolUse;
        }
    }

    /// <summary>
    ///     Extracts thinking blocks from assistant messages.
    /// </summary>
    public static async IAsyncEnumerable<ThinkingBlock> SelectThinking(
        this IAsyncEnumerable<AssistantMessage> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in source.WithCancellation(ct).ConfigureAwait(false))
        {
            foreach (var thinking in message.GetThinking())
            {
                yield return thinking;
            }
        }
    }

    #endregion

    #region Side Effects

    /// <summary>
    ///     Executes an action for each element without transforming.
    /// </summary>
    public static async IAsyncEnumerable<T> Do<T>(
        this IAsyncEnumerable<T> source,
        Action<T> action,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            action(item);
            yield return item;
        }
    }

    /// <summary>
    ///     Executes an async action for each element without transforming.
    /// </summary>
    public static async IAsyncEnumerable<T> DoAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, Task> action,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            await action(item).ConfigureAwait(false);
            yield return item;
        }
    }

    /// <summary>
    ///     Logs each element (for debugging).
    /// </summary>
    public static IAsyncEnumerable<T> Log<T>(
        this IAsyncEnumerable<T> source,
        Action<string> logger,
        string? prefix = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var actualPrefix = string.IsNullOrEmpty(prefix) ? "" : $"{prefix}: ";
        return source.Do(item => logger($"{actualPrefix}{item}"), ct);
    }

    #endregion

    #region Aggregation

    /// <summary>
    ///     Aggregates all text from assistant messages into a single string.
    /// </summary>
    public static async Task<string> AggregateTextAsync(
        this IAsyncEnumerable<AssistantMessage> source,
        string separator = "",
        CancellationToken ct = default)
    {
        var texts = new List<string>();
        await foreach (var message in source.WithCancellation(ct).ConfigureAwait(false))
        {
            var text = message.GetText();
            if (!string.IsNullOrEmpty(text))
                texts.Add(text);
        }
        return string.Join(separator, texts);
    }

    /// <summary>
    ///     Aggregates all text from messages into a single string.
    /// </summary>
    public static async Task<string> AggregateTextAsync(
        this IAsyncEnumerable<Message> source,
        string separator = "",
        CancellationToken ct = default)
    {
        var texts = new List<string>();
        await foreach (var message in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (message is AssistantMessage assistant)
            {
                var text = assistant.GetText();
                if (!string.IsNullOrEmpty(text))
                    texts.Add(text);
            }
        }
        return string.Join(separator, texts);
    }

    /// <summary>
    ///     Collects all elements into a list.
    /// </summary>
    public static async Task<List<T>> ToListAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            list.Add(item);
        }
        return list;
    }

    /// <summary>
    ///     Collects all elements into an array.
    /// </summary>
    public static async Task<T[]> ToArrayAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default) =>
        (await source.ToListAsync(ct).ConfigureAwait(false)).ToArray();

    /// <summary>
    ///     Collects all elements into a dictionary.
    /// </summary>
    public static async Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(
        this IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        CancellationToken ct = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        var dict = new Dictionary<TKey, T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            dict[keySelector(item)] = item;
        }
        return dict;
    }

    /// <summary>
    ///     Counts elements.
    /// </summary>
    public static async Task<int> CountAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var count = 0;
        await foreach (var _ in source.WithCancellation(ct).ConfigureAwait(false))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    ///     Counts elements matching a predicate.
    /// </summary>
    public static async Task<int> CountAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var count = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item))
                count++;
        }
        return count;
    }

    /// <summary>
    ///     Aggregates elements using a custom aggregator.
    /// </summary>
    public static async Task<TAccumulate> AggregateAsync<T, TAccumulate>(
        this IAsyncEnumerable<T> source,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> accumulator,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accumulator);
        var result = seed;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            result = accumulator(result, item);
        }
        return result;
    }

    #endregion

    #region First/Last/Single

    /// <summary>
    ///     Returns the first element.
    /// </summary>
    public static async Task<T> FirstAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            return item;
        }
        throw new InvalidOperationException("Sequence contains no elements.");
    }

    /// <summary>
    ///     Returns the first element, or default if empty.
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            return item;
        }
        return default;
    }

    /// <summary>
    ///     Returns the first element matching a predicate.
    /// </summary>
    public static async Task<T> FirstAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item))
                return item;
        }
        throw new InvalidOperationException("Sequence contains no matching element.");
    }

    /// <summary>
    ///     Returns the first element matching a predicate, or default.
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item))
                return item;
        }
        return default;
    }

    /// <summary>
    ///     Returns the first element as an Option.
    /// </summary>
    public static async Task<Option<T>> FirstOptionAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            return Option.Some(item);
        }
        return Option.NoneOf<T>();
    }

    /// <summary>
    ///     Returns the last element.
    /// </summary>
    public static async Task<T> LastAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var hasValue = false;
        T last = default!;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            hasValue = true;
            last = item;
        }
        return hasValue ? last : throw new InvalidOperationException("Sequence contains no elements.");
    }

    /// <summary>
    ///     Returns the last element, or default if empty.
    /// </summary>
    public static async Task<T?> LastOrDefaultAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        T? last = default;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            last = item;
        }
        return last;
    }

    /// <summary>
    ///     Returns the single element.
    /// </summary>
    public static async Task<T> SingleAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var hasValue = false;
        T result = default!;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (hasValue)
                throw new InvalidOperationException("Sequence contains more than one element.");
            hasValue = true;
            result = item;
        }
        return hasValue ? result : throw new InvalidOperationException("Sequence contains no elements.");
    }

    /// <summary>
    ///     Returns the single element, or default.
    /// </summary>
    public static async Task<T?> SingleOrDefaultAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var hasValue = false;
        T? result = default;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (hasValue)
                throw new InvalidOperationException("Sequence contains more than one element.");
            hasValue = true;
            result = item;
        }
        return result;
    }

    #endregion

    #region Predicates

    /// <summary>
    ///     Returns true if any element matches the predicate.
    /// </summary>
    public static async Task<bool> AnyAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item))
                return true;
        }
        return false;
    }

    /// <summary>
    ///     Returns true if the sequence has any elements.
    /// </summary>
    public static async Task<bool> AnyAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await foreach (var _ in source.WithCancellation(ct).ConfigureAwait(false))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    ///     Returns true if all elements match the predicate.
    /// </summary>
    public static async Task<bool> AllAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (!predicate(item))
                return false;
        }
        return true;
    }

    /// <summary>
    ///     Returns true if the sequence contains the specified element.
    /// </summary>
    public static async Task<bool> ContainsAsync<T>(
        this IAsyncEnumerable<T> source,
        T value,
        CancellationToken ct = default)
    {
        var comparer = EqualityComparer<T>.Default;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (comparer.Equals(item, value))
                return true;
        }
        return false;
    }

    #endregion

    #region Combination

    /// <summary>
    ///     Concatenates two sequences.
    /// </summary>
    public static async IAsyncEnumerable<T> Concat<T>(
        this IAsyncEnumerable<T> first,
        IAsyncEnumerable<T> second,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in first.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item;
        }
        await foreach (var item in second.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    ///     Prepends an element to the sequence.
    /// </summary>
    public static async IAsyncEnumerable<T> Prepend<T>(
        this IAsyncEnumerable<T> source,
        T element,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return element;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    ///     Appends an element to the sequence.
    /// </summary>
    public static async IAsyncEnumerable<T> Append<T>(
        this IAsyncEnumerable<T> source,
        T element,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item;
        }
        yield return element;
    }

    /// <summary>
    ///     Zips two sequences together.
    /// </summary>
    public static async IAsyncEnumerable<(T1, T2)> Zip<T1, T2>(
        this IAsyncEnumerable<T1> first,
        IAsyncEnumerable<T2> second,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var e1 = first.GetAsyncEnumerator(ct);
        await using var e2 = second.GetAsyncEnumerator(ct);

        while (await e1.MoveNextAsync().ConfigureAwait(false) &&
               await e2.MoveNextAsync().ConfigureAwait(false))
        {
            yield return (e1.Current, e2.Current);
        }
    }

    /// <summary>
    ///     Zips with a result selector.
    /// </summary>
    public static async IAsyncEnumerable<TResult> Zip<T1, T2, TResult>(
        this IAsyncEnumerable<T1> first,
        IAsyncEnumerable<T2> second,
        Func<T1, T2, TResult> resultSelector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(resultSelector);
        await using var e1 = first.GetAsyncEnumerator(ct);
        await using var e2 = second.GetAsyncEnumerator(ct);

        while (await e1.MoveNextAsync().ConfigureAwait(false) &&
               await e2.MoveNextAsync().ConfigureAwait(false))
        {
            yield return resultSelector(e1.Current, e2.Current);
        }
    }

    #endregion

    #region Buffering

    /// <summary>
    ///     Buffers elements into chunks.
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<T>> Buffer<T>(
        this IAsyncEnumerable<T> source,
        int size,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Buffer size must be positive.");

        var buffer = new List<T>(size);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            buffer.Add(item);
            if (buffer.Count >= size)
            {
                yield return buffer;
                buffer = new List<T>(size);
            }
        }

        if (buffer.Count > 0)
            yield return buffer;
    }

    /// <summary>
    ///     Buffers elements by time window.
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<T>> BufferByTime<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan timeSpan,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var buffer = new List<T>();
        var lastFlush = DateTime.UtcNow;

        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            buffer.Add(item);
            if (DateTime.UtcNow - lastFlush >= timeSpan)
            {
                yield return buffer;
                buffer = new List<T>();
                lastFlush = DateTime.UtcNow;
            }
        }

        if (buffer.Count > 0)
            yield return buffer;
    }

    #endregion

    #region ForEach

    /// <summary>
    ///     Executes an action for each element.
    /// </summary>
    public static async Task ForEachAsync<T>(
        this IAsyncEnumerable<T> source,
        Action<T> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            action(item);
        }
    }

    /// <summary>
    ///     Executes an async action for each element.
    /// </summary>
    public static async Task ForEachAsync<T>(
        this IAsyncEnumerable<T> source,
        Func<T, Task> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            await action(item).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Executes an action for each element with its index.
    /// </summary>
    public static async Task ForEachAsync<T>(
        this IAsyncEnumerable<T> source,
        Action<T, int> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var index = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            action(item, index++);
        }
    }

    #endregion
}
