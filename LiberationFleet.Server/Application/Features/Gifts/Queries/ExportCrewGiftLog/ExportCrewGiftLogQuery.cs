using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.ExportCrewGiftLog;

public record ExportCrewGiftLogQuery : IRequest<GiftLogResponse>;

public class ExportCrewGiftLogQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<ExportCrewGiftLogQuery, GiftLogResponse>
{
    public async Task<GiftLogResponse> Handle(ExportCrewGiftLogQuery request, CancellationToken cancellationToken)
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

        if (!CrewRoleAuthorizationService.CanExportCrewData(membership))
        {
            return new GiftLogResponse { Success = false, Message = "You do not have permission to export the gift log." };
        }

        var allItems = new List<GiftLogEntryDto>();
        DateTime? beforeCreatedAt = null;
        int? beforeId = null;
        var hasMore = true;

        while (hasMore)
        {
            var page = await giftRepository.GetLogPageByCrewIdAsync(
                membership.CrewId,
                100,
                beforeCreatedAt,
                beforeId,
                cancellationToken);

            if (page.Items.Count == 0)
            {
                break;
            }

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

            foreach (var gift in page.Items)
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

                allItems.Add(entry);
            }

            hasMore = page.HasMore;
            if (!hasMore)
            {
                break;
            }

            var last = page.Items[^1];
            beforeCreatedAt = last.CreatedAt;
            beforeId = last.Id;
        }

        return new GiftLogResponse
        {
            Success = true,
            Message = "Gift log exported.",
            Items = allItems,
            HasMore = false
        };
    }
}
