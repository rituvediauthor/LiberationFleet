using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using FluentAssertions;

namespace LiberationFleet.Server.Tests.Application.Features.Notifications;

public class NotificationBadgeBuilderTests
{
    [Fact]
    public void Build_ExcludesDisabledKindsFromUnreadAndAreas()
    {
        var notifications = new[]
        {
            Notification(1, NotificationKind.NewChatMessage, "/app/crew/chats/5"),
            Notification(2, NotificationKind.NewForumPost, "/app/crew/forums/9")
        };
        var preferences = new[]
        {
            new UserNotificationPreference { Kind = NotificationKind.NewChatMessage, IsEnabled = false }
        };

        var summary = NotificationBadgeBuilder.Build(
            notifications,
            preferences,
            Array.Empty<UserMutedContent>(),
            Array.Empty<UserHiddenContent>(),
            new HashSet<int>());

        summary.UnreadCount.Should().Be(1);
        summary.AreaCounts["crewChats"].Should().Be(0);
        summary.AreaCounts["crewForums"].Should().Be(1);
    }

    [Fact]
    public void Build_MapsCrewInvitationToUserInvitationsArea()
    {
        var notifications = new[]
        {
            Notification(
                1,
                NotificationKind.JoinRequestFromCrew,
                "/app/crew/invitations/42",
                relatedEntityId: 42)
        };

        var summary = NotificationBadgeBuilder.Build(
            notifications,
            Array.Empty<UserNotificationPreference>(),
            Array.Empty<UserMutedContent>(),
            Array.Empty<UserHiddenContent>(),
            new HashSet<int>());

        summary.UnreadCount.Should().Be(1);
        summary.AreaCounts["userInvitations"].Should().Be(1);
        summary.AreaCounts.ContainsKey("fleetCrewmates").Should().BeTrue();
        summary.AreaCounts["fleetCrewmates"].Should().Be(0);
        summary.AreaCounts.ContainsKey("fleetLibrary").Should().BeFalse();
    }

    [Fact]
    public void Build_DoesNotDropProposalReplyWhenForumIsMuted()
    {
        var notifications = new[]
        {
            Notification(
                1,
                NotificationKind.NewProposalReply,
                "/app/crew/proposals/7?commentId=3",
                relatedEntityId: 7,
                secondaryEntityId: 3)
        };
        var muted = new[]
        {
            new UserMutedContent { ContentType = MutedContentType.Forum, ResourceId = 7 }
        };

        var summary = NotificationBadgeBuilder.Build(
            notifications,
            Array.Empty<UserNotificationPreference>(),
            muted,
            Array.Empty<UserHiddenContent>(),
            new HashSet<int>());

        summary.UnreadCount.Should().Be(1);
        summary.AreaCounts["crewProposals"].Should().Be(1);
        summary.ResourceCounts["proposal:7"].Should().Be(1);
    }

    [Fact]
    public void Build_ExcludesMutedForumPost()
    {
        var notifications = new[]
        {
            Notification(1, NotificationKind.NewForumComment, "/app/crew/forums/11", relatedEntityId: 11)
        };
        var muted = new[]
        {
            new UserMutedContent { ContentType = MutedContentType.Forum, ResourceId = 11 }
        };

        var summary = NotificationBadgeBuilder.Build(
            notifications,
            Array.Empty<UserNotificationPreference>(),
            muted,
            Array.Empty<UserHiddenContent>(),
            new HashSet<int>());

        summary.UnreadCount.Should().Be(0);
    }

    [Fact]
    public void GetKindCategory_JoinRequestFromCrew_IsCrew()
    {
        NotificationService.GetKindCategory(NotificationKind.JoinRequestFromCrew).Should().Be("Crew");
    }

    [Fact]
    public void CategoryMapper_MapsProposalRepliesToProposals()
    {
        NotificationCategoryMapper.ToFilterCategory(NotificationKind.NewProposalReply)
            .Should().Be(NotificationFilterCategory.Proposals);
        NotificationCategoryMapper.ToFilterCategory(NotificationKind.NewFleetProposalReply)
            .Should().Be(NotificationFilterCategory.Proposals);
        NotificationCategoryMapper.ToFilterCategory(NotificationKind.NewReply)
            .Should().Be(NotificationFilterCategory.Comments);
    }

    [Fact]
    public void Build_MapsEmergencyRequestToCrewEmergencyArea()
    {
        var notifications = new[]
        {
            Notification(
                1,
                NotificationKind.NewEmergencyRequest,
                "/app/crew/emergency-requests/9?highlightId=9",
                relatedEntityId: 9)
        };

        var summary = NotificationBadgeBuilder.Build(
            notifications,
            Array.Empty<UserNotificationPreference>(),
            Array.Empty<UserMutedContent>(),
            Array.Empty<UserHiddenContent>(),
            new HashSet<int>());

        summary.UnreadCount.Should().Be(1);
        summary.AreaCounts["crewEmergency"].Should().Be(1);
        summary.AreaCounts["crewGiftLog"].Should().Be(0);
    }

    [Fact]
    public void Build_MapsProposalAcceptedToApprovedStatusBadge()
    {
        var notifications = new[]
        {
            Notification(
                1,
                NotificationKind.ProposalAccepted,
                "/app/crew/proposals/list/approved?highlightId=42",
                relatedEntityId: 42)
        };

        var summary = NotificationBadgeBuilder.Build(
            notifications,
            Array.Empty<UserNotificationPreference>(),
            Array.Empty<UserMutedContent>(),
            Array.Empty<UserHiddenContent>(),
            new HashSet<int>());

        summary.AreaCounts["crewProposals"].Should().Be(1);
        summary.ResourceCounts["proposal-status:approved"].Should().Be(1);
        summary.ResourceCounts["proposal:42"].Should().Be(1);
    }

    private static Notification Notification(
        int id,
        NotificationKind kind,
        string actionUrl,
        int? relatedEntityId = null,
        int? secondaryEntityId = null) =>
        new()
        {
            Id = id,
            UserId = 1,
            Kind = kind,
            Title = kind.ToString(),
            Body = "body",
            ActionUrl = actionUrl,
            RelatedEntityId = relatedEntityId,
            SecondaryEntityId = secondaryEntityId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
}
