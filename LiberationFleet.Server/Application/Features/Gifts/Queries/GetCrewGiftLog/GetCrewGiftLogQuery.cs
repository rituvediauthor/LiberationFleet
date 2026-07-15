using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;

public record GetCrewGiftLogQuery(
    int Limit = 50,
    DateTime? BeforeCreatedAt = null,
    int? BeforeId = null) : IRequest<GiftLogResponse>;

public class GetCrewGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewGiftLogQuery, GiftLogResponse>
{
    public async Task<GiftLogResponse> Handle(GetCrewGiftLogQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftLogResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftLogResponse { Success = false, Message = "You are not in a crew." };
        }

        var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 100);
        var page = await giftRepository.GetLogPageByCrewIdAsync(
            membership.CrewId,
            limit,
            request.BeforeCreatedAt,
            request.BeforeId,
            cancellationToken);

        var completedByInitiated = await giftRepository.GetCompletedGiftsByInitiatedIdsAsync(membership.CrewId, cancellationToken);

        var initiatedParents = page.Items
            .Where(g => g.Type == GiftType.Initiated)
            .ToDictionary(g => g.Id, g => g);

        var missingParentIds = page.Items
            .Where(g => g.Type == GiftType.Completed && g.InitiatedGiftId.HasValue)
            .Select(g => g.InitiatedGiftId!.Value)
            .Where(id => !initiatedParents.ContainsKey(id))
            .Distinct()
            .ToList();

        foreach (var parentId in missingParentIds)
        {
            var parent = await giftRepository.GetByIdWithUsersAsync(parentId, cancellationToken);
            if (parent is not null)
            {
                initiatedParents[parentId] = parent;
            }
        }

        var giftIds = page.Items.Select(g => g.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.GiftLogEntry,
            giftIds,
            crewId: membership.CrewId,
            cancellationToken: cancellationToken);
        var envelopeByGiftId = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = page.Items.Select(gift =>
        {
            completedByInitiated.TryGetValue(gift.Id, out var completedChild);
            Gift? initiatedParent = null;
            if (gift.Type == GiftType.Completed && gift.InitiatedGiftId.HasValue)
            {
                initiatedParents.TryGetValue(gift.InitiatedGiftId.Value, out initiatedParent);
            }

            var entry = GiftMapper.MapGift(gift, userId, completedChild, initiatedParent);
            if (envelopeByGiftId.TryGetValue(gift.Id.ToString(), out var envelope))
            {
                entry.HasEncryptedContent = true;
                entry.EncryptedPayload = CryptoMapper.MapPayload(envelope);
                entry.GiverName = string.Empty;
                entry.RecipientName = string.Empty;
                entry.MiddlemanName = null;
                entry.Platform = string.Empty;
                entry.Message = string.Empty;
            }

            return entry;
        }).ToList();

        return new GiftLogResponse
        {
            Success = true,
            Message = "Gift log loaded.",
            Items = items,
            HasMore = page.HasMore
        };
    }
}
