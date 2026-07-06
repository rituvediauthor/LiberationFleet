using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Activity.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Activity.Queries.GetUserActivity;

public record GetUserActivityQuery(
    string Category = "All",
    DateTime? BeforeCreatedAt = null,
    string? BeforeKey = null,
    int Limit = 50) : IRequest<UserActivityListResponse>;

public class GetUserActivityQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserActivityRepository activityRepository) : IRequestHandler<GetUserActivityQuery, UserActivityListResponse>
{
    public async Task<UserActivityListResponse> Handle(GetUserActivityQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new UserActivityListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var category = ParseCategory(request.Category);
        var limit = Math.Clamp(request.Limit, 1, 100);

        var records = await activityRepository.GetUserActivitiesAsync(
            userId,
            category,
            request.BeforeCreatedAt,
            request.BeforeKey,
            limit,
            cancellationToken);

        var hasMore = records.Count > limit;
        var page = hasMore ? records.Take(limit).ToList() : records;

        var accessibleCrewIds = new HashSet<int>();
        foreach (var crewId in page.Select(item => item.CrewId).Distinct())
        {
            if (await membershipRepository.IsUserInCrewAsync(userId, crewId, cancellationToken))
            {
                accessibleCrewIds.Add(crewId);
            }
        }

        return new UserActivityListResponse
        {
            Success = true,
            Message = "Activity loaded.",
            HasMore = hasMore,
            Items = page.Select(record => new UserActivityItemDto
            {
                Key = record.Key,
                Kind = record.Kind,
                Category = record.Category,
                Label = record.Label,
                Detail = string.IsNullOrWhiteSpace(record.Detail) ? null : record.Detail,
                CreatedAt = record.CreatedAt,
                CrewId = record.CrewId,
                ResourceId = record.ResourceId,
                ParentResourceId = record.ParentResourceId,
                RelatedUserId = record.RelatedUserId,
                ChatRoomType = record.ChatRoomType,
                LibraryUnitId = record.LibraryUnitId,
                IsAccessible = record.ResourceExists && accessibleCrewIds.Contains(record.CrewId)
            }).ToList()
        };
    }

    private static UserActivityFilterCategory ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return UserActivityFilterCategory.All;
        }

        return category.Trim().ToLowerInvariant() switch
        {
            "chats" or "chat" => UserActivityFilterCategory.Chats,
            "forums" or "forum" => UserActivityFilterCategory.Forums,
            "library" or "libraryofthings" or "library-of-things" => UserActivityFilterCategory.Library,
            "gifts" or "gift" => UserActivityFilterCategory.Gifts,
            "proposals" or "proposal" => UserActivityFilterCategory.Proposals,
            _ => UserActivityFilterCategory.All
        };
    }
}
