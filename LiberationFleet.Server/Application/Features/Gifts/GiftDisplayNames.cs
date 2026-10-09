using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Gifts;

public static class GiftDisplayNames
{
    public static string GetRecipientName(User? user) =>
        user is null
            ? "Unknown"
            : user.IsCrewGiftRecipient
                ? CrewGiftRecipientService.DisplayName
                : UserDisplay.Name(user);

    public static string GetUserName(User? user) =>
        user is null ? string.Empty : UserDisplay.Name(user);
}
