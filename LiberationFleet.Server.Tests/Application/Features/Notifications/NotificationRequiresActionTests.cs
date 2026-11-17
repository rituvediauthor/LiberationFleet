using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Notifications;

public class NotificationRequiresActionTests
{
    [Fact]
    public void BuildRequest_MarksRecipientAndIntermediaryAsRequiresAction()
    {
        var recipient = GiftEventNotificationKinds.BuildRequest(
            NotificationKind.GiftReceivedByMe,
            viewerUserId: 2,
            crewId: 1,
            giverName: "Ada",
            recipientName: "Bob",
            actionUrl: "/app/crew/gift-log/9",
            relatedEntityId: 9,
            recipientUserId: 2,
            giverUserId: 1,
            fleet: false);
        recipient.RequiresAction.Should().BeTrue();

        var intermediary = GiftEventNotificationKinds.BuildRequest(
            NotificationKind.IntermediaryGift,
            viewerUserId: 3,
            crewId: 1,
            giverName: "Ada",
            recipientName: "Bob",
            actionUrl: "/app/crew/gift-log/9",
            relatedEntityId: 9,
            recipientUserId: 2,
            giverUserId: 1,
            fleet: false);
        intermediary.RequiresAction.Should().BeTrue();

        var spectator = GiftEventNotificationKinds.BuildRequest(
            NotificationKind.NewGifts,
            viewerUserId: 4,
            crewId: 1,
            giverName: "Ada",
            recipientName: "Bob",
            actionUrl: "/app/crew/gift-log/9",
            relatedEntityId: 9,
            recipientUserId: 2,
            giverUserId: 1,
            fleet: false);
        spectator.RequiresAction.Should().BeFalse();
    }

    [Fact]
    public async Task TryMarkReadAsync_SkipsRequiresActionUntilResolved()
    {
        // MarkAllRead / MarkReadByContent use ExecuteUpdate (SQLite only).
        await using var context = await TestDbContextFactory.CreateSqliteAsync();
        var user = new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var notification = new Notification
        {
            UserId = user.Id,
            Kind = NotificationKind.LibraryTaskCompletionSubmitted,
            Title = "Quest completion submitted",
            Body = "Confirm or deny",
            ActionUrl = "/app/crew/library-of-things/tasks/5",
            RelatedEntityId = 5,
            IsRead = false,
            RequiresAction = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var repo = new NotificationRepository(context);
        (await repo.TryMarkReadAsync(notification.Id, user.Id)).Should().BeFalse();
        (await context.Notifications.SingleAsync()).IsRead.Should().BeFalse();

        await repo.MarkAllReadAsync(user.Id);
        (await context.Notifications.SingleAsync()).IsRead.Should().BeFalse();

        (await repo.MarkReadByContentAsync(
            user.Id,
            "/app/crew/library-of-things/tasks/5",
            relatedEntityId: 5)).Should().Be(0);
        (await context.Notifications.SingleAsync()).IsRead.Should().BeFalse();

        (await repo.ResolveRequiresActionAsync(
            user.Id,
            NotificationKind.LibraryTaskCompletionSubmitted,
            relatedEntityId: 5)).Should().Be(1);
        await context.SaveChangesAsync();

        var resolved = await context.Notifications.SingleAsync();
        resolved.RequiresAction.Should().BeFalse();
        resolved.IsRead.Should().BeTrue();
    }

    [Fact]
    public void GetKindLabel_IncludesQuestCompletionSubmitted()
    {
        NotificationService.GetKindLabel(NotificationKind.LibraryTaskCompletionSubmitted)
            .Should().Be("Quest completion submitted");
    }

    [Fact]
    public async Task MarkRead_AllowsWhenQuestDeleted()
    {
        await using var context = TestDbContextFactory.Create();
        var user = new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var notification = new Notification
        {
            UserId = user.Id,
            Kind = NotificationKind.LibraryTaskCompletionSubmitted,
            Title = "Quest completion submitted",
            Body = "Confirm",
            ActionUrl = "/app/crew/library-of-things/tasks/99",
            RelatedEntityId = 99,
            IsRead = false,
            RequiresAction = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var liveness = new ActionRequiredNotificationLiveness(
            new LibraryTaskRepository(context),
            new GiftRepository(context),
            new LibraryRepository(context),
            new CrewInvitationRepository(context),
            new FriendshipRepository(context));

        (await liveness.IsActionStillRequiredAsync(notification)).Should().BeFalse();

        notification.RequiresAction = false;
        notification.IsRead = true;
        await context.SaveChangesAsync();

        var saved = await context.Notifications.SingleAsync();
        saved.IsRead.Should().BeTrue();
        saved.RequiresAction.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveGiftForRecipient_ClearsCrewAndFleetKinds()
    {
        await using var context = TestDbContextFactory.Create();
        var user = new User
        {
            Username = "bob",
            Email = "bob@example.com",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.Notifications.AddRange(
            new Notification
            {
                UserId = user.Id,
                Kind = NotificationKind.GiftReceivedByMe,
                Title = "Gift",
                Body = "Crew",
                ActionUrl = "/app/crew/gift-log/9",
                RelatedEntityId = 9,
                RequiresAction = true,
                CreatedAt = DateTime.UtcNow
            },
            new Notification
            {
                UserId = user.Id,
                Kind = NotificationKind.FleetGiftReceivedByMe,
                Title = "Fleet gift",
                Body = "Fleet",
                ActionUrl = "/app/fleet/gift-log?highlightId=9",
                RelatedEntityId = 9,
                RequiresAction = true,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var repo = new NotificationRepository(context);
        await ActionRequiredNotificationResolver.ResolveGiftForRecipientAsync(
            repo,
            user.Id,
            giftId: 9,
            fleet: false);
        await context.SaveChangesAsync();

        var rows = await context.Notifications.ToListAsync();
        rows.Should().OnlyContain(n => !n.RequiresAction && n.IsRead);
    }
}
