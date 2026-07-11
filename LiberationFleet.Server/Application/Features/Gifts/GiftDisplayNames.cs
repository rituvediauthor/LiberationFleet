using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Gifts;

public static class GiftDisplayNames
{
    public static string GetRecipientName(User user) =>
        user.IsCrewGiftRecipient ? CrewGiftRecipientService.DisplayName : user.Username;
}
