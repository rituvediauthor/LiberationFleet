using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Domain;
using FluentAssertions;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts;

public class IdentityGroupKeysTests
{
    [Fact]
    public void AreValid_AcceptsTargetedMinorityKeys()
    {
        IdentityGroupKeys.AreValid(
        [
            IdentityGroupKeys.PhysicallyDisfigured,
            IdentityGroupKeys.Bipoc,
            IdentityGroupKeys.Trans,
            IdentityGroupKeys.Intersex,
            IdentityGroupKeys.UnhousedOrHousingInsecure,
            IdentityGroupKeys.ReligiousOrAreligiousMinority,
            IdentityGroupKeys.OtherTargetedMinority
        ]).Should().BeTrue();
    }

    [Fact]
    public void AreValid_RejectsLegacyKeys()
    {
        IdentityGroupKeys.AreValid(["NonWhite", "Indigenous", "Lgbtqia", "PrimaryCaregiver"])
            .Should().BeFalse();
        IdentityGroupKeys.IsValid("NonWhite").Should().BeFalse();
        IdentityGroupKeys.IsValid("Homeless").Should().BeFalse();
    }

    [Fact]
    public void Serialize_RoundTripsStableKeys()
    {
        var serialized = IdentityGroupKeys.Serialize(
        [
            IdentityGroupKeys.Woman,
            IdentityGroupKeys.Bipoc,
            IdentityGroupKeys.Woman
        ]);

        serialized.Should().Be($"{IdentityGroupKeys.Bipoc},{IdentityGroupKeys.Woman}");
        IdentityGroupKeys.Parse(serialized).Should().Equal(
            IdentityGroupKeys.Bipoc,
            IdentityGroupKeys.Woman);
    }

    [Fact]
    public void Parse_DropsLegacyStoredKeys()
    {
        IdentityGroupKeys.Parse("NonWhite,Woman,Indigenous,Trans")
            .Should().Equal(IdentityGroupKeys.Woman, IdentityGroupKeys.Trans);
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
