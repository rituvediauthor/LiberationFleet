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
                : user.Username;
}
