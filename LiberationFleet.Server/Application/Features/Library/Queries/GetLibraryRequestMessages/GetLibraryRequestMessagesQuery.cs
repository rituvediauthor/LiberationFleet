using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryRequestMessages;

public record GetLibraryRequestMessagesQuery(int RequestId, int Limit, int? BeforeMessageId)
    : IRequest<LibraryRequestMessageListResponse>;

public class GetLibraryRequestMessagesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetLibraryRequestMessagesQuery, LibraryRequestMessageListResponse>
{
    private const int MaxLimit = 50;

    public async Task<LibraryRequestMessageListResponse> Handle(
        GetLibraryRequestMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestMessageListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryRequestMessageListResponse { Success = false, Message = "You are not in a crew." };
        }

        var libraryRequest = await libraryRepository.GetRequestByIdForCrewAsync(
            request.RequestId,
            membership.CrewId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryRequestMessageListResponse { Success = false, Message = "Request not found." };
        }

        if (!LibraryRequestAccess.CanMessage(libraryRequest, userId))
        {
            return new LibraryRequestMessageListResponse { Success = false, Message = "Messaging is not available for this request." };
        }

        var limit = request.Limit <= 0 ? MaxLimit : Math.Min(request.Limit, MaxLimit);
        var messages = request.BeforeMessageId.HasValue
            ? await libraryRepository.GetRequestMessagesBeforeIdAsync(
                request.RequestId,
                request.BeforeMessageId.Value,
                limit,
                cancellationToken)
            : await libraryRepository.GetLatestRequestMessagesAsync(
                request.RequestId,
                limit,
                cancellationToken);

        var resourceIds = messages.Select(m => m.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.LibraryRequestMessage,
            resourceIds,
            membership.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var hasMore = false;
        if (messages.Count > 0)
        {
            var oldestId = messages[0].Id;
            var older = await libraryRepository.GetRequestMessagesBeforeIdAsync(
                request.RequestId,
                oldestId,
                1,
                cancellationToken);
            hasMore = older.Count > 0;
        }

        return new LibraryRequestMessageListResponse
        {
            Success = true,
            Message = "Messages loaded.",
            Items = messages.Select(message =>
            {
                envelopeById.TryGetValue(message.Id.ToString(), out var envelope);
                return LibraryMapper.MapRequestMessage(message, envelope);
            }).ToList(),
            HasMore = hasMore
        };
    }
}
