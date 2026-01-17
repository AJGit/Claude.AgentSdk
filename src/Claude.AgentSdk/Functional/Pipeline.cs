namespace Claude.AgentSdk.Functional;

/// <summary>
///     A fluent pipeline builder for composing operations with Result-based error handling.
///     Enables railway-oriented programming patterns.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
/// <remarks>
///     <para>
///         Pipeline enables composing multiple operations that may fail,
///         with automatic short-circuiting on errors.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     var pipeline = Pipeline
///         .Start&lt;string&gt;()
///         .Then(ValidateInput)
///         .Then(ParseJson)
///         .Then(TransformData)
///         .ThenTap(LogSuccess)
///         .Catch(error => $"Pipeline failed: {error}");
/// 
///     Result&lt;OutputData&gt; result = pipeline.Run(inputString);
///     </code>
///     </para>
/// </remarks>
public sealed class Pipeline<TInput, TOutput>
{
    private readonly Func<TInput, Result<TOutput>> _execute;

    internal Pipeline(Func<TInput, Result<TOutput>> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>
    ///     Runs the pipeline with the given input.
    /// </summary>
    public Result<TOutput> Run(TInput input) => _execute(input);

    /// <summary>
    ///     Adds a transformation step to the pipeline.
    /// </summary>
    public Pipeline<TInput, TResult> Then<TResult>(Func<TOutput, TResult> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new Pipeline<TInput, TResult>(input =>
            _execute(input).Map(transform));
    }

    /// <summary>
    ///     Adds a Result-returning step to the pipeline.
    /// </summary>
    public Pipeline<TInput, TResult> ThenBind<TResult>(Func<TOutput, Result<TResult>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new Pipeline<TInput, TResult>(input =>
            _execute(input).Bind(transform));
    }

    /// <summary>
    ///     Adds a validation step that ensures a condition is met.
    /// </summary>
    public Pipeline<TInput, TOutput> ThenEnsure(Func<TOutput, bool> predicate, string errorIfFalse)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new Pipeline<TInput, TOutput>(input =>
            _execute(input).Ensure(predicate, errorIfFalse));
    }

    /// <summary>
    ///     Adds a validation step with lazy error.
    /// </summary>
    public Pipeline<TInput, TOutput> ThenEnsure(
        Func<TOutput, bool> predicate,
        Func<TOutput, string> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        return new Pipeline<TInput, TOutput>(input =>
        {
            var result = _execute(input);
            if (result.IsFailure)
            {
                return result;
            }

            return predicate(result.Value)
                ? result
                : Result.Failure<TOutput>(errorFactory(result.Value));
        });
    }

    /// <summary>
    ///     Adds a side-effect step (tap) that doesn't affect the value.
    /// </summary>
    public Pipeline<TInput, TOutput> ThenTap(Action<TOutput> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new Pipeline<TInput, TOutput>(input =>
            _execute(input).Do(action));
    }

    /// <summary>
    ///     Adds a conditional transformation.
    /// </summary>
    public Pipeline<TInput, TOutput> ThenIf(
        Func<TOutput, bool> condition,
        Func<TOutput, TOutput> transform)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(transform);
        return new Pipeline<TInput, TOutput>(input =>
        {
            var result = _execute(input);
            if (result.IsFailure)
            {
                return result;
            }

            return condition(result.Value)
                ? Result.Success(transform(result.Value))
                : result;
        });
    }

    /// <summary>
    ///     Recovers from errors using a fallback value.
    /// </summary>
    public Pipeline<TInput, TOutput> Catch(TOutput fallbackValue) =>
        new(input =>
        {
            var result = _execute(input);
            return result.IsSuccess ? result : Result.Success(fallbackValue);
        });

    /// <summary>
    ///     Recovers from errors using a fallback function.
    /// </summary>
    public Pipeline<TInput, TOutput> Catch(Func<string, TOutput> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return new Pipeline<TInput, TOutput>(input =>
        {
            var result = _execute(input);
            return result.IsSuccess
                ? result
                : Result.Success(fallbackFactory(result.Error));
        });
    }

    /// <summary>
    ///     Recovers from errors using a Result-returning fallback.
    /// </summary>
    public Pipeline<TInput, TOutput> CatchBind(Func<string, Result<TOutput>> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return new Pipeline<TInput, TOutput>(input =>
        {
            var result = _execute(input);
            return result.IsSuccess ? result : fallbackFactory(result.Error);
        });
    }

    /// <summary>
    ///     Maps errors to a different format.
    /// </summary>
    public Pipeline<TInput, TOutput> MapError(Func<string, string> errorMapper)
    {
        ArgumentNullException.ThrowIfNull(errorMapper);
        return new Pipeline<TInput, TOutput>(input =>
        {
            var result = _execute(input);
            return result.IsSuccess
                ? result
                : Result.Failure<TOutput>(errorMapper(result.Error));
        });
    }

    /// <summary>
    ///     Converts the pipeline to a function.
    /// </summary>
    public Func<TInput, Result<TOutput>> ToFunc() => _execute;

    /// <summary>
    ///     Converts the pipeline to run with a default on failure.
    /// </summary>
    public Func<TInput, TOutput> ToFuncWithDefault(TOutput defaultValue) =>
        input => _execute(input).GetValueOrDefault(defaultValue);
}

/// <summary>
///     Async version of Pipeline.
/// </summary>
public sealed class PipelineAsync<TInput, TOutput>
{
    private readonly Func<TInput, Task<Result<TOutput>>> _execute;

    internal PipelineAsync(Func<TInput, Task<Result<TOutput>>> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>
    ///     Runs the async pipeline with the given input.
    /// </summary>
    public Task<Result<TOutput>> RunAsync(TInput input) => _execute(input);

    /// <summary>
    ///     Adds a sync transformation step.
    /// </summary>
    public PipelineAsync<TInput, TResult> Then<TResult>(Func<TOutput, TResult> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new PipelineAsync<TInput, TResult>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return result.Map(transform);
        });
    }

    /// <summary>
    ///     Adds an async transformation step.
    /// </summary>
    public PipelineAsync<TInput, TResult> ThenAsync<TResult>(Func<TOutput, Task<TResult>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new PipelineAsync<TInput, TResult>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return await result.MapAsync(transform).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Adds a Result-returning step.
    /// </summary>
    public PipelineAsync<TInput, TResult> ThenBind<TResult>(Func<TOutput, Result<TResult>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new PipelineAsync<TInput, TResult>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return result.Bind(transform);
        });
    }

    /// <summary>
    ///     Adds an async Result-returning step.
    /// </summary>
    public PipelineAsync<TInput, TResult> ThenBindAsync<TResult>(
        Func<TOutput, Task<Result<TResult>>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new PipelineAsync<TInput, TResult>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return await result.BindAsync(transform).ConfigureAwait(false);
        });
    }

    /// <summary>
    ///     Adds a validation step.
    /// </summary>
    public PipelineAsync<TInput, TOutput> ThenEnsure(Func<TOutput, bool> predicate, string errorIfFalse)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new PipelineAsync<TInput, TOutput>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return result.Ensure(predicate, errorIfFalse);
        });
    }

    /// <summary>
    ///     Adds an async validation step.
    /// </summary>
    public PipelineAsync<TInput, TOutput> ThenEnsureAsync(
        Func<TOutput, Task<bool>> predicate,
        string errorIfFalse)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new PipelineAsync<TInput, TOutput>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }

            return await predicate(result.Value).ConfigureAwait(false)
                ? result
                : Result.Failure<TOutput>(errorIfFalse);
        });
    }

    /// <summary>
    ///     Adds a side-effect step.
    /// </summary>
    public PipelineAsync<TInput, TOutput> ThenTap(Action<TOutput> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new PipelineAsync<TInput, TOutput>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return result.Do(action);
        });
    }

    /// <summary>
    ///     Adds an async side-effect step.
    /// </summary>
    public PipelineAsync<TInput, TOutput> ThenTapAsync(Func<TOutput, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new PipelineAsync<TInput, TOutput>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                await action(result.Value).ConfigureAwait(false);
            }

            return result;
        });
    }

    /// <summary>
    ///     Recovers from errors.
    /// </summary>
    public PipelineAsync<TInput, TOutput> Catch(TOutput fallbackValue) =>
        new(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return result.IsSuccess ? result : Result.Success(fallbackValue);
        });

    /// <summary>
    ///     Recovers from errors with an async fallback.
    /// </summary>
    public PipelineAsync<TInput, TOutput> CatchAsync(Func<string, Task<TOutput>> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return new PipelineAsync<TInput, TOutput>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return result.IsSuccess
                ? result
                : Result.Success(await fallbackFactory(result.Error).ConfigureAwait(false));
        });
    }

    /// <summary>
    ///     Maps errors.
    /// </summary>
    public PipelineAsync<TInput, TOutput> MapError(Func<string, string> errorMapper)
    {
        ArgumentNullException.ThrowIfNull(errorMapper);
        return new PipelineAsync<TInput, TOutput>(async input =>
        {
            var result = await _execute(input).ConfigureAwait(false);
            return result.IsSuccess
                ? result
                : Result.Failure<TOutput>(errorMapper(result.Error));
        });
    }

    /// <summary>
    ///     Converts to an async function.
    /// </summary>
    public Func<TInput, Task<Result<TOutput>>> ToFunc() => _execute;
}

/// <summary>
///     Static methods for creating pipelines.
/// </summary>
public static class Pipeline
{
    /// <summary>
    ///     Starts a new pipeline that passes input through unchanged.
    /// </summary>
    public static Pipeline<T, T> Start<T>() =>
        new(Result.Success);

    /// <summary>
    ///     Starts a new pipeline with a transformation.
    /// </summary>
    public static Pipeline<TInput, TOutput> Start<TInput, TOutput>(Func<TInput, TOutput> initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        return new Pipeline<TInput, TOutput>(input => Result.Success(initial(input)));
    }

    /// <summary>
    ///     Starts a new pipeline with a Result-returning transformation.
    /// </summary>
    public static Pipeline<TInput, TOutput> StartWith<TInput, TOutput>(
        Func<TInput, Result<TOutput>> initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        return new Pipeline<TInput, TOutput>(initial);
    }

    /// <summary>
    ///     Starts a new async pipeline.
    /// </summary>
    public static PipelineAsync<T, T> StartAsync<T>() =>
        new(input => Task.FromResult(Result.Success(input)));

    /// <summary>
    ///     Starts a new async pipeline with an async transformation.
    /// </summary>
    public static PipelineAsync<TInput, TOutput> StartAsync<TInput, TOutput>(
        Func<TInput, Task<TOutput>> initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        return new PipelineAsync<TInput, TOutput>(async input =>
            Result.Success(await initial(input).ConfigureAwait(false)));
    }

    /// <summary>
    ///     Starts a new async pipeline with a Result-returning async transformation.
    /// </summary>
    public static PipelineAsync<TInput, TOutput> StartWithAsync<TInput, TOutput>(
        Func<TInput, Task<Result<TOutput>>> initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        return new PipelineAsync<TInput, TOutput>(initial);
    }

    /// <summary>
    ///     Converts a sync pipeline to async.
    /// </summary>
    public static PipelineAsync<TInput, TOutput> ToAsync<TInput, TOutput>(
        this Pipeline<TInput, TOutput> pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return new PipelineAsync<TInput, TOutput>(input =>
            Task.FromResult(pipeline.Run(input)));
    }

    /// <summary>
    ///     Combines two pipelines in parallel, returning both results.
    /// </summary>
    public static Pipeline<TInput, (T1, T2)> Parallel<TInput, T1, T2>(
        Pipeline<TInput, T1> pipeline1,
        Pipeline<TInput, T2> pipeline2) =>
        new(input =>
            Result.Combine(pipeline1.Run(input), pipeline2.Run(input)));

    /// <summary>
    ///     Combines three pipelines in parallel.
    /// </summary>
    public static Pipeline<TInput, (T1, T2, T3)> Parallel<TInput, T1, T2, T3>(
        Pipeline<TInput, T1> pipeline1,
        Pipeline<TInput, T2> pipeline2,
        Pipeline<TInput, T3> pipeline3) =>
        new(input =>
            Result.Combine(
                pipeline1.Run(input),
                pipeline2.Run(input),
                pipeline3.Run(input)));

    /// <summary>
    ///     Runs the first successful pipeline.
    /// </summary>
    public static Pipeline<TInput, TOutput> Race<TInput, TOutput>(
        params Pipeline<TInput, TOutput>[] pipelines)
    {
        ArgumentNullException.ThrowIfNull(pipelines);
        return new Pipeline<TInput, TOutput>(input =>
        {
            var errors = new List<string>();
            foreach (var pipeline in pipelines)
            {
                var result = pipeline.Run(input);
                if (result.IsSuccess)
                {
                    return result;
                }

                errors.Add(result.Error);
            }

            return Result.Failure<TOutput>(
                $"All pipelines failed: {string.Join("; ", errors)}");
        });
    }

    /// <summary>
    ///     Creates a conditional pipeline.
    /// </summary>
    public static Pipeline<TInput, TOutput> When<TInput, TOutput>(
        Func<TInput, bool> condition,
        Pipeline<TInput, TOutput> whenTrue,
        Pipeline<TInput, TOutput> whenFalse)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);
        return new Pipeline<TInput, TOutput>(input =>
            condition(input) ? whenTrue.Run(input) : whenFalse.Run(input));
    }
}
