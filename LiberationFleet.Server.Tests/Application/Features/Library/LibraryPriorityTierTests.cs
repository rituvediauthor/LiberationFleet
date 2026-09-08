using LiberationFleet.Server.Application.Features.Library;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryPriorityTierTests
{
    [Theory]
    [InlineData(0, 100, 1)]
    [InlineData(10, 0, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(20, 100, 1)]
    [InlineData(20.01, 100, 2)]
    [InlineData(40, 100, 2)]
    [InlineData(40.01, 100, 3)]
    [InlineData(60, 100, 3)]
    [InlineData(60.01, 100, 4)]
    [InlineData(80, 100, 4)]
    [InlineData(80.01, 100, 5)]
    [InlineData(100, 100, 5)]
    [InlineData(250, 100, 5)]
    public void ResolveTier_UsesConfirmedBands(decimal score, decimal average, int expectedTier)
    {
        LibraryPriorityTier.ResolveTier(score, average).Should().Be(expectedTier);
    }

    [Fact]
    public void BuildTierCounts_TalliesClampedTiers()
    {
        var counts = LibraryPriorityTier.BuildTierCounts([1, 2, 2, 5, 99]);
        counts.Should().Equal(1, 2, 0, 0, 2);
    }
}
