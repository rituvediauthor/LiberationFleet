using LiberationFleet.Server.Application.Features.Library;

namespace LiberationFleet.Server.Tests.Application.Features.Library;

public class LibraryPriorityTierTests
{
    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(0, 0, 2)]
    [InlineData(0, 3, 0)]
    [InlineData(1, 3, 1)]
    [InlineData(2, 3, 2)]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 0)]
    [InlineData(2, 4, 1)]
    [InlineData(3, 4, 2)]
    [InlineData(0, 6, 0)]
    [InlineData(1, 6, 0)]
    [InlineData(2, 6, 1)]
    [InlineData(3, 6, 1)]
    [InlineData(4, 6, 2)]
    [InlineData(5, 6, 2)]
    public void ResolveTercileBandFromIndex_SplitsIntoThirds(int index, int count, int expectedBand)
    {
        LibraryPriorityTier.ResolveTercileBandFromIndex(index, count).Should().Be(expectedBand);
    }

    [Fact]
    public void AssignTiers_CrewBase_MapsTercilesToTiers4Through6()
    {
        var scores = new Dictionary<int, decimal>
        {
            [1] = 10m,
            [2] = 20m,
            [3] = 30m
        };

        var tiers = LibraryPriorityTier.AssignTiers(scores, LibraryPriorityTier.CrewmateMinTier);

        tiers[1].Should().Be(4);
        tiers[2].Should().Be(5);
        tiers[3].Should().Be(6);
    }

    [Fact]
    public void AssignTiers_FleetBase_MapsTercilesToTiers1Through3()
    {
        var scores = new Dictionary<int, decimal>
        {
            [1] = 10m,
            [2] = 20m,
            [3] = 30m
        };

        var tiers = LibraryPriorityTier.AssignTiers(scores, LibraryPriorityTier.FleetMateMinTier);

        tiers[1].Should().Be(1);
        tiers[2].Should().Be(2);
        tiers[3].Should().Be(3);
    }

    [Fact]
    public void AssignTiers_SoleMember_GetsTopBand()
    {
        var scores = new Dictionary<int, decimal> { [9] = 50m };

        LibraryPriorityTier.AssignTiers(scores, LibraryPriorityTier.CrewmateMinTier)[9].Should().Be(6);
        LibraryPriorityTier.AssignTiers(scores, LibraryPriorityTier.FleetMateMinTier)[9].Should().Be(3);
    }

    [Fact]
    public void AssignTiers_RejectsInvalidTierBase()
    {
        var act = () => LibraryPriorityTier.AssignTiers(new Dictionary<int, decimal> { [1] = 1m }, 2);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BuildTierCounts_TalliesClampedTiersAcrossSixSlots()
    {
        var counts = LibraryPriorityTier.BuildTierCounts([1, 2, 2, 5, 6, 99]);
        counts.Should().Equal(1, 2, 0, 0, 1, 2);
    }

    [Fact]
    public void EmptyTierCounts_HasLengthSix()
    {
        LibraryPriorityTier.EmptyTierCounts().Should().Equal(0, 0, 0, 0, 0, 0);
    }
}
