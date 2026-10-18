using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Proposals;

public class ProposalListTimerTests
{
    [Fact]
    public void NeedsTimerResolve_IsFalseUntilPendingTimerExpires()
    {
        var now = DateTime.UtcNow;
        ProposalListTimer.NeedsTimerResolve(
            new Proposal { Status = ProposalStatus.Pending, ApprovalTimerEndsAt = now.AddMinutes(5) },
            now).Should().BeFalse();
        ProposalListTimer.NeedsTimerResolve(
            new Proposal { Status = ProposalStatus.Approved, ApprovalTimerEndsAt = now.AddMinutes(-5) },
            now).Should().BeFalse();
        ProposalListTimer.NeedsTimerResolve(
            new Proposal { Status = ProposalStatus.Pending, ApprovalTimerEndsAt = now.AddMinutes(-1) },
            now).Should().BeTrue();
    }

    [Fact]
    public async Task ResolveExpired_DoesNotTouchRepositoriesWhenNothingIsDue()
    {
        var now = DateTime.UtcNow;
        var proposals = new List<Proposal>
        {
            new() { Id = 1, CrewId = 9, Status = ProposalStatus.Pending, ApprovalTimerEndsAt = now.AddHours(2) },
            new() { Id = 2, CrewId = 9, Status = ProposalStatus.Approved, ApprovalTimerEndsAt = now.AddHours(-1) }
        };
        var crewRepository = new Mock<ICrewRepository>(MockBehavior.Strict);
        var fleetRepository = new Mock<IFleetRepository>(MockBehavior.Strict);
        var proposalRepository = new Mock<IProposalRepository>(MockBehavior.Strict);
        var applyCount = 0;

        await ProposalListTimer.ResolveExpiredAsync(
            proposals,
            now,
            crewRepository.Object,
            fleetRepository.Object,
            proposalRepository.Object,
            (_, _, _) =>
            {
                applyCount++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        applyCount.Should().Be(0);
        crewRepository.VerifyNoOtherCalls();
        fleetRepository.VerifyNoOtherCalls();
        proposalRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveExpired_LoadsCrewOnceAndAppliesOnlyNewlyApproved()
    {
        var now = DateTime.UtcNow;
        var dueApprove = new Proposal
        {
            Id = 1,
            CrewId = 9,
            Status = ProposalStatus.Pending,
            ApprovalTimerEndsAt = now.AddMinutes(-2),
            ApproveCount = 3,
            DisapproveCount = 1
        };
        var dueReject = new Proposal
        {
            Id = 2,
            CrewId = 9,
            Status = ProposalStatus.Pending,
            ApprovalTimerEndsAt = now.AddMinutes(-2),
            ApproveCount = 0,
            DisapproveCount = 2
        };
        var crewRepository = new Mock<ICrewRepository>(MockBehavior.Strict);
        crewRepository
            .Setup(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Crew { Id = 9, AutoResolveOverTime = true });
        var fleetRepository = new Mock<IFleetRepository>(MockBehavior.Strict);
        var proposalRepository = new Mock<IProposalRepository>(MockBehavior.Strict);
        proposalRepository
            .Setup(x => x.GetActiveCrewMemberCountAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        var appliedIds = new List<int>();

        await ProposalListTimer.ResolveExpiredAsync(
            [dueApprove, dueReject],
            now,
            crewRepository.Object,
            fleetRepository.Object,
            proposalRepository.Object,
            (proposal, _, _) =>
            {
                appliedIds.Add(proposal.Id);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        dueApprove.Status.Should().Be(ProposalStatus.Approved);
        dueReject.Status.Should().Be(ProposalStatus.Rejected);
        appliedIds.Should().Equal(1);
        crewRepository.Verify(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>()), Times.Once);
        proposalRepository.Verify(x => x.GetActiveCrewMemberCountAsync(9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveExpired_PersistsStatusBeforeApply()
    {
        var now = DateTime.UtcNow;
        var dueApprove = new Proposal
        {
            Id = 1,
            CrewId = 9,
            Status = ProposalStatus.Pending,
            ApprovalTimerEndsAt = now.AddMinutes(-2),
            ApproveCount = 1,
            DisapproveCount = 0
        };
        var crewRepository = new Mock<ICrewRepository>(MockBehavior.Strict);
        crewRepository
            .Setup(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Crew { Id = 9, AutoResolveOverTime = true });
        var proposalRepository = new Mock<IProposalRepository>(MockBehavior.Strict);
        proposalRepository
            .Setup(x => x.GetActiveCrewMemberCountAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var order = new List<string>();

        await ProposalListTimer.ResolveExpiredAsync(
            [dueApprove],
            now,
            crewRepository.Object,
            new Mock<IFleetRepository>(MockBehavior.Strict).Object,
            proposalRepository.Object,
            (_, _, _) =>
            {
                order.Add("apply");
                return Task.CompletedTask;
            },
            CancellationToken.None,
            persistResolvedStatus: _ =>
            {
                order.Add("persist");
                dueApprove.Status.Should().Be(ProposalStatus.Approved);
                return Task.CompletedTask;
            });

        order.Should().Equal("persist", "apply");
    }

    [Fact]
    public void SelectForRequestedStatus_DropsResolvedRowsFromPending()
    {
        var pending = new List<Proposal>
        {
            new() { Id = 1, Status = ProposalStatus.Approved, LastActivityAt = DateTime.UtcNow },
            new() { Id = 2, Status = ProposalStatus.Pending, LastActivityAt = DateTime.UtcNow }
        };

        var result = ProposalListTimer.SelectForRequestedStatus(ProposalStatus.Pending, pending);

        result.Select(p => p.Id).Should().Equal(2);
    }

    [Fact]
    public void SelectForRequestedStatus_IncludesNewlyApprovedInApprovedList()
    {
        var now = DateTime.UtcNow;
        var pending = new List<Proposal>
        {
            new() { Id = 1, Status = ProposalStatus.Approved, LastActivityAt = now },
            new() { Id = 2, Status = ProposalStatus.Pending, LastActivityAt = now }
        };
        var storedApproved = new List<Proposal>
        {
            new() { Id = 3, Status = ProposalStatus.Approved, LastActivityAt = now.AddMinutes(-1) }
        };

        var result = ProposalListTimer.SelectForRequestedStatus(
            ProposalStatus.Approved,
            pending,
            storedApproved);

        result.Select(p => p.Id).Should().Equal(1, 3);
    }
}
