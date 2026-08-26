using Aygaz.ECommerce.SemanticKernel.Services;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class GreetingFastPathTests
{
    [Theory]
    [InlineData("selam")]
    [InlineData("merhaba")]
    [InlineData("günaydın")]
    public void IsGreeting_MatchesKnownGreetings(string greeting)
    {
        Assert.True(GreetingFastPath.IsGreeting(greeting));
    }

    [Fact]
    public void IsGreeting_RejectsBusinessQuery()
    {
        Assert.False(GreetingFastPath.IsGreeting("ID'si 2 olan müşteri"));
    }

    [Fact]
    public void GreetingResponse_IsStable()
    {
        Assert.Equal(
            "Merhaba! Aygaz ile ilgili nasıl yardımcı olabilirim?",
            GreetingFastPath.GreetingResponse);
    }
}
