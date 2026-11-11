using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.RecordGifts;

public class GiftEventNotificationKindsTests
{
    [Fact]
    public void ResolveForViewer_ExcludesGiver()
    {
        GiftEventNotificationKinds.ResolveForViewer(1, 1, 2, null, fleet: false)
            .Should().BeNull();
    }

    [Fact]
    public void ResolveForViewer_RecipientGetsGiftReceivedByMe_NotGiftsGiven()
    {
        GiftEventNotificationKinds.ResolveForViewer(2, 1, 2, middlemanUserId: 3, fleet: false)
            .Should().Be(NotificationKind.GiftReceivedByMe);
    }

    [Fact]
    public void ResolveForViewer_MiddlemanGetsIntermediaryGift_NotGiftsGiven()
    {
        GiftEventNotificationKinds.ResolveForViewer(3, 1, 2, middlemanUserId: 3, fleet: false)
            .Should().Be(NotificationKind.IntermediaryGift);
    }

    [Fact]
    public void ResolveForViewer_OtherCrewmateGetsGiftsGiven()
    {
        GiftEventNotificationKinds.ResolveForViewer(4, 1, 2, middlemanUserId: 3, fleet: false)
            .Should().Be(NotificationKind.NewGifts);
    }

    [Fact]
    public void ResolveForViewer_FleetUsesFleetKinds()
    {
        GiftEventNotificationKinds.ResolveForViewer(2, 1, 2, null, fleet: true)
            .Should().Be(NotificationKind.FleetGiftReceivedByMe);
        GiftEventNotificationKinds.ResolveForViewer(3, 1, 2, 3, fleet: true)
            .Should().Be(NotificationKind.FleetIntermediaryGift);
        GiftEventNotificationKinds.ResolveForViewer(4, 1, 2, 3, fleet: true)
            .Should().Be(NotificationKind.NewFleetGifts);
    }

    [Fact]
    public void GetKindLabel_UsesGiftToggleLabels()
    {
        NotificationService.GetKindLabel(NotificationKind.NewGifts).Should().Be("Gifts given");
        NotificationService.GetKindLabel(NotificationKind.GiftReceivedByMe).Should().Be("Gifts given to me");
        NotificationService.GetKindLabel(NotificationKind.IntermediaryGift).Should().Be("Intermediary gifts");
        NotificationService.GetKindLabel(NotificationKind.NewFleetGifts).Should().Be("Fleet gifts given");
        NotificationService.GetKindLabel(NotificationKind.FleetGiftReceivedByMe).Should().Be("Fleet gifts given to me");
        NotificationService.GetKindLabel(NotificationKind.FleetIntermediaryGift).Should().Be("Fleet intermediary gifts");
    }
}
