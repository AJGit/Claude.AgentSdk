namespace Claude.AgentSdk.Functional;

/// <summary>
///     Represents a computation that requires an environment/context to produce a result.
///     Useful for dependency injection style composition without explicit parameter passing.
/// </summary>
/// <typeparam name="TEnv">The environment/context type.</typeparam>
/// <typeparam name="T">The result type.</typeparam>
/// <remarks>
///     <para>
///         Reader monad allows you to compose functions that depend on a shared context
///         without explicitly threading that context through every function call.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     // Define a context
///     record AppConfig(string ConnectionString, ILogger Logger);
/// 
///     // Create readers that depend on the context
///     Reader&lt;AppConfig, string&gt; getConnectionString =
///         Reader.Ask&lt;AppConfig&gt;().Map(cfg => cfg.ConnectionString);
/// 
///     Reader&lt;AppConfig, User&gt; getUser(int id) =>
///         getConnectionString.Bind(connStr => Reader.Return&lt;AppConfig, User&gt;(
///             LoadUser(connStr, id)));
/// 
///     // Run with context
///     var config = new AppConfig("...", logger);
///     User user = getUser(123).Run(config);
///     </code>
///     </para>
/// </remarks>
public readonly struct Reader<TEnv, T>
{
    private readonly Func<TEnv, T> _run;

    /// <summary>
    ///     Creates a Reader from a function.
    /// </summary>
    public Reader(Func<TEnv, T> run)
    {
        _run = run ?? throw new ArgumentNullException(nameof(run));
    }

    /// <summary>
    ///     Runs the Reader with the provided environment.
    /// </summary>
    public T Run(TEnv env) => _run(env);

    /// <summary>
    ///     Transforms the result of this Reader.
    /// </summary>
    public Reader<TEnv, TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var run = _run;
        return new Reader<TEnv, TResult>(env => mapper(run(env)));
    }

    /// <summary>
    ///     Chains Reader-returning computations.
    /// </summary>
    public Reader<TEnv, TResult> Bind<TResult>(Func<T, Reader<TEnv, TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        var run = _run;
        return new Reader<TEnv, TResult>(env =>
        {
            var value = run(env);
            return binder(value).Run(env);
        });
    }

    /// <summary>
    ///     Applies a wrapped function to this Reader's result.
    /// </summary>
    public Reader<TEnv, TResult> Apply<TResult>(Reader<TEnv, Func<T, TResult>> funcReader)
    {
        var run = _run;
        return new Reader<TEnv, TResult>(env =>
        {
            var func = funcReader.Run(env);
            var value = run(env);
            return func(value);
        });
    }

    /// <summary>
    ///     Combines two Readers using a selector function.
    /// </summary>
    public Reader<TEnv, TResult> SelectMany<T2, TResult>(
        Func<T, Reader<TEnv, T2>> binder,
        Func<T, T2, TResult> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(resultSelector);
        var run = _run;
        return new Reader<TEnv, TResult>(env =>
        {
            var value1 = run(env);
            var value2 = binder(value1).Run(env);
            return resultSelector(value1, value2);
        });
    }

    /// <summary>
    ///     Converts to a Reader that returns a Result.
    /// </summary>
    public Reader<TEnv, Result<T>> ToResult()
    {
        var run = _run;
        return new Reader<TEnv, Result<T>>(env =>
        {
            try
            {
                return Result.Success(run(env));
            }
            catch (Exception ex)
            {
                return Result.Failure<T>(ex.Message);
            }
        });
    }

    /// <summary>
    ///     Converts to a Reader that returns an Option.
    /// </summary>
    public Reader<TEnv, Option<T>> ToOption()
    {
        var run = _run;
        return new Reader<TEnv, Option<T>>(env =>
        {
            try
            {
                var value = run(env);
                return value is null ? Option.NoneOf<T>() : Option.Some(value);
            }
            catch
            {
                return Option.NoneOf<T>();
            }
        });
    }

    /// <summary>
    ///     Adapts this Reader to work with a different environment type.
    /// </summary>
    public Reader<TNewEnv, T> Local<TNewEnv>(Func<TNewEnv, TEnv> envMapper)
    {
        ArgumentNullException.ThrowIfNull(envMapper);
        var run = _run;
        return new Reader<TNewEnv, T>(newEnv => run(envMapper(newEnv)));
    }
}

/// <summary>
///     Async version of Reader monad.
/// </summary>
public readonly struct ReaderAsync<TEnv, T>
{
    private readonly Func<TEnv, Task<T>> _run;

    /// <summary>
    ///     Creates an async Reader from an async function.
    /// </summary>
    public ReaderAsync(Func<TEnv, Task<T>> run)
    {
        _run = run ?? throw new ArgumentNullException(nameof(run));
    }

    /// <summary>
    ///     Runs the async Reader with the provided environment.
    /// </summary>
    public Task<T> RunAsync(TEnv env) => _run(env);

    /// <summary>
    ///     Transforms the result of this async Reader.
    /// </summary>
    public ReaderAsync<TEnv, TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var run = _run;
        return new ReaderAsync<TEnv, TResult>(async env => mapper(await run(env).ConfigureAwait(false)));
    }

    /// <summary>
    ///     Transforms the result of this async Reader with an async mapper.
    /// </summary>
    public ReaderAsync<TEnv, TResult> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var run = _run;
        return new ReaderAsync<TEnv, TResult>(async env =>
        {
            var value = await run(env).ConfigureAwait(false);
            return await mapper(value).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Chains async Reader-returning computations.
    /// </summary>
    public ReaderAsync<TEnv, TResult> Bind<TResult>(Func<T, ReaderAsync<TEnv, TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        var run = _run;
        return new ReaderAsync<TEnv, TResult>(async env =>
        {
            var value = await run(env).ConfigureAwait(false);
            return await binder(value).RunAsync(env).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Chains async Reader-returning computations with async binder.
    /// </summary>
    public ReaderAsync<TEnv, TResult> BindAsync<TResult>(Func<T, Task<ReaderAsync<TEnv, TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        var run = _run;
        return new ReaderAsync<TEnv, TResult>(async env =>
        {
            var value = await run(env).ConfigureAwait(false);
            var reader = await binder(value).ConfigureAwait(false);
            return await reader.RunAsync(env).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Converts to an async Reader that returns a Result.
    /// </summary>
    public ReaderAsync<TEnv, Result<T>> ToResult()
    {
        var run = _run;
        return new ReaderAsync<TEnv, Result<T>>(async env =>
        {
            try
            {
                return Result.Success(await run(env).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                return Result.Failure<T>(ex.Message);
            }
        });
    }

    /// <summary>
    ///     Adapts this async Reader to work with a different environment type.
    /// </summary>
    public ReaderAsync<TNewEnv, T> Local<TNewEnv>(Func<TNewEnv, TEnv> envMapper)
    {
        ArgumentNullException.ThrowIfNull(envMapper);
        var run = _run;
        return new ReaderAsync<TNewEnv, T>(newEnv => run(envMapper(newEnv)));
    }
}

/// <summary>
///     Static helper methods for creating Reader values.
/// </summary>
public static class Reader
{
    /// <summary>
    ///     Creates a Reader that returns a constant value.
    /// </summary>
    public static Reader<TEnv, T> Return<TEnv, T>(T value) =>
        new(_ => value);

    /// <summary>
    ///     Creates a Reader that returns the environment itself.
    /// </summary>
    public static Reader<TEnv, TEnv> Ask<TEnv>() =>
        new(env => env);

    /// <summary>
    ///     Creates a Reader from a function.
    /// </summary>
    public static Reader<TEnv, T> From<TEnv, T>(Func<TEnv, T> func) =>
        new(func);

    /// <summary>
    ///     Creates an async Reader that returns a constant value.
    /// </summary>
    public static ReaderAsync<TEnv, T> ReturnAsync<TEnv, T>(T value) =>
        new(_ => Task.FromResult(value));

    /// <summary>
    ///     Creates an async Reader that returns the environment itself.
    /// </summary>
    public static ReaderAsync<TEnv, TEnv> AskAsync<TEnv>() =>
        new(Task.FromResult);

    /// <summary>
    ///     Creates an async Reader from a function.
    /// </summary>
    public static ReaderAsync<TEnv, T> FromAsync<TEnv, T>(Func<TEnv, Task<T>> func) =>
        new(func);

    /// <summary>
    ///     Creates a Reader that extracts a value from the environment.
    /// </summary>
    public static Reader<TEnv, T> Asks<TEnv, T>(Func<TEnv, T> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new Reader<TEnv, T>(selector);
    }

    /// <summary>
    ///     Lifts a sync Reader to async.
    /// </summary>
    public static ReaderAsync<TEnv, T> ToAsync<TEnv, T>(this Reader<TEnv, T> reader) =>
        new(env => Task.FromResult(reader.Run(env)));

    /// <summary>
    ///     Combines two Readers into a tuple.
    /// </summary>
    public static Reader<TEnv, (T1, T2)> Zip<TEnv, T1, T2>(
        Reader<TEnv, T1> reader1,
        Reader<TEnv, T2> reader2) =>
        new(env => (reader1.Run(env), reader2.Run(env)));

    /// <summary>
    ///     Combines three Readers into a tuple.
    /// </summary>
    public static Reader<TEnv, (T1, T2, T3)> Zip<TEnv, T1, T2, T3>(
        Reader<TEnv, T1> reader1,
        Reader<TEnv, T2> reader2,
        Reader<TEnv, T3> reader3) =>
        new(env => (reader1.Run(env), reader2.Run(env), reader3.Run(env)));

    /// <summary>
    ///     Applies a function within the Reader context.
    /// </summary>
    public static Reader<TEnv, TResult> Map2<TEnv, T1, T2, TResult>(
        Reader<TEnv, T1> reader1,
        Reader<TEnv, T2> reader2,
        Func<T1, T2, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return new Reader<TEnv, TResult>(env =>
            mapper(reader1.Run(env), reader2.Run(env)));
    }

    /// <summary>
    ///     Applies a function within the Reader context.
    /// </summary>
    public static Reader<TEnv, TResult> Map3<TEnv, T1, T2, T3, TResult>(
        Reader<TEnv, T1> reader1,
        Reader<TEnv, T2> reader2,
        Reader<TEnv, T3> reader3,
        Func<T1, T2, T3, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return new Reader<TEnv, TResult>(env =>
            mapper(reader1.Run(env), reader2.Run(env), reader3.Run(env)));
    }

    /// <summary>
    ///     Sequences a collection of Readers.
    /// </summary>
    public static Reader<TEnv, IReadOnlyList<T>> Sequence<TEnv, T>(
        this IEnumerable<Reader<TEnv, T>> readers) =>
        new(env => readers.Select(r => r.Run(env)).ToList());

    /// <summary>
    ///     Traverses a collection with a Reader-returning function.
    /// </summary>
    public static Reader<TEnv, IReadOnlyList<TResult>> Traverse<TEnv, T, TResult>(
        this IEnumerable<T> source,
        Func<T, Reader<TEnv, TResult>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return new Reader<TEnv, IReadOnlyList<TResult>>(env =>
            source.Select(item => func(item).Run(env)).ToList());
    }
}
