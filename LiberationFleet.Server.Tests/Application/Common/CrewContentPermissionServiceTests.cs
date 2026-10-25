using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Tests.Application.Common;

public class CrewContentPermissionServiceTests
{
    [Fact]
    public void CanAttachFiles_WhenDenied_IsFalseEvenIfRulesAreMet()
    {
        var crew = new Crew
        {
            AllowCrewmateFileAttachments = true,
            MinimumCrewmateTenureDaysForAttachments = 30,
            MinimumContributionForAttachments = 20m
        };
        var membership = new CrewMembership { AttachmentsDenied = true };

        CrewContentPermissionService.CanAttachFilesToCrewContent(crew, membership, 100m, 90)
            .Should().BeFalse();
        AttachmentPermissionReason.ForCrew(crew, membership, 100m, 90)
            .Should().Be(AttachmentPermissionReason.Blocked);
    }

    [Fact]
    public void CanAttachFiles_WhenDisabled_ReturnsDisabledReason()
    {
        var crew = new Crew
        {
            AllowCrewmateFileAttachments = false,
            MinimumCrewmateTenureDaysForAttachments = 0,
            MinimumContributionForAttachments = 0m
        };
        var membership = new CrewMembership();

        CrewContentPermissionService.CanAttachFilesToCrewContent(crew, membership, 100m, 90)
            .Should().BeFalse();
        AttachmentPermissionReason.ForCrew(crew, membership, 100m, 90)
            .Should().Be(AttachmentPermissionReason.Disabled);
    }

    [Fact]
    public void CanAttachFiles_WhenTenureOrContributionShort_ReturnsRequirementsReason()
    {
        var crew = new Crew
        {
            AllowCrewmateFileAttachments = true,
            MinimumCrewmateTenureDaysForAttachments = 30,
            MinimumContributionForAttachments = 20m
        };
        var membership = new CrewMembership();

        CrewContentPermissionService.CanAttachFilesToCrewContent(crew, membership, 5m, 2)
            .Should().BeFalse();
        AttachmentPermissionReason.ForCrew(crew, membership, 5m, 2)
            .Should().Be(AttachmentPermissionReason.Requirements);
    }

    [Fact]
    public void CanAttachFiles_OrganizerIgnoresDeny()
    {
        var crew = new Crew { AllowCrewmateFileAttachments = false };
        var membership = new CrewMembership { IsOrganizer = true, AttachmentsDenied = true };

        CrewContentPermissionService.CanAttachFilesToCrewContent(crew, membership, 0m, 0)
            .Should().BeTrue();
        AttachmentPermissionReason.ForCrew(crew, membership, 0m, 0)
            .Should().BeNull();
    }
}
