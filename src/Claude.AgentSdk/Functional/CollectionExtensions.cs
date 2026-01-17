namespace Claude.AgentSdk.Functional;

/// <summary>
///     Functional extension methods for collections.
///     Provides Option-based and Result-based collection operations.
/// </summary>
/// <remarks>
///     <para>
///         These extensions enable safer collection operations without exceptions:
///         <code>
///     var users = GetUsers();
/// 
///     // Safe first element access
///     Option&lt;User&gt; firstUser = users.FirstOrNone();
/// 
///     // Filter and unwrap Options
///     IEnumerable&lt;string&gt; emails = users
///         .Choose(u => u.Email.IsVerified ? Option.Some(u.Email.Address) : Option.None);
/// 
///     // Partition by success/failure
///     var (successes, failures) = results.Partition();
///     </code>
///     </para>
/// </remarks>
public static class CollectionExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        ///     Groups elements by a key selector that may fail.
        ///     Returns groups for successful keys and a list of failures.
        /// </summary>
        public (IReadOnlyDictionary<TKey, IReadOnlyList<T>> Groups, IReadOnlyList<(T Item, string Error)> Failures)
            GroupByResult<TKey>(Func<T, Result<TKey>> keySelector)
            where TKey : notnull
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(keySelector);

            var groups = new Dictionary<TKey, List<T>>();
            var failures = new List<(T, string)>();

            foreach (var item in source)
            {
                var keyResult = keySelector(item);
                if (keyResult.IsSuccess)
                {
                    if (!groups.TryGetValue(keyResult.Value, out var list))
                    {
                        list = [];
                        groups[keyResult.Value] = list;
                    }

                    list.Add(item);
                }
                else
                {
                    failures.Add((item, keyResult.Error));
                }
            }

            var readOnlyGroups = groups.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<T>)kvp.Value);

            return (readOnlyGroups, failures);
        }

        /// <summary>
        ///     Zips two sequences and applies a function, returning None if lengths differ.
        /// </summary>
        public Option<IReadOnlyList<TResult>> ZipExact<T2, TResult>(IEnumerable<T2> second,
            Func<T, T2, TResult> resultSelector)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(second);
            ArgumentNullException.ThrowIfNull(resultSelector);

            var list1 = source.ToList();
            var list2 = second.ToList();

            if (list1.Count != list2.Count)
            {
                return Option.NoneOf<IReadOnlyList<TResult>>();
            }

            var results = new List<TResult>(list1.Count);
            for (var i = 0; i < list1.Count; i++)
            {
                results.Add(resultSelector(list1[i], list2[i]));
            }

            return Option.Some<IReadOnlyList<TResult>>(results);
        }

        /// <summary>
        ///     Batches elements into chunks, processing each batch with a Result-returning function.
        /// </summary>
        public IEnumerable<Result<TResult>> BatchProcess<TResult>(int batchSize,
            Func<IReadOnlyList<T>, Result<TResult>> batchProcessor)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(batchProcessor);
            if (batchSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");
            }

            var batch = new List<T>(batchSize);

            foreach (var item in source)
            {
                batch.Add(item);

                if (batch.Count >= batchSize)
                {
                    yield return batchProcessor(batch);
                    batch = new List<T>(batchSize);
                }
            }

            if (batch.Count > 0)
            {
                yield return batchProcessor(batch);
            }
        }

        /// <summary>
        ///     Gets the first element as an Option, or None if empty.
        /// </summary>
        public Option<T> FirstOrNone()
        {
            ArgumentNullException.ThrowIfNull(source);

            if (source is IList<T> list)
            {
                return list.Count > 0 ? Option.Some(list[0]) : Option.NoneOf<T>();
            }

            using var enumerator = source.GetEnumerator();
            return enumerator.MoveNext() ? Option.Some(enumerator.Current) : Option.NoneOf<T>();
        }

        /// <summary>
        ///     Gets the first element matching a predicate as an Option.
        /// </summary>
        public Option<T> FirstOrNone(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            foreach (var item in source)
            {
                if (predicate(item))
                {
                    return Option.Some(item);
                }
            }

            return Option.NoneOf<T>();
        }

        /// <summary>
        ///     Gets the last element as an Option, or None if empty.
        /// </summary>
        public Option<T> LastOrNone()
        {
            ArgumentNullException.ThrowIfNull(source);

            if (source is IList<T> list)
            {
                return list.Count > 0 ? Option.Some(list[^1]) : Option.NoneOf<T>();
            }

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return Option.NoneOf<T>();
            }

            T last = enumerator.Current;
            while (enumerator.MoveNext())
            {
                last = enumerator.Current;
            }

            return Option.Some(last);
        }

        /// <summary>
        ///     Gets the last element matching a predicate as an Option.
        /// </summary>
        public Option<T> LastOrNone(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            var result = Option.NoneOf<T>();
            foreach (var item in source)
            {
                if (predicate(item))
                {
                    result = Option.Some(item);
                }
            }

            return result;
        }

        /// <summary>
        ///     Gets the single element as an Option, or None if empty or more than one.
        /// </summary>
        public Option<T> SingleOrNone()
        {
            ArgumentNullException.ThrowIfNull(source);

            if (source is IList<T> list)
            {
                return list.Count == 1 ? Option.Some(list[0]) : Option.NoneOf<T>();
            }

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return Option.NoneOf<T>();
            }

            var item = enumerator.Current;
            return enumerator.MoveNext() ? Option.NoneOf<T>() : Option.Some(item);
        }

        /// <summary>
        ///     Gets the single element matching a predicate as an Option.
        /// </summary>
        public Option<T> SingleOrNone(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            Option<T> result = Option.NoneOf<T>();
            foreach (var item in source)
            {
                if (!predicate(item))
                {
                    continue;
                }

                if (result.IsSome)
                {
                    return Option.NoneOf<T>(); // More than one match
                }

                result = Option.Some(item);
            }

            return result;
        }

        /// <summary>
        ///     Gets an element at the specified index as an Option.
        /// </summary>
        public Option<T> ElementAtOrNone(int index)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (index < 0)
            {
                return Option.NoneOf<T>();
            }

            if (source is IList<T> list)
            {
                return index < list.Count ? Option.Some(list[index]) : Option.NoneOf<T>();
            }

            var currentIndex = 0;
            foreach (var item in source)
            {
                if (currentIndex == index)
                {
                    return Option.Some(item);
                }

                currentIndex++;
            }

            return Option.NoneOf<T>();
        }

        /// <summary>
        ///     Filters and maps in one operation using Options.
        ///     Keeps only Some values and unwraps them.
        /// </summary>
        public IEnumerable<TResult> Choose<TResult>(Func<T, Option<TResult>> chooser)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(chooser);

            foreach (var item in source)
            {
                var result = chooser(item);
                if (result.IsSome)
                {
                    yield return result.Value;
                }
            }
        }

        /// <summary>
        ///     Filters and maps in one operation using Results.
        ///     Keeps only Success values and unwraps them.
        /// </summary>
        public IEnumerable<TResult> Choose<TResult>(Func<T, Result<TResult>> chooser)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(chooser);

            foreach (var item in source)
            {
                var result = chooser(item);
                if (result.IsSuccess)
                {
                    yield return result.Value;
                }
            }
        }
    }

    /// <summary>
    ///     Filters Options to keep only Some values.
    /// </summary>
    public static IEnumerable<T> Some<T>(this IEnumerable<Option<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var option in source)
        {
            if (option.IsSome)
            {
                yield return option.Value;
            }
        }
    }

    /// <summary>
    ///     Filters Results to keep only Success values.
    /// </summary>
    public static IEnumerable<T> Successes<T>(this IEnumerable<Result<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var result in source)
        {
            if (result.IsSuccess)
            {
                yield return result.Value;
            }
        }
    }

    /// <summary>
    ///     Filters Results to keep only Success values.
    /// </summary>
    public static IEnumerable<T> Successes<T, TError>(this IEnumerable<Result<T, TError>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var result in source)
        {
            if (result.IsSuccess)
            {
                yield return result.Value;
            }
        }
    }

    /// <summary>
    ///     Filters Results to keep only Failure errors.
    /// </summary>
    public static IEnumerable<string> Failures<T>(this IEnumerable<Result<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var result in source)
        {
            if (result.IsFailure)
            {
                yield return result.Error;
            }
        }
    }

    /// <summary>
    ///     Filters Results to keep only Failure errors.
    /// </summary>
    public static IEnumerable<TError> Failures<T, TError>(this IEnumerable<Result<T, TError>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var result in source)
        {
            if (result.IsFailure)
            {
                yield return result.Error;
            }
        }
    }

    /// <summary>
    ///     Partitions Options into Some values and count of None.
    /// </summary>
    public static (IReadOnlyList<T> Somes, int NoneCount) Partition<T>(
        this IEnumerable<Option<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var somes = new List<T>();
        var noneCount = 0;

        foreach (var option in source)
        {
            if (option.IsSome)
            {
                somes.Add(option.Value);
            }
            else
            {
                noneCount++;
            }
        }

        return (somes, noneCount);
    }

    /// <summary>
    ///     Partitions Results into Success values and Failure errors.
    /// </summary>
    public static (IReadOnlyList<T> Successes, IReadOnlyList<string> Failures) Partition<T>(
        this IEnumerable<Result<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var successes = new List<T>();
        var failures = new List<string>();

        foreach (var result in source)
        {
            if (result.IsSuccess)
            {
                successes.Add(result.Value);
            }
            else
            {
                failures.Add(result.Error);
            }
        }

        return (successes, failures);
    }

    /// <summary>
    ///     Partitions Results into Success values and Failure errors.
    /// </summary>
    public static (IReadOnlyList<T> Successes, IReadOnlyList<TError> Failures) Partition<T, TError>(
        this IEnumerable<Result<T, TError>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var successes = new List<T>();
        var failures = new List<TError>();

        foreach (var result in source)
        {
            if (result.IsSuccess)
            {
                successes.Add(result.Value);
            }
            else
            {
                failures.Add(result.Error);
            }
        }

        return (successes, failures);
    }

    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        ///     Partitions elements by a predicate.
        /// </summary>
        public (IReadOnlyList<T> Matching, IReadOnlyList<T> NonMatching) PartitionBy(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(predicate);

            var matching = new List<T>();
            var nonMatching = new List<T>();

            foreach (var item in source)
            {
                if (predicate(item))
                {
                    matching.Add(item);
                }
                else
                {
                    nonMatching.Add(item);
                }
            }

            return (matching, nonMatching);
        }

        /// <summary>
        ///     Folds a collection with a Result-returning folder, short-circuiting on failure.
        /// </summary>
        public Result<TAccumulator> FoldResult<TAccumulator>(TAccumulator seed,
            Func<TAccumulator, T, Result<TAccumulator>> folder)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(folder);

            var accumulator = Result.Success(seed);
            foreach (var item in source)
            {
                if (accumulator.IsFailure)
                {
                    return accumulator;
                }

                accumulator = folder(accumulator.Value, item);
            }

            return accumulator;
        }
    }

    /// <summary>
    ///     Folds a collection while a predicate is true.
    /// </summary>
    public static T FoldWhile<T, TItem>(
        this IEnumerable<TItem> source,
        T seed,
        Func<T, TItem, T> folder,
        Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(predicate);

        var accumulator = seed;
        foreach (var item in source)
        {
            if (!predicate(accumulator))
            {
                break;
            }

            accumulator = folder(accumulator, item);
        }

        return accumulator;
    }

    extension<T>(IEnumerable<T> source) where T : IComparable<T>
    {
        /// <summary>
        ///     Safely computes the maximum, returning None for empty sequences.
        /// </summary>
        public Option<T> MaxOrNone()
        {
            ArgumentNullException.ThrowIfNull(source);

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return Option.NoneOf<T>();
            }

            var max = enumerator.Current;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.CompareTo(max) > 0)
                {
                    max = enumerator.Current;
                }
            }

            return Option.Some(max);
        }

        /// <summary>
        ///     Safely computes the minimum, returning None for empty sequences.
        /// </summary>
        public Option<T> MinOrNone()
        {
            ArgumentNullException.ThrowIfNull(source);

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return Option.NoneOf<T>();
            }

            var min = enumerator.Current;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.CompareTo(min) < 0)
                {
                    min = enumerator.Current;
                }
            }

            return Option.Some(min);
        }
    }

    /// <summary>
    ///     Safely computes the average, returning None for empty sequences.
    /// </summary>
    public static Option<double> AverageOrNone(this IEnumerable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        long sum = 0;
        var count = 0;

        foreach (var item in source)
        {
            sum += item;
            count++;
        }

        return count > 0 ? Option.Some((double)sum / count) : Option.NoneOf<double>();
    }

    /// <summary>
    ///     Safely computes the average, returning None for empty sequences.
    /// </summary>
    public static Option<double> AverageOrNone(this IEnumerable<double> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        double sum = 0;
        var count = 0;

        foreach (var item in source)
        {
            sum += item;
            count++;
        }

        return count > 0 ? Option.Some(sum / count) : Option.NoneOf<double>();
    }

    /// <summary>
    ///     Tries to get a value from a dictionary, returning an Option.
    /// </summary>
    public static Option<TValue> GetValueOrNone<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> dictionary,
        TKey key)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.TryGetValue(key, out var value) ? Option.Some(value) : Option.NoneOf<TValue>();
    }

    /// <summary>
    ///     Tries to get a value from a dictionary, returning an Option.
    /// </summary>
    public static Option<TValue> GetValueOrNone<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.TryGetValue(key, out var value) ? Option.Some(value) : Option.NoneOf<TValue>();
    }
}
