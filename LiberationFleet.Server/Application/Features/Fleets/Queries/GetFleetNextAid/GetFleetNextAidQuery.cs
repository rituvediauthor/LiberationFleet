using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetReceptionOrder;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetNextAid;

public record GetFleetNextAidQuery : IRequest<NextAidDto?>;

public class GetFleetNextAidQueryHandler(
    IMediator mediator,
    ICurrentUserService currentUser) : IRequestHandler<GetFleetNextAidQuery, NextAidDto?>
{
    public async Task<NextAidDto?> Handle(GetFleetNextAidQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var userId = currentUser.UserId.Value;
        var entries = await mediator.Send(
            new GetFleetReceptionOrderQuery(Limit: 1, ForRecordGift: false, ExcludeSelfAsRecipient: false),
            cancellationToken);
        var first = entries.FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        var platformDisplay = ResolveNextAidPlatformDisplay(first, userId);

        return new NextAidDto
        {
            RecipientName = first.Username,
            Amount = first.AmountNeeded,
            IsCurrentUserRecipient = first.UserId == userId,
            PlatformDisplayKind = platformDisplay.Kind,
            PlatformName = platformDisplay.Name,
            PlatformHandle = platformDisplay.Handle,
            HasUnverifiedPending = first.HasUnverifiedPending,
            IsUnlimitedNeed = first.IsUnlimitedNeed
        };
    }

    private static (string Kind, string? Name, string? Handle) ResolveNextAidPlatformDisplay(
        ReceptionOrderEntryDto entry,
        int viewerUserId)
    {
        if (entry.UserId == viewerUserId)
        {
            return (NextAidPlatformDisplayKind.None, null, null);
        }

        if (entry.CommonPlatformIds.Count > 0)
        {
            if (!string.IsNullOrEmpty(entry.RecipientPreferredPlatformName))
            {
                var preferred = entry.RecipientPlatformAccounts
                    .FirstOrDefault(a => a.Name == entry.RecipientPreferredPlatformName);
                if (preferred is not null)
                {
                    return (NextAidPlatformDisplayKind.Preferred, preferred.Name, preferred.Handle);
                }
            }

            var common = entry.RecipientPlatformAccounts.FirstOrDefault();
            if (common is not null)
            {
                return (NextAidPlatformDisplayKind.Common, common.Name, common.Handle);
            }
        }

        if (entry.MiddlemanOptions.Count > 0)
        {
            return (NextAidPlatformDisplayKind.MiddlemanNeeded, null, null);
        }

        return (NextAidPlatformDisplayKind.Unavailable, null, null);
    }
}
