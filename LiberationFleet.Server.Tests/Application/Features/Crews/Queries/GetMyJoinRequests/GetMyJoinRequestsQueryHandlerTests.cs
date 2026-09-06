using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetMyJoinRequests;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Queries.GetMyJoinRequests;

public class GetMyJoinRequestsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsFailure()
    {
        var handler = CreateHandler(currentUserId: null);

        var result = await handler.Handle(new GetMyJoinRequestsQuery(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized.");
    }

    [Fact]
    public async Task Handle_HidesVoteProgressFromApplicant()
    {
        var user = HandlerTestFixture.CreateUser();
        var crew = HandlerTestFixture.CreateCrew(id: 7, name: "Harbor Crew");
        var proposal = new Proposal
        {
            Id = 42,
            CrewId = crew.Id,
            AuthorUserId = user.Id,
            Kind = ProposalKind.CrewJoinRequest,
            Status = ProposalStatus.Pending,
            ApproveCount = 3,
            DisapproveCount = 1,
            ApprovalTimerEndsAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var proposalRepository = new Mock<IProposalRepository>(MockBehavior.Strict);
        proposalRepository
            .Setup(r => r.GetJoinRequestProposalsByApplicantAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Proposal> { proposal });
        proposalRepository
            .Setup(r => r.GetCrewJoinRequestsByProposalIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.Single() == proposal.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, ProposalCrewJoinRequest>
            {
                [proposal.Id] = new ProposalCrewJoinRequest
                {
                    ProposalId = proposal.Id,
                    ApplicantUserId = user.Id,
                    IsKeyPrepared = true
                }
            });

        var crewRepository = HandlerTestFixture.CreateCrewRepositoryMock();
        crewRepository
            .Setup(r => r.GetByIdAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(crew);

        var handler = CreateHandler(
            currentUserId: user.Id,
            proposalRepository: proposalRepository,
            crewRepository: crewRepository);

        var result = await handler.Handle(new GetMyJoinRequestsQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle();
        var item = result.Items[0];
        item.CrewName.Should().Be("Harbor Crew");
        item.Status.Should().Be(nameof(ProposalStatus.Pending));
        item.IsKeyPrepared.Should().BeTrue();
        item.ApproveCount.Should().Be(0);
        item.DisapproveCount.Should().Be(0);
        item.ApprovalTimerEndsAt.Should().BeNull();
    }

    private static GetMyJoinRequestsQueryHandler CreateHandler(
        int? currentUserId = 1,
        Mock<IProposalRepository>? proposalRepository = null,
        Mock<ICrewRepository>? crewRepository = null)
    {
        proposalRepository ??= new Mock<IProposalRepository>(MockBehavior.Strict);
        proposalRepository
            .Setup(r => r.GetJoinRequestProposalsByApplicantAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Proposal>());
        proposalRepository
            .Setup(r => r.GetCrewJoinRequestsByProposalIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, ProposalCrewJoinRequest>());

        crewRepository ??= HandlerTestFixture.CreateCrewRepositoryMock();

        return new GetMyJoinRequestsQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(currentUserId).Object,
            proposalRepository.Object,
            crewRepository.Object);
    }
}
