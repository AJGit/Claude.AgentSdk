using System.Collections.Concurrent;

namespace Claude.AgentSdk.Functional;

/// <summary>
///     Functional programming utilities: pipe, compose, curry, partial application, and memoization.
/// </summary>
/// <remarks>
///     <para>
///         These utilities enable point-free style programming and function composition in C#.
///     </para>
///     <para>
///         Example usage:
///         <code>
///     // Pipe: value flows left to right through functions
///     var result = 5
///         .Pipe(x => x * 2)
///         .Pipe(x => x + 1)
///         .Pipe(x => x.ToString());
///     // result = "11"
/// 
///     // Compose: combine functions right to left
///     var composed = F.Compose&lt;int, int, string&gt;(
///         x => x.ToString(),
///         x => x * 2
///     );
///     // composed(5) = "10"
/// 
///     // Partial application
///     Func&lt;int, int, int&gt; add = (a, b) => a + b;
///     var add5 = add.Partial(5);
///     // add5(3) = 8
/// 
///     // Memoization
///     var expensiveFunc = F.Memoize&lt;int, int&gt;(x => {
///         Thread.Sleep(1000);
///         return x * 2;
///     });
///     // First call: slow, subsequent calls with same arg: instant
///     </code>
///     </para>
/// </remarks>
public static class FunctionalExtensions
{
    /// <param name="value">The input value.</param>
    /// <typeparam name="T">The input type.</typeparam>
    extension<T>(T value)
    {
        /// <summary>
        ///     Pipes a value through a function (forward application).
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="func">The function to apply.</param>
        /// <returns>The result of applying the function to the value.</returns>
        public TResult Pipe<TResult>(Func<T, TResult> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return func(value);
        }

        /// <summary>
        ///     Pipes a value through a function and returns the original value (tap).
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <returns>The original value.</returns>
        public T Tap(Action<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action(value);
            return value;
        }

        /// <summary>
        ///     Pipes a value through an async function.
        /// </summary>
        public async Task<TResult> PipeAsync<TResult>(Func<T, Task<TResult>> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return await func(value).ConfigureAwait(false);
        }
    }

    extension<T>(Task<T> task)
    {
        /// <summary>
        ///     Pipes a Task result through a function.
        /// </summary>
        public async Task<TResult> PipeAsync<TResult>(Func<T, TResult> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            var value = await task.ConfigureAwait(false);
            return func(value);
        }

        /// <summary>
        ///     Pipes a Task result through an async function.
        /// </summary>
        public async Task<TResult> PipeAsync<TResult>(Func<T, Task<TResult>> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            var value = await task.ConfigureAwait(false);
            return await func(value).ConfigureAwait(false);
        }
    }

    extension<T>(T value)
    {
        /// <summary>
        ///     Conditionally applies a transformation.
        /// </summary>
        public T PipeIf(bool condition, Func<T, T> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return condition ? func(value) : value;
        }

        /// <summary>
        ///     Conditionally applies a transformation based on the value.
        /// </summary>
        public T PipeIf(Func<T, bool> predicate, Func<T, T> func)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            ArgumentNullException.ThrowIfNull(func);
            return predicate(value) ? func(value) : value;
        }
    }

    /// <summary>
    ///     Applies a wrapped function to a wrapped value.
    /// </summary>
    public static Option<TResult> Apply<T, TResult>(
        this Option<Func<T, TResult>> funcOption,
        Option<T> valueOption) =>
        funcOption.Bind(valueOption.Map);

    /// <summary>
    ///     Applies a wrapped function to a wrapped value.
    /// </summary>
    public static Result<TResult, TError> Apply<T, TResult, TError>(
        this Result<Func<T, TResult>, TError> funcResult,
        Result<T, TError> valueResult) =>
        funcResult.Bind(valueResult.Map);

    /// <summary>
    ///     Returns the input value unchanged.
    /// </summary>
    public static T Identity<T>(T value) => value;

    /// <summary>
    ///     Returns a function that always returns the specified value.
    /// </summary>
    public static Func<T, TResult> Const<T, TResult>(TResult value) => _ => value;

    /// <summary>
    ///     Returns an action that does nothing.
    /// </summary>
    public static Action<T> Ignore<T>() => _ => { };

    /// <summary>
    ///     Returns the value if not null, otherwise executes the fallback.
    /// </summary>
    public static T OrElse<T>(this T? value, Func<T> fallback) where T : class
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return value ?? fallback();
    }

    /// <summary>
    ///     Returns the value if it has a value, otherwise executes the fallback.
    /// </summary>
    public static T OrElse<T>(this T? value, Func<T> fallback) where T : struct
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return value ?? fallback();
    }

    /// <summary>
    ///     Safely casts to a type, returning Option.
    /// </summary>
    public static Option<T> SafeCast<T>(this object? obj) where T : class =>
        obj is T result ? Option.Some(result) : Option.NoneOf<T>();

    /// <summary>
    ///     Safely accesses a potentially null value.
    /// </summary>
    public static Option<TResult> SafeAccess<T, TResult>(this T? obj, Func<T, TResult> accessor)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(accessor);
        return obj is not null ? Option.Some(accessor(obj)) : Option.NoneOf<TResult>();
    }
}

/// <summary>
///     Static functional helpers for composition, currying, and memoization.
/// </summary>
public static class F
{
    /// <summary>
    ///     Flips the argument order of a two-argument function.
    /// </summary>
    public static Func<T2, T1, TResult> Flip<T1, T2, TResult>(this Func<T1, T2, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return (arg2, arg1) => func(arg1, arg2);
    }

    /// <summary>
    ///     Composes two functions (right to left): g after f.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TIntermediate">The intermediate type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="g">The second function to apply.</param>
    /// <param name="f">The first function to apply.</param>
    /// <returns>A composed function.</returns>
    public static Func<T, TResult> Compose<T, TIntermediate, TResult>(
        Func<TIntermediate, TResult> g,
        Func<T, TIntermediate> f)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(f);
        return x => g(f(x));
    }

    /// <summary>
    ///     Composes three functions.
    /// </summary>
    public static Func<T, TResult> Compose<T, T1, T2, TResult>(
        Func<T2, TResult> h,
        Func<T1, T2> g,
        Func<T, T1> f)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(f);
        return x => h(g(f(x)));
    }

    extension<T, TIntermediate>(Func<T, TIntermediate> f)
    {
        /// <summary>
        ///     Composes two functions (left to right): f then g.
        /// </summary>
        public Func<T, TResult> Then<TResult>(Func<TIntermediate, TResult> g)
        {
            ArgumentNullException.ThrowIfNull(f);
            ArgumentNullException.ThrowIfNull(g);
            return x => g(f(x));
        }

        /// <summary>
        ///     Chains an action after a function.
        /// </summary>
        public Func<T, TIntermediate> ThenDo(Action<TIntermediate> action)
        {
            ArgumentNullException.ThrowIfNull(f);
            ArgumentNullException.ThrowIfNull(action);
            return x =>
            {
                var result = f(x);
                action(result);
                return result;
            };
        }
    }

    /// <summary>
    ///     Partially applies the first argument of a two-argument function.
    /// </summary>
    public static Func<T2, TResult> Partial<T1, T2, TResult>(
        this Func<T1, T2, TResult> func,
        T1 arg1)
    {
        ArgumentNullException.ThrowIfNull(func);
        return arg2 => func(arg1, arg2);
    }

    extension<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> func)
    {
        /// <summary>
        ///     Partially applies the first argument of a three-argument function.
        /// </summary>
        public Func<T2, T3, TResult> Partial(T1 arg1)
        {
            ArgumentNullException.ThrowIfNull(func);
            return (arg2, arg3) => func(arg1, arg2, arg3);
        }

        /// <summary>
        ///     Partially applies the first two arguments of a three-argument function.
        /// </summary>
        public Func<T3, TResult> Partial(T1 arg1,
            T2 arg2)
        {
            ArgumentNullException.ThrowIfNull(func);
            return arg3 => func(arg1, arg2, arg3);
        }
    }

    /// <summary>
    ///     Partially applies the first argument of a four-argument function.
    /// </summary>
    public static Func<T2, T3, T4, TResult> Partial<T1, T2, T3, T4, TResult>(
        this Func<T1, T2, T3, T4, TResult> func,
        T1 arg1)
    {
        ArgumentNullException.ThrowIfNull(func);
        return (arg2, arg3, arg4) => func(arg1, arg2, arg3, arg4);
    }

    /// <summary>
    ///     Curries a two-argument function.
    /// </summary>
    public static Func<T1, Func<T2, TResult>> Curry<T1, T2, TResult>(
        this Func<T1, T2, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return arg1 => arg2 => func(arg1, arg2);
    }

    /// <summary>
    ///     Curries a three-argument function.
    /// </summary>
    public static Func<T1, Func<T2, Func<T3, TResult>>> Curry<T1, T2, T3, TResult>(
        this Func<T1, T2, T3, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return arg1 => arg2 => arg3 => func(arg1, arg2, arg3);
    }

    /// <summary>
    ///     Curries a four-argument function.
    /// </summary>
    public static Func<T1, Func<T2, Func<T3, Func<T4, TResult>>>> Curry<T1, T2, T3, T4, TResult>(
        this Func<T1, T2, T3, T4, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return arg1 => arg2 => arg3 => arg4 => func(arg1, arg2, arg3, arg4);
    }

    /// <summary>
    ///     Uncurries a curried function back to a two-argument function.
    /// </summary>
    public static Func<T1, T2, TResult> Uncurry<T1, T2, TResult>(
        this Func<T1, Func<T2, TResult>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return (arg1, arg2) => func(arg1)(arg2);
    }

    /// <summary>
    ///     Uncurries a curried function back to a three-argument function.
    /// </summary>
    public static Func<T1, T2, T3, TResult> Uncurry<T1, T2, T3, TResult>(
        this Func<T1, Func<T2, Func<T3, TResult>>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return (arg1, arg2, arg3) => func(arg1)(arg2)(arg3);
    }

    /// <summary>
    ///     Creates a memoized version of a function (caches results).
    /// </summary>
    /// <typeparam name="TArg">The argument type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="func">The function to memoize.</param>
    /// <returns>A memoized version of the function.</returns>
    public static Func<TArg, TResult> Memoize<TArg, TResult>(Func<TArg, TResult> func)
        where TArg : notnull
    {
        ArgumentNullException.ThrowIfNull(func);
        var cache = new ConcurrentDictionary<TArg, TResult>();
        return arg => cache.GetOrAdd(arg, func);
    }

    /// <summary>
    ///     Creates a memoized version of a function with a custom cache.
    /// </summary>
    public static Func<TArg, TResult> Memoize<TArg, TResult>(
        Func<TArg, TResult> func,
        IDictionary<TArg, TResult> cache)
        where TArg : notnull
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(cache);
        return arg =>
        {
            if (cache.TryGetValue(arg, out var result))
            {
                return result;
            }

            result = func(arg);
            cache[arg] = result;
            return result;
        };
    }

    /// <summary>
    ///     Creates a memoized version of an async function.
    /// </summary>
    public static Func<TArg, Task<TResult>> MemoizeAsync<TArg, TResult>(
        Func<TArg, Task<TResult>> func)
        where TArg : notnull
    {
        ArgumentNullException.ThrowIfNull(func);
        var cache = new ConcurrentDictionary<TArg, Task<TResult>>();
        return arg => cache.GetOrAdd(arg, func);
    }

    /// <summary>
    ///     Creates a memoized version of a two-argument function.
    /// </summary>
    public static Func<T1, T2, TResult> Memoize<T1, T2, TResult>(Func<T1, T2, TResult> func)
        where T1 : notnull
        where T2 : notnull
    {
        ArgumentNullException.ThrowIfNull(func);
        var cache = new ConcurrentDictionary<(T1, T2), TResult>();
        return (arg1, arg2) => cache.GetOrAdd((arg1, arg2), key => func(key.Item1, key.Item2));
    }

    /// <summary>
    ///     Negates a predicate.
    /// </summary>
    public static Func<T, bool> Not<T>(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return x => !predicate(x);
    }

    /// <summary>
    ///     Combines predicates with AND.
    /// </summary>
    public static Func<T, bool> And<T>(Func<T, bool> p1, Func<T, bool> p2)
    {
        ArgumentNullException.ThrowIfNull(p1);
        ArgumentNullException.ThrowIfNull(p2);
        return x => p1(x) && p2(x);
    }

    /// <summary>
    ///     Combines predicates with OR.
    /// </summary>
    public static Func<T, bool> Or<T>(Func<T, bool> p1, Func<T, bool> p2)
    {
        ArgumentNullException.ThrowIfNull(p1);
        ArgumentNullException.ThrowIfNull(p2);
        return x => p1(x) || p2(x);
    }

    /// <summary>
    ///     Combines multiple predicates with AND.
    /// </summary>
    public static Func<T, bool> All<T>(params Func<T, bool>[] predicates)
    {
        ArgumentNullException.ThrowIfNull(predicates);
        return x => predicates.All(p => p(x));
    }

    /// <summary>
    ///     Combines multiple predicates with OR.
    /// </summary>
    public static Func<T, bool> Any<T>(params Func<T, bool>[] predicates)
    {
        ArgumentNullException.ThrowIfNull(predicates);
        return x => predicates.Any(p => p(x));
    }

    /// <summary>
    ///     Wraps a function call in a try-catch, returning a Result.
    /// </summary>
    public static Func<T, Result<TResult>> Try<T, TResult>(Func<T, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return arg =>
        {
            try
            {
                return Result.Success(func(arg));
            }
            catch (Exception ex)
            {
                return Result.Failure<TResult>(ex.Message);
            }
        };
    }

    /// <summary>
    ///     Wraps a function call in a try-catch with a custom error mapper.
    /// </summary>
    public static Func<T, Result<TResult, TError>> Try<T, TResult, TError>(
        Func<T, TResult> func,
        Func<Exception, TError> errorMapper)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(errorMapper);
        return arg =>
        {
            try
            {
                return Result<TResult, TError>.Success(func(arg));
            }
            catch (Exception ex)
            {
                return Result<TResult, TError>.Failure(errorMapper(ex));
            }
        };
    }

    /// <summary>
    ///     Executes a function with a disposable resource, ensuring disposal.
    /// </summary>
    public static TResult Using<TResource, TResult>(
        Func<TResource> resourceFactory,
        Func<TResource, TResult> body)
        where TResource : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resourceFactory);
        ArgumentNullException.ThrowIfNull(body);
        using var resource = resourceFactory();
        return body(resource);
    }

    /// <summary>
    ///     Executes an async function with a disposable resource, ensuring disposal.
    /// </summary>
    public static async Task<TResult> UsingAsync<TResource, TResult>(
        Func<TResource> resourceFactory,
        Func<TResource, Task<TResult>> body)
        where TResource : IDisposable
    {
        ArgumentNullException.ThrowIfNull(resourceFactory);
        ArgumentNullException.ThrowIfNull(body);
        using var resource = resourceFactory();
        return await body(resource).ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes an async function with an async disposable resource.
    /// </summary>
    public static async Task<TResult> UsingAsyncDisposable<TResource, TResult>(
        Func<TResource> resourceFactory,
        Func<TResource, Task<TResult>> body)
        where TResource : IAsyncDisposable
    {
        ArgumentNullException.ThrowIfNull(resourceFactory);
        ArgumentNullException.ThrowIfNull(body);
        await using var resource = resourceFactory();
        return await body(resource).ConfigureAwait(false);
    }
}
