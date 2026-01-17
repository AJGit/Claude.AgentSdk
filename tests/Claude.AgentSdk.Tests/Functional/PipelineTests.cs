using Claude.AgentSdk.Functional;

namespace Claude.AgentSdk.Tests.Functional;

/// <summary>
///     Tests for the Pipeline and PipelineAsync classes.
/// </summary>
[UnitTest]
public class PipelineTests
{
    #region Pipeline.Start Tests

    [Fact]
    public void Start_WithType_CreatesPassthroughPipeline()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act
        var result = pipeline.Run(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Start_WithTransformation_AppliesTransformation()
    {
        // Arrange
        var pipeline = Pipeline.Start<int, string>(x => x.ToString());

        // Act
        var result = pipeline.Run(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void Start_WithNullTransformation_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Pipeline.Start<int, string>(null!));
    }

    [Fact]
    public void StartWith_WithResultTransformation_AppliesTransformation()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, string>(x =>
            x > 0 ? Result.Success(x.ToString()) : Result.Failure<string>("Must be positive"));

        // Act
        var successResult = pipeline.Run(42);
        var failureResult = pipeline.Run(-1);

        // Assert
        Assert.True(successResult.IsSuccess);
        Assert.Equal("42", successResult.Value);
        Assert.True(failureResult.IsFailure);
        Assert.Equal("Must be positive", failureResult.Error);
    }

    [Fact]
    public void StartWith_WithNullTransformation_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Pipeline.StartWith<int, string>(null!));
    }

    #endregion

    #region Pipeline.Run Tests

    [Fact]
    public void Run_ExecutesPipeline()
    {
        // Arrange
        var pipeline = Pipeline.Start<string>()
            .Then(s => s.ToUpper())
            .Then(s => s.Length);

        // Act
        var result = pipeline.Run("hello");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    #endregion

    #region Pipeline.Then Tests

    [Fact]
    public void Then_TransformsValue()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .Then(x => x * 2)
            .Then(x => x + 10);

        // Act
        var result = pipeline.Run(5);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Then_WithNullTransform_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.Then<string>(null!));
    }

    [Fact]
    public void Then_PropagatesFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, string>(x =>
                x > 0 ? Result.Success(x.ToString()) : Result.Failure<string>("Negative"))
            .Then(s => s.Length);

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Negative", result.Error);
    }

    #endregion

    #region Pipeline.ThenBind Tests

    [Fact]
    public void ThenBind_WithSuccess_TransformsValue()
    {
        // Arrange
        var pipeline = Pipeline.Start<string>()
            .ThenBind(s => int.TryParse(s, out var n)
                ? Result.Success(n)
                : Result.Failure<int>("Invalid number"));

        // Act
        var result = pipeline.Run("42");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ThenBind_WithFailure_ReturnsFailure()
    {
        // Arrange
        var pipeline = Pipeline.Start<string>()
            .ThenBind(s => int.TryParse(s, out var n)
                ? Result.Success(n)
                : Result.Failure<int>("Invalid number"));

        // Act
        var result = pipeline.Run("not a number");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Invalid number", result.Error);
    }

    [Fact]
    public void ThenBind_WithNullTransform_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenBind<string>(null!));
    }

    #endregion

    #region Pipeline.ThenEnsure Tests

    [Fact]
    public void ThenEnsure_WithSatisfiedCondition_ContinuesPipeline()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .ThenEnsure(x => x > 0, "Must be positive");

        // Act
        var result = pipeline.Run(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ThenEnsure_WithUnsatisfiedCondition_ReturnsFailure()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .ThenEnsure(x => x > 0, "Must be positive");

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Must be positive", result.Error);
    }

    [Fact]
    public void ThenEnsure_WithLazyError_ReturnsComputedError()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .ThenEnsure(x => x > 0, x => $"Value {x} is not positive");

        // Act
        var result = pipeline.Run(-5);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Value -5 is not positive", result.Error);
    }

    [Fact]
    public void ThenEnsure_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenEnsure(null!, "error"));
    }

    [Fact]
    public void ThenEnsure_PropagatesExistingFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Initial failure"))
            .ThenEnsure(x => x < 100, "Too large");

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Initial failure", result.Error);
    }

    #endregion

    #region Pipeline.ThenTap Tests

    [Fact]
    public void ThenTap_ExecutesSideEffect()
    {
        // Arrange
        var sideEffectValue = 0;
        var pipeline = Pipeline.Start<int>()
            .ThenTap(x => sideEffectValue = x);

        // Act
        var result = pipeline.Run(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(42, sideEffectValue);
    }

    [Fact]
    public void ThenTap_WithFailure_DoesNotExecuteSideEffect()
    {
        // Arrange
        var sideEffectExecuted = false;
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Negative"))
            .ThenTap(_ => sideEffectExecuted = true);

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.False(sideEffectExecuted);
    }

    [Fact]
    public void ThenTap_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenTap(null!));
    }

    #endregion

    #region Pipeline.ThenIf Tests

    [Fact]
    public void ThenIf_WithTrueCondition_AppliesTransform()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .ThenIf(x => x > 10, x => x * 2);

        // Act
        var result = pipeline.Run(20);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(40, result.Value);
    }

    [Fact]
    public void ThenIf_WithFalseCondition_SkipsTransform()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .ThenIf(x => x > 10, x => x * 2);

        // Act
        var result = pipeline.Run(5);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void ThenIf_PropagatesFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Negative"))
            .ThenIf(x => x > 10, x => x * 2);

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Negative", result.Error);
    }

    #endregion

    #region Pipeline.Catch Tests

    [Fact]
    public void Catch_WithFallbackValue_RecoversFromFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Negative"))
            .Catch(0);

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Catch_WithSuccess_ReturnsOriginalValue()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .Catch(0);

        // Act
        var result = pipeline.Run(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Catch_WithFallbackFactory_RecoversFromFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, string>(x =>
                x > 0 ? Result.Success(x.ToString()) : Result.Failure<string>("Negative"))
            .Catch(error => $"Recovered from: {error}");

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Recovered from: Negative", result.Value);
    }

    [Fact]
    public void Catch_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.Catch((Func<string, int>)null!));
    }

    #endregion

    #region Pipeline.CatchBind Tests

    [Fact]
    public void CatchBind_WithRecovery_RecoversFromFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Negative"))
            .CatchBind(_ => Result.Success(0));

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void CatchBind_WithFailedRecovery_PropagatesNewFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Original"))
            .CatchBind(_ => Result.Failure<int>("Recovery also failed"));

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Recovery also failed", result.Error);
    }

    [Fact]
    public void CatchBind_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.CatchBind(null!));
    }

    #endregion

    #region Pipeline.MapError Tests

    [Fact]
    public void MapError_WithFailure_MapsErrorMessage()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Negative"))
            .MapError(e => $"Wrapped: {e}");

        // Act
        var result = pipeline.Run(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Wrapped: Negative", result.Error);
    }

    [Fact]
    public void MapError_WithSuccess_DoesNothing()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .MapError(e => $"Wrapped: {e}");

        // Act
        var result = pipeline.Run(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void MapError_WithNullMapper_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.MapError(null!));
    }

    #endregion

    #region Pipeline.ToFunc Tests

    [Fact]
    public void ToFunc_ReturnsExecutableFunction()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>()
            .Then(x => x * 2);
        var func = pipeline.ToFunc();

        // Act
        var result = func(21);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToFuncWithDefault_ReturnsDefaultOnFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWith<int, int>(x =>
                x > 0 ? Result.Success(x) : Result.Failure<int>("Negative"))
            .ToFuncWithDefault(-999);

        // Act
        var successResult = pipeline(42);
        var failureResult = pipeline(-1);

        // Assert
        Assert.Equal(42, successResult);
        Assert.Equal(-999, failureResult);
    }

    #endregion

    #region Pipeline.ToAsync Tests

    [Fact]
    public async Task ToAsync_ConvertsToAsyncPipeline()
    {
        // Arrange
        var syncPipeline = Pipeline.Start<int>()
            .Then(x => x * 2);
        var asyncPipeline = syncPipeline.ToAsync();

        // Act
        var result = await asyncPipeline.RunAsync(21);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToAsync_WithNullPipeline_ThrowsArgumentNullException()
    {
        // Arrange
        Pipeline<int, int>? pipeline = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline!.ToAsync());
    }

    #endregion

    #region Pipeline.Parallel Tests

    [Fact]
    public void Parallel_WithTwoPipelines_CombinesResults()
    {
        // Arrange
        var pipeline1 = Pipeline.Start<int>().Then(x => x * 2);
        var pipeline2 = Pipeline.Start<int>().Then(x => x.ToString());
        var combined = Pipeline.Parallel(pipeline1, pipeline2);

        // Act
        var result = combined.Run(21);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value.Item1);
        Assert.Equal("21", result.Value.Item2);
    }

    [Fact]
    public void Parallel_WithThreePipelines_CombinesResults()
    {
        // Arrange
        var pipeline1 = Pipeline.Start<int>().Then(x => x * 2);
        var pipeline2 = Pipeline.Start<int>().Then(x => x.ToString());
        var pipeline3 = Pipeline.Start<int>().Then(x => x > 10);
        var combined = Pipeline.Parallel(pipeline1, pipeline2, pipeline3);

        // Act
        var result = combined.Run(21);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value.Item1);
        Assert.Equal("21", result.Value.Item2);
        Assert.True(result.Value.Item3);
    }

    [Fact]
    public void Parallel_WithOneFailure_ReturnsFailure()
    {
        // Arrange
        var pipeline1 = Pipeline.Start<int>().Then(x => x * 2);
        var pipeline2 = Pipeline.StartWith<int, string>(_ => Result.Failure<string>("Failed"));
        var combined = Pipeline.Parallel(pipeline1, pipeline2);

        // Act
        var result = combined.Run(21);

        // Assert
        Assert.True(result.IsFailure);
    }

    #endregion

    #region Pipeline.Race Tests

    [Fact]
    public void Race_WithFirstSuccess_ReturnsFirstResult()
    {
        // Arrange
        var pipeline1 = Pipeline.Start<int>().Then(x => x * 2);
        var pipeline2 = Pipeline.Start<int>().Then(x => x * 3);
        var raced = Pipeline.Race(pipeline1, pipeline2);

        // Act
        var result = raced.Run(10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Race_WithFirstFailure_ReturnsSecondResult()
    {
        // Arrange
        var pipeline1 = Pipeline.StartWith<int, int>(_ => Result.Failure<int>("First failed"));
        var pipeline2 = Pipeline.Start<int>().Then(x => x * 3);
        var raced = Pipeline.Race(pipeline1, pipeline2);

        // Act
        var result = raced.Run(10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Value);
    }

    [Fact]
    public void Race_WithAllFailures_ReturnsAggregatedError()
    {
        // Arrange
        var pipeline1 = Pipeline.StartWith<int, int>(_ => Result.Failure<int>("First failed"));
        var pipeline2 = Pipeline.StartWith<int, int>(_ => Result.Failure<int>("Second failed"));
        var raced = Pipeline.Race(pipeline1, pipeline2);

        // Act
        var result = raced.Run(10);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("First failed", result.Error);
        Assert.Contains("Second failed", result.Error);
    }

    [Fact]
    public void Race_WithNullPipelines_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Pipeline.Race<int, int>(null!));
    }

    #endregion

    #region Pipeline.When Tests

    [Fact]
    public void When_WithTrueCondition_ExecutesWhenTrue()
    {
        // Arrange
        var whenTrue = Pipeline.Start<int>().Then(x => x * 2);
        var whenFalse = Pipeline.Start<int>().Then(x => x * 3);
        var conditional = Pipeline.When(x => x > 5, whenTrue, whenFalse);

        // Act
        var result = conditional.Run(10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void When_WithFalseCondition_ExecutesWhenFalse()
    {
        // Arrange
        var whenTrue = Pipeline.Start<int>().Then(x => x * 2);
        var whenFalse = Pipeline.Start<int>().Then(x => x * 3);
        var conditional = Pipeline.When(x => x > 5, whenTrue, whenFalse);

        // Act
        var result = conditional.Run(3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.Value);
    }

    [Fact]
    public void When_WithNullCondition_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.Start<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Pipeline.When<int, int>(null!, pipeline, pipeline));
    }

    #endregion

    #region PipelineAsync.StartAsync Tests

    [Fact]
    public async Task StartAsync_WithType_CreatesPassthroughPipeline()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task StartAsync_WithAsyncTransformation_AppliesTransformation()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int, string>(async x =>
        {
            await Task.Delay(1);
            return x.ToString();
        });

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void StartAsync_WithNullTransformation_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Pipeline.StartAsync<int, string>(null!));
    }

    [Fact]
    public async Task StartWithAsync_WithResultTransformation_AppliesTransformation()
    {
        // Arrange
        var pipeline = Pipeline.StartWithAsync<int, string>(async x =>
        {
            await Task.Delay(1);
            return x > 0 ? Result.Success(x.ToString()) : Result.Failure<string>("Negative");
        });

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("42", result.Value);
    }

    #endregion

    #region PipelineAsync.Then Tests

    [Fact]
    public async Task PipelineAsync_Then_TransformsValue()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>()
            .Then(x => x * 2)
            .Then(x => x + 10);

        // Act
        var result = await pipeline.RunAsync(5);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void PipelineAsync_Then_WithNullTransform_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.Then<string>(null!));
    }

    #endregion

    #region PipelineAsync.ThenAsync Tests

    [Fact]
    public async Task ThenAsync_WithAsyncTransformation_AppliesTransformation()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>()
            .ThenAsync(async x =>
            {
                await Task.Delay(1);
                return x.ToString();
            });

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void ThenAsync_WithNullTransform_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenAsync<string>(null!));
    }

    #endregion

    #region PipelineAsync.ThenBind Tests

    [Fact]
    public async Task PipelineAsync_ThenBind_WithSuccess_TransformsValue()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<string>()
            .ThenBind(s => int.TryParse(s, out var n)
                ? Result.Success(n)
                : Result.Failure<int>("Invalid number"));

        // Act
        var result = await pipeline.RunAsync("42");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void PipelineAsync_ThenBind_WithNullTransform_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<string>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenBind<int>(null!));
    }

    #endregion

    #region PipelineAsync.ThenBindAsync Tests

    [Fact]
    public async Task ThenBindAsync_WithAsyncResult_TransformsValue()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<string>()
            .ThenBindAsync(async s =>
            {
                await Task.Delay(1);
                return int.TryParse(s, out var n)
                    ? Result.Success(n)
                    : Result.Failure<int>("Invalid number");
            });

        // Act
        var result = await pipeline.RunAsync("42");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ThenBindAsync_WithNullTransform_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<string>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenBindAsync<int>(null!));
    }

    #endregion

    #region PipelineAsync.ThenEnsure Tests

    [Fact]
    public async Task PipelineAsync_ThenEnsure_WithSatisfiedCondition_ContinuesPipeline()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>()
            .ThenEnsure(x => x > 0, "Must be positive");

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task PipelineAsync_ThenEnsure_WithUnsatisfiedCondition_ReturnsFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>()
            .ThenEnsure(x => x > 0, "Must be positive");

        // Act
        var result = await pipeline.RunAsync(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Must be positive", result.Error);
    }

    [Fact]
    public void PipelineAsync_ThenEnsure_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenEnsure(null!, "error"));
    }

    #endregion

    #region PipelineAsync.ThenEnsureAsync Tests

    [Fact]
    public async Task ThenEnsureAsync_WithSatisfiedCondition_ContinuesPipeline()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>()
            .ThenEnsureAsync(async x =>
            {
                await Task.Delay(1);
                return x > 0;
            }, "Must be positive");

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task ThenEnsureAsync_WithUnsatisfiedCondition_ReturnsFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>()
            .ThenEnsureAsync(async x =>
            {
                await Task.Delay(1);
                return x > 0;
            }, "Must be positive");

        // Act
        var result = await pipeline.RunAsync(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Must be positive", result.Error);
    }

    [Fact]
    public void ThenEnsureAsync_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenEnsureAsync(null!, "error"));
    }

    #endregion

    #region PipelineAsync.ThenTap Tests

    [Fact]
    public async Task PipelineAsync_ThenTap_ExecutesSideEffect()
    {
        // Arrange
        var sideEffectValue = 0;
        var pipeline = Pipeline.StartAsync<int>()
            .ThenTap(x => sideEffectValue = x);

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(42, sideEffectValue);
    }

    [Fact]
    public void PipelineAsync_ThenTap_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenTap(null!));
    }

    #endregion

    #region PipelineAsync.ThenTapAsync Tests

    [Fact]
    public async Task ThenTapAsync_ExecutesAsyncSideEffect()
    {
        // Arrange
        var sideEffectValue = 0;
        var pipeline = Pipeline.StartAsync<int>()
            .ThenTapAsync(async x =>
            {
                await Task.Delay(1);
                sideEffectValue = x;
            });

        // Act
        var result = await pipeline.RunAsync(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(42, sideEffectValue);
    }

    [Fact]
    public async Task ThenTapAsync_WithFailure_DoesNotExecuteSideEffect()
    {
        // Arrange
        var sideEffectExecuted = false;
        var pipeline = Pipeline.StartWithAsync<int, int>(x =>
                Task.FromResult(x > 0 ? Result.Success(x) : Result.Failure<int>("Negative")))
            .ThenTapAsync(async _ =>
            {
                await Task.Delay(1);
                sideEffectExecuted = true;
            });

        // Act
        var result = await pipeline.RunAsync(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.False(sideEffectExecuted);
    }

    [Fact]
    public void ThenTapAsync_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.ThenTapAsync(null!));
    }

    #endregion

    #region PipelineAsync.Catch Tests

    [Fact]
    public async Task PipelineAsync_Catch_WithFallbackValue_RecoversFromFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWithAsync<int, int>(x =>
                Task.FromResult(x > 0 ? Result.Success(x) : Result.Failure<int>("Negative")))
            .Catch(0);

        // Act
        var result = await pipeline.RunAsync(-1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    #endregion

    #region PipelineAsync.CatchAsync Tests

    [Fact]
    public async Task CatchAsync_WithAsyncFallback_RecoversFromFailure()
    {
        // Arrange
        var pipeline = Pipeline.StartWithAsync<int, string>(x =>
                Task.FromResult(x > 0 ? Result.Success(x.ToString()) : Result.Failure<string>("Negative")))
            .CatchAsync(async error =>
            {
                await Task.Delay(1);
                return $"Recovered from: {error}";
            });

        // Act
        var result = await pipeline.RunAsync(-1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Recovered from: Negative", result.Value);
    }

    [Fact]
    public void CatchAsync_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.CatchAsync(null!));
    }

    #endregion

    #region PipelineAsync.MapError Tests

    [Fact]
    public async Task PipelineAsync_MapError_WithFailure_MapsErrorMessage()
    {
        // Arrange
        var pipeline = Pipeline.StartWithAsync<int, int>(x =>
                Task.FromResult(x > 0 ? Result.Success(x) : Result.Failure<int>("Negative")))
            .MapError(e => $"Wrapped: {e}");

        // Act
        var result = await pipeline.RunAsync(-1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Wrapped: Negative", result.Error);
    }

    [Fact]
    public void PipelineAsync_MapError_WithNullMapper_ThrowsArgumentNullException()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            pipeline.MapError(null!));
    }

    #endregion

    #region PipelineAsync.ToFunc Tests

    [Fact]
    public async Task PipelineAsync_ToFunc_ReturnsExecutableAsyncFunction()
    {
        // Arrange
        var pipeline = Pipeline.StartAsync<int>()
            .Then(x => x * 2);
        var func = pipeline.ToFunc();

        // Act
        var result = await func(21);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    #endregion

    #region Complex Pipeline Scenarios

    [Fact]
    public void ComplexPipeline_WithMultipleSteps_ExecutesCorrectly()
    {
        // Arrange - A realistic data processing pipeline
        var pipeline = Pipeline.Start<string>()
            .ThenBind(input => string.IsNullOrWhiteSpace(input)
                ? Result.Failure<string>("Input cannot be empty")
                : Result.Success(input.Trim()))
            .Then(s => s.ToUpper())
            .ThenEnsure(s => s.Length <= 100, "Input too long")
            .Then(s => s.Split(' '))
            .Then(words => words.Length)
            .ThenIf(count => count == 1, _ => 1)
            .MapError(e => $"Validation failed: {e}");

        // Act
        var result = pipeline.Run("hello world");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task ComplexAsyncPipeline_WithMultipleSteps_ExecutesCorrectly()
    {
        // Arrange - An async data processing pipeline
        var pipeline = Pipeline.StartAsync<int>()
            .ThenEnsure(x => x > 0, "Must be positive")
            .ThenAsync(async x =>
            {
                await Task.Delay(1);
                return x * 2;
            })
            .Then(x => x.ToString())
            .ThenTapAsync(async _ => await Task.Delay(1))
            .MapError(e => $"Pipeline error: {e}");

        // Act
        var result = await pipeline.RunAsync(21);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void Pipeline_ShortCircuits_OnFirstFailure()
    {
        // Arrange
        var step1Called = false;
        var step2Called = false;
        var step3Called = false;

        var pipeline = Pipeline.Start<int>()
            .ThenTap(_ => step1Called = true)
            .ThenEnsure(_ => false, "Forced failure")
            .ThenTap(_ => step2Called = true)
            .ThenTap(_ => step3Called = true);

        // Act
        var result = pipeline.Run(42);

        // Assert
        Assert.True(result.IsFailure);
        Assert.True(step1Called);
        Assert.False(step2Called);
        Assert.False(step3Called);
    }

    #endregion
}
