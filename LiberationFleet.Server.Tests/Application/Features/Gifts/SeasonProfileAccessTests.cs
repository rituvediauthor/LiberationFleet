using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Domain;
using FluentAssertions;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts;

public class IdentityGroupKeysTests
{
    [Fact]
    public void AreValid_AcceptsExpandedInclusivityKeys()
    {
        IdentityGroupKeys.AreValid(
        [
            IdentityGroupKeys.Indigenous,
            IdentityGroupKeys.TransOrNonbinary,
            IdentityGroupKeys.ImmigrantOrRefugee,
            IdentityGroupKeys.ReligiousMinority,
            IdentityGroupKeys.Neurodivergent,
            IdentityGroupKeys.PrimaryCaregiver
        ]).Should().BeTrue();
    }

    [Fact]
    public void Serialize_RoundTripsStableKeys()
    {
        var serialized = IdentityGroupKeys.Serialize(
        [
            IdentityGroupKeys.Woman,
            IdentityGroupKeys.Indigenous,
            IdentityGroupKeys.Woman
        ]);

        serialized.Should().Be($"{IdentityGroupKeys.Indigenous},{IdentityGroupKeys.Woman}");
        IdentityGroupKeys.Parse(serialized).Should().Equal(
            IdentityGroupKeys.Indigenous,
            IdentityGroupKeys.Woman);
    }
}

public class SeasonProfileAccessTests
{
    [Fact]
    public void CanEditEstimatedContribution_WhenNeverJoined_ReturnsTrue()
    {
        SeasonProfileAccess.CanEditEstimatedContribution(null, DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void CanEditEstimatedContribution_WhenJoinedUnderNinetyDays_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        SeasonProfileAccess.CanEditEstimatedContribution(now.AddDays(-30), now).Should().BeTrue();
    }

    [Fact]
    public void CanEditEstimatedContribution_WhenJoinedNinetyDaysOrMore_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        SeasonProfileAccess.CanEditEstimatedContribution(now.AddDays(-90), now).Should().BeFalse();
        SeasonProfileAccess.CanEditEstimatedContribution(now.AddDays(-120), now).Should().BeFalse();
    }
}
