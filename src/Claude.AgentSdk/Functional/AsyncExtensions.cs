namespace Claude.AgentSdk.Functional;

/// <summary>
///     Async extension methods for functional types.
///     Enables fluent chaining with async operations on Option, Result, and Either.
/// </summary>
/// <remarks>
///     <para>
///         These extensions allow seamless composition of async and sync operations:
///         <code>
///     var result = await GetUserAsync(id)           // Task&lt;Option&lt;User&gt;&gt;
///         .MapAsync(u => u.Email)                    // Task&lt;Option&lt;string&gt;&gt;
///         .BindAsync(ValidateEmailAsync)             // Task&lt;Option&lt;string&gt;&gt;
///         .GetValueOrDefaultAsync("unknown@email"); // Task&lt;string&gt;
///     </code>
///     </para>
/// </remarks>
public static class AsyncExtensions
{
    extension<T>(Task<Option<T>> optionTask)
    {
        /// <summary>
        ///     Maps the value inside an async Option.
        /// </summary>
        public async Task<Option<TResult>> MapAsync<TResult>(Func<T, TResult> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            var option = await optionTask.ConfigureAwait(false);
            return option.Map(mapper);
        }

        /// <summary>
        ///     Maps the value inside an async Option with an async mapper.
        /// </summary>
        public async Task<Option<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            var option = await optionTask.ConfigureAwait(false);
            return await option.MapAsync(mapper).ConfigureAwait(false);
        }

        /// <summary>
        ///     Binds an async Option with a sync binder.
        /// </summary>
        public async Task<Option<TResult>> BindAsync<TResult>(Func<T, Option<TResult>> binder)
        {
            ArgumentNullException.ThrowIfNull(binder);
            var option = await optionTask.ConfigureAwait(false);
            return option.Bind(binder);
        }

        /// <summary>
        ///     Binds an async Option with an async binder.
        /// </summary>
        public async Task<Option<TResult>> BindAsync<TResult>(Func<T, Task<Option<TResult>>> binder)
        {
            ArgumentNullException.ThrowIfNull(binder);
            var option = await optionTask.ConfigureAwait(false);
            return await option.BindAsync(binder).ConfigureAwait(false);
        }

        /// <summary>
        ///     Filters an async Option with a predicate.
        /// </summary>
        public async Task<Option<T>> WhereAsync(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var option = await optionTask.ConfigureAwait(false);
            return option.Where(predicate);
        }

        /// <summary>
        ///     Filters an async Option with an async predicate.
        /// </summary>
        public async Task<Option<T>> WhereAsync(Func<T, Task<bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var option = await optionTask.ConfigureAwait(false);
            if (option.IsNone)
            {
                return Option<T>.None;
            }

            return await predicate(option.Value).ConfigureAwait(false) ? option : Option<T>.None;
        }

        /// <summary>
        ///     Pattern matches on an async Option.
        /// </summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> some,
            Func<TResult> none)
        {
            ArgumentNullException.ThrowIfNull(some);
            ArgumentNullException.ThrowIfNull(none);
            var option = await optionTask.ConfigureAwait(false);
            return option.Match(some, none);
        }

        /// <summary>
        ///     Pattern matches on an async Option with async handlers.
        /// </summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> some,
            Func<Task<TResult>> none)
        {
            ArgumentNullException.ThrowIfNull(some);
            ArgumentNullException.ThrowIfNull(none);
            var option = await optionTask.ConfigureAwait(false);
            return option.IsSome
                ? await some(option.Value).ConfigureAwait(false)
                : await none().ConfigureAwait(false);
        }

        /// <summary>
        ///     Gets the value or default from an async Option.
        /// </summary>
        public async Task<T> GetValueOrDefaultAsync(T defaultValue = default!)
        {
            var option = await optionTask.ConfigureAwait(false);
            return option.GetValueOrDefault(defaultValue);
        }

        /// <summary>
        ///     Gets the value or computes a default from an async Option.
        /// </summary>
        public async Task<T> GetValueOrElseAsync(Func<T> defaultFactory)
        {
            ArgumentNullException.ThrowIfNull(defaultFactory);
            var option = await optionTask.ConfigureAwait(false);
            return option.GetValueOrElse(defaultFactory);
        }

        /// <summary>
        ///     Gets the value or computes a default asynchronously from an async Option.
        /// </summary>
        public async Task<T> GetValueOrElseAsync(Func<Task<T>> defaultFactory)
        {
            ArgumentNullException.ThrowIfNull(defaultFactory);
            var option = await optionTask.ConfigureAwait(false);
            return option.IsSome ? option.Value : await defaultFactory().ConfigureAwait(false);
        }

        /// <summary>
        ///     Executes an action on an async Option if it has a value.
        /// </summary>
        public async Task<Option<T>> DoAsync(Action<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var option = await optionTask.ConfigureAwait(false);
            return option.Do(action);
        }

        /// <summary>
        ///     Returns this option or an alternative if none.
        /// </summary>
        public async Task<Option<T>> OrAsync(Option<T> alternative)
        {
            var option = await optionTask.ConfigureAwait(false);
            return option.Or(alternative);
        }

        /// <summary>
        ///     Returns this option or computes an alternative if none.
        /// </summary>
        public async Task<Option<T>> OrElseAsync(Func<Task<Option<T>>> alternativeFactory)
        {
            ArgumentNullException.ThrowIfNull(alternativeFactory);
            var option = await optionTask.ConfigureAwait(false);
            return option.IsSome ? option : await alternativeFactory().ConfigureAwait(false);
        }

        /// <summary>
        ///     Converts an async Option to a Result.
        /// </summary>
        public async Task<Result<T, TError>> ToResultAsync<TError>(TError error)
        {
            var option = await optionTask.ConfigureAwait(false);
            return option.ToResult(error);
        }
    }

    /// <summary>
    ///     Maps the success value inside an async Result.
    /// </summary>
    public static async Task<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Task<Result<T, TError>> resultTask,
        Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var result = await resultTask.ConfigureAwait(false);
        return result.Map(mapper);
    }

    /// <summary>
    ///     Maps the success value inside an async Result with an async mapper.
    /// </summary>
    public static async Task<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Task<Result<T, TError>> resultTask,
        Func<T, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var result = await resultTask.ConfigureAwait(false);
        return await result.MapAsync(mapper).ConfigureAwait(false);
    }

    /// <summary>
    ///     Binds an async Result with a sync binder.
    /// </summary>
    public static async Task<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Task<Result<T, TError>> resultTask,
        Func<T, Result<TResult, TError>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        var result = await resultTask.ConfigureAwait(false);
        return result.Bind(binder);
    }

    /// <summary>
    ///     Binds an async Result with an async binder.
    /// </summary>
    public static async Task<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Task<Result<T, TError>> resultTask,
        Func<T, Task<Result<TResult, TError>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        var result = await resultTask.ConfigureAwait(false);
        return await result.BindAsync(binder).ConfigureAwait(false);
    }

    extension<T, TError>(Task<Result<T, TError>> resultTask)
    {
        /// <summary>
        ///     Pattern matches on an async Result.
        /// </summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> success,
            Func<TError, TResult> failure)
        {
            ArgumentNullException.ThrowIfNull(success);
            ArgumentNullException.ThrowIfNull(failure);
            var result = await resultTask.ConfigureAwait(false);
            return result.Match(success, failure);
        }

        /// <summary>
        ///     Pattern matches on an async Result with async handlers.
        /// </summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> success,
            Func<TError, Task<TResult>> failure)
        {
            ArgumentNullException.ThrowIfNull(success);
            ArgumentNullException.ThrowIfNull(failure);
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess
                ? await success(result.Value).ConfigureAwait(false)
                : await failure(result.Error).ConfigureAwait(false);
        }

        /// <summary>
        ///     Gets the value or default from an async Result.
        /// </summary>
        public async Task<T> GetValueOrDefaultAsync(T defaultValue = default!)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.GetValueOrDefault(defaultValue);
        }

        /// <summary>
        ///     Gets the value or computes a default from an async Result.
        /// </summary>
        public async Task<T> GetValueOrElseAsync(Func<TError, T> defaultFactory)
        {
            ArgumentNullException.ThrowIfNull(defaultFactory);
            var result = await resultTask.ConfigureAwait(false);
            return result.GetValueOrElse(defaultFactory);
        }

        /// <summary>
        ///     Executes an action on success.
        /// </summary>
        public async Task<Result<T, TError>> DoAsync(Action<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var result = await resultTask.ConfigureAwait(false);
            return result.Do(action);
        }

        /// <summary>
        ///     Executes an action on failure.
        /// </summary>
        public async Task<Result<T, TError>> DoOnErrorAsync(Action<TError> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var result = await resultTask.ConfigureAwait(false);
            return result.DoOnError(action);
        }

        /// <summary>
        ///     Ensures a condition is met.
        /// </summary>
        public async Task<Result<T, TError>> EnsureAsync(Func<T, bool> predicate,
            TError error)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var result = await resultTask.ConfigureAwait(false);
            return result.Ensure(predicate, error);
        }

        /// <summary>
        ///     Ensures a condition is met with async predicate.
        /// </summary>
        public async Task<Result<T, TError>> EnsureAsync(Func<T, Task<bool>> predicate,
            TError error)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var result = await resultTask.ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }

            return await predicate(result.Value).ConfigureAwait(false)
                ? result
                : Result<T, TError>.Failure(error);
        }

        /// <summary>
        ///     Converts an async Result to an Option.
        /// </summary>
        public async Task<Option<T>> ToOptionAsync()
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.ToOption();
        }
    }

    extension<T>(Task<Result<T>> resultTask)
    {
        /// <summary>
        ///     Maps the success value inside an async Result.
        /// </summary>
        public async Task<Result<TResult>> MapAsync<TResult>(Func<T, TResult> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            var result = await resultTask.ConfigureAwait(false);
            return result.Map(mapper);
        }

        /// <summary>
        ///     Maps the success value inside an async Result with an async mapper.
        /// </summary>
        public async Task<Result<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            var result = await resultTask.ConfigureAwait(false);
            return await result.MapAsync(mapper).ConfigureAwait(false);
        }

        /// <summary>
        ///     Binds an async Result with a sync binder.
        /// </summary>
        public async Task<Result<TResult>> BindAsync<TResult>(Func<T, Result<TResult>> binder)
        {
            ArgumentNullException.ThrowIfNull(binder);
            var result = await resultTask.ConfigureAwait(false);
            return result.Bind(binder);
        }

        /// <summary>
        ///     Binds an async Result with an async binder.
        /// </summary>
        public async Task<Result<TResult>> BindAsync<TResult>(Func<T, Task<Result<TResult>>> binder)
        {
            ArgumentNullException.ThrowIfNull(binder);
            var result = await resultTask.ConfigureAwait(false);
            return await result.BindAsync(binder).ConfigureAwait(false);
        }

        /// <summary>
        ///     Pattern matches on an async Result.
        /// </summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> success,
            Func<string, TResult> failure)
        {
            ArgumentNullException.ThrowIfNull(success);
            ArgumentNullException.ThrowIfNull(failure);
            var result = await resultTask.ConfigureAwait(false);
            return result.Match(success, failure);
        }

        /// <summary>
        ///     Gets the value or default from an async Result.
        /// </summary>
        public async Task<T> GetValueOrDefaultAsync(T defaultValue = default!)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.GetValueOrDefault(defaultValue);
        }

        /// <summary>
        ///     Ensures a condition is met.
        /// </summary>
        public async Task<Result<T>> EnsureAsync(Func<T, bool> predicate,
            string error)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var result = await resultTask.ConfigureAwait(false);
            return result.Ensure(predicate, error);
        }
    }

    extension<TLeft, TRight>(Task<Either<TLeft, TRight>> eitherTask)
    {
        /// <summary>
        ///     Maps the Right value inside an async Either.
        /// </summary>
        public async Task<Either<TLeft, TResult>> MapAsync<TResult>(Func<TRight, TResult> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            var either = await eitherTask.ConfigureAwait(false);
            return either.Map(mapper);
        }

        /// <summary>
        ///     Binds an async Either with a sync binder.
        /// </summary>
        public async Task<Either<TLeft, TResult>> BindAsync<TResult>(Func<TRight, Either<TLeft, TResult>> binder)
        {
            ArgumentNullException.ThrowIfNull(binder);
            var either = await eitherTask.ConfigureAwait(false);
            return either.Bind(binder);
        }

        /// <summary>
        ///     Binds an async Either with an async binder.
        /// </summary>
        public async Task<Either<TLeft, TResult>> BindAsync<TResult>(Func<TRight, Task<Either<TLeft, TResult>>> binder)
        {
            ArgumentNullException.ThrowIfNull(binder);
            var either = await eitherTask.ConfigureAwait(false);
            return await either.BindAsync(binder).ConfigureAwait(false);
        }

        /// <summary>
        ///     Pattern matches on an async Either.
        /// </summary>
        public async Task<TResult> MatchAsync<TResult>(Func<TLeft, TResult> left,
            Func<TRight, TResult> right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);
            var either = await eitherTask.ConfigureAwait(false);
            return either.Match(left, right);
        }
    }

    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        ///     Traverses a collection with an async Option-returning function.
        /// </summary>
        public async Task<Option<IReadOnlyList<TResult>>> TraverseAsync<TResult>(Func<T, Task<Option<TResult>>> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            var results = new List<TResult>();
            foreach (var item in source)
            {
                var result = await mapper(item).ConfigureAwait(false);
                if (result.IsNone)
                {
                    return Option.NoneOf<IReadOnlyList<TResult>>();
                }

                results.Add(result.Value);
            }

            return Option.Some<IReadOnlyList<TResult>>(results);
        }

        /// <summary>
        ///     Traverses a collection with an async Result-returning function.
        /// </summary>
        public async Task<Result<IReadOnlyList<TResult>>> TraverseAsync<TResult>(Func<T, Task<Result<TResult>>> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            var results = new List<TResult>();
            foreach (var item in source)
            {
                var result = await mapper(item).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<TResult>>(result.Error);
                }

                results.Add(result.Value);
            }

            return Result.Success<IReadOnlyList<TResult>>(results);
        }
    }

    /// <summary>
    ///     Sequences a collection of async Options.
    /// </summary>
    public static async Task<Option<IReadOnlyList<T>>> SequenceAsync<T>(
        this IEnumerable<Task<Option<T>>> tasks)
    {
        var results = new List<T>();
        foreach (var task in tasks)
        {
            var result = await task.ConfigureAwait(false);
            if (result.IsNone)
            {
                return Option.NoneOf<IReadOnlyList<T>>();
            }

            results.Add(result.Value);
        }

        return Option.Some<IReadOnlyList<T>>(results);
    }

    /// <summary>
    ///     Sequences a collection of async Results.
    /// </summary>
    public static async Task<Result<IReadOnlyList<T>>> SequenceAsync<T>(
        this IEnumerable<Task<Result<T>>> tasks)
    {
        var results = new List<T>();
        foreach (var task in tasks)
        {
            var result = await task.ConfigureAwait(false);
            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyList<T>>(result.Error);
            }

            results.Add(result.Value);
        }

        return Result.Success<IReadOnlyList<T>>(results);
    }
}
