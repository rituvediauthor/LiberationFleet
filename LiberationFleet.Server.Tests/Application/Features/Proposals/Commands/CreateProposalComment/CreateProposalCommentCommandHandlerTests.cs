using FluentAssertions;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Features.Proposals.Commands.CreateProposalComment;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Proposals.Commands.CreateProposalComment;

public class CreateProposalCommentCommandHandlerTests
{
    [Fact]
    public async Task Handle_UsesAnonymousAliasInReplyAndMentionNotifications()
    {
        var commenter = HandlerTestFixture.CreateUser(id: 1, username: "Commenter");
        var mentionedUser = HandlerTestFixture.CreateUser(id: 2, username: "Mentioned");
        var proposalAuthor = HandlerTestFixture.CreateUser(id: 3, username: "ProposalAuthor");
        var crew = HandlerTestFixture.CreateCrew(id: 7);
        var membership = HandlerTestFixture.CreateMembership(commenter, crew);

        var membershipRepository = HandlerTestFixture.CreateCrewMembershipRepositoryMock();
        membershipRepository
            .Setup(r => r.GetActiveMembershipAsync(commenter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        membershipRepository
            .Setup(r => r.IsUserInCrewAsync(commenter.Id, crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        membershipRepository
            .Setup(r => r.GetActiveMembersByCrewIdAsync(crew.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                membership,
                HandlerTestFixture.CreateMembership(mentionedUser, crew),
                HandlerTestFixture.CreateMembership(proposalAuthor, crew)
            ]);

        var proposal = new Proposal
        {
            Id = 9,
            CrewId = crew.Id,
            AuthorUserId = proposalAuthor.Id,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        ProposalComment? savedComment = null;
        var proposalRepository = new Mock<IProposalRepository>(MockBehavior.Loose);
        proposalRepository
            .Setup(r => r.GetByIdAsync(proposal.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proposal);
        proposalRepository
            .Setup(r => r.AddCommentAsync(It.IsAny<ProposalComment>(), It.IsAny<CancellationToken>()))
            .Callback<ProposalComment, CancellationToken>((comment, _) =>
            {
                comment.Id = 41;
                savedComment = comment;
            })
            .Returns(Task.CompletedTask);
        proposalRepository
            .Setup(r => r.GetAnonymousAliasAsync(proposal.Id, commenter.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProposalAnonymousAlias
            {
                ProposalId = proposal.Id,
                UserId = commenter.Id,
                Nickname = "Fox"
            });

        var cryptoRepository = new Mock<ICryptoRepository>(MockBehavior.Loose);
        cryptoRepository
            .Setup(r => r.UpsertEnvelopeAsync(It.IsAny<EncryptedContentEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var createdNotifications = new List<Notification>();
        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Loose);
        notificationRepository
            .Setup(r => r.IsKindEnabledAsync(It.IsAny<int>(), It.IsAny<NotificationKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        notificationRepository
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((item, _) => createdNotifications.Add(item))
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((items, _) => createdNotifications.AddRange(items))
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(r => r.GetUnreadCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var notificationService = HandlerTestFixture.CreateNotificationService(notificationRepository: notificationRepository);

        var mentionRepository = new Mock<IContentMentionRepository>(MockBehavior.Loose);
        mentionRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<ContentMention>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var contentMentionService = new ContentMentionService(
            membershipRepository.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            mentionRepository.Object,
            HandlerTestFixture.CreateUserRepositoryMock().Object,
            HandlerTestFixture.CreateUserBlockRepositoryMock().Object,
            notificationService,
            HandlerTestFixture.CreateUnitOfWorkMock().Object);

        var handler = new CreateProposalCommentCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(commenter.Id).Object,
            membershipRepository.Object,
            HandlerTestFixture.CreateFleetRepositoryMock().Object,
            proposalRepository.Object,
            cryptoRepository.Object,
            new ProposalAnonymousAliasService(proposalRepository.Object),
            notificationService,
            contentMentionService,
            HandlerTestFixture.CreateUnitOfWorkMock().Object);

        var result = await handler.Handle(
            new CreateProposalCommentCommand(
                proposal.Id,
                null,
                "hello @Mentioned",
                "nonce",
                "ciphertext",
                1,
                [mentionedUser.Id]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Alias.Should().Be("Fox");
        savedComment.Should().NotBeNull();
        createdNotifications.Should().HaveCount(2);
        createdNotifications.Select(n => n.ActorUserId).Should().OnlyContain(actorUserId => actorUserId == null);
        createdNotifications.Should().ContainSingle(n =>
            n.Kind == NotificationKind.NewProposalReply
            && n.Title.Contains("Fox")
            && !n.Title.Contains(commenter.Username));
        createdNotifications.Should().ContainSingle(n =>
            n.Kind == NotificationKind.Mention
            && n.Title.Contains("Fox mentioned you"));
    }
}
