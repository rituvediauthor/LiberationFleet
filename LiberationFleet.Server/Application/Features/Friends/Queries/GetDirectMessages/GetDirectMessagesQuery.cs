using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Queries.GetDirectMessages;

public record GetDirectMessagesQuery(int FriendUserId, int Limit, int? BeforeMessageId) : IRequest<DirectMessageListResponse>;

public class GetDirectMessagesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUserRepository userRepository,
    IDirectMessageRepository directMessageRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetDirectMessagesQuery, DirectMessageListResponse>
{
    private const int MaxLimit = 50;

    public async Task<DirectMessageListResponse> Handle(GetDirectMessagesQuery request, CancellationToken cancellationToken)
    {
        var access = await FriendAccessHelper.ValidateFriendMessagingAsync(
            currentUser,
            membershipRepository,
            friendshipRepository,
            blockRepository,
            userRepository,
            request.FriendUserId,
            cancellationToken);
        if (!access.Success)
        {
            return new DirectMessageListResponse { Success = false, Message = access.Message };
        }

        var conversation = await directMessageRepository.GetConversationBetweenUsersAsync(
            access.ViewerId,
            request.FriendUserId,
            cancellationToken);

        if (conversation is null)
        {
            return new DirectMessageListResponse
            {
                Success = true,
                Message = "Messages loaded.",
                FriendUsername = access.FriendUsername,
                Items = Array.Empty<DirectMessageDto>(),
                HasMore = false
            };
        }

        var limit = request.Limit <= 0 ? MaxLimit : Math.Min(request.Limit, MaxLimit);
        var messages = request.BeforeMessageId.HasValue
            ? await directMessageRepository.GetMessagesBeforeIdAsync(conversation.Id, request.BeforeMessageId.Value, limit, cancellationToken)
            : await directMessageRepository.GetLatestMessagesAsync(conversation.Id, limit, cancellationToken);

        var resourceIds = messages.Select(m => m.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.DirectMessage,
            resourceIds,
            crewId: access.CrewId,
            cancellationToken: cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var hasMore = false;
        if (messages.Count > 0)
        {
            var oldestId = messages[0].Id;
            var older = await directMessageRepository.GetMessagesBeforeIdAsync(conversation.Id, oldestId, 1, cancellationToken);
            hasMore = older.Count > 0;
        }

        var items = messages.Select(message =>
        {
            envelopeById.TryGetValue(message.Id.ToString(), out var envelope);
            return DirectMessageMapper.MapMessage(message, envelope);
        }).ToList();

        return new DirectMessageListResponse
        {
            Success = true,
            Message = "Messages loaded.",
            FriendUsername = access.FriendUsername,
            Items = items,
            HasMore = hasMore
        };
    }
}
