using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetForumPosts;

public record GetFleetForumPostsQuery(int Offset = 0, int Limit = 20) : IRequest<ForumListResponse>;

public class GetFleetForumPostsQueryHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IUserRepository userRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetFleetForumPostsQuery, ForumListResponse>
{
    private const int MaxLimit = 50;

    public async Task<ForumListResponse> Handle(GetFleetForumPostsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var fleet = await fleetRepository.GetFleetForUserAsync(userId, cancellationToken);
        if (fleet is null)
        {
            return new ForumListResponse { Success = false, Message = "You are not in a fleet." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(userId, fleet.Id, cancellationToken))
        {
            return new ForumListResponse { Success = false, Message = "You are not in this fleet." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        var excludeAdult = preference == AdultContentPreference.Block;
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var offset = Math.Max(0, request.Offset);

        var page = await forumRepository.GetByFleetIdPageAsync(
            fleet.Id,
            offset,
            limit,
            excludeAdult,
            hiddenUserIds,
            cancellationToken);
        var posts = page.Items;

        var resourceIds = posts.Select(p => p.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ForumPost,
            resourceIds,
            fleetId: fleet.Id,
            cancellationToken: cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var postIds = posts.Select(p => p.Id).ToList();
        var likeCounts = await forumRepository.GetActiveLikeCountsForPostsAsync(postIds, cancellationToken);
        var likedPostIds = await forumRepository.GetActiveLikedPostIdsByUserAsync(userId, postIds, cancellationToken);
        var commentCounts = await forumRepository.GetCommentCountsForPostsAsync(postIds, cancellationToken);

        var items = posts.Select(post =>
        {
            envelopeById.TryGetValue(post.Id.ToString(), out var envelope);
            likeCounts.TryGetValue(post.Id, out var likeCount);
            commentCounts.TryGetValue(post.Id, out var commentCount);
            return ForumMapper.MapListItem(
                post,
                envelope,
                likeCount,
                likedPostIds.Contains(post.Id),
                commentCount);
        }).ToList();

        return new ForumListResponse
        {
            Success = true,
            Message = "Forum posts loaded.",
            Items = items,
            HasMore = page.HasMore
        };
    }
}
