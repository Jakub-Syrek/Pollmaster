using Pollmaster.Shared.Common;

namespace Pollmaster.Api.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_ExposesValueAndIsSuccess()
    {
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ExposesErrorAndIsFailure()
    {
        var result = Result<int>.Failure("boom");
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void AccessingValueOnFailure_Throws()
    {
        var result = Result<int>.Failure("nope");
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void AccessingErrorOnSuccess_Throws()
    {
        var result = Result<int>.Success(1);
        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }
}
