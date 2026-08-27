using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Friends;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.ManageFriendship;

/// <summary>
/// Friendship is global (not scoped to a crew or fleet). These commands operate on the user-to-user friendship graph.
/// </summary>
public record RequestFriendshipCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record CancelFriendshipRequestCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record AcceptFriendshipCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record RejectFriendshipCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record UnfriendCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record BlockCrewmateCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record UnblockCrewmateCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;

public class RequestFriendshipCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<RequestFriendshipCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(RequestFriendshipCommand request, CancellationToken cancellationToken)
    {
        var access = await FriendAccessHelper.ValidateSocialTargetAsync(
            currentUser,
            userRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!access.Success)
        {
            return ManageFriendshipAccessMapping.Fail(access);
        }

        var existing = await friendshipRepository.GetBetweenUsersAsync(access.ViewerId, request.TargetUserId, cancellationToken);
        if (existing is not null)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = existing.Status == FriendshipStatus.Accepted
                    ? "You are already friends."
                    : "A friendship request already exists.",
                FriendshipState = CrewmateMapper.MapFriendshipState(
                    access.ViewerId,
                    request.TargetUserId,
                    existing,
                    false,
                    false)
            };
        }

        var friendship = new Friendship
        {
            RequesterUserId = access.ViewerId,
            AddresseeUserId = request.TargetUserId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        Friendship.SetPairIds(friendship);

        await friendshipRepository.AddAsync(friendship, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = request.TargetUserId,
            Kind = NotificationKind.FriendRequest,
            Title = NotificationService.GetKindLabel(NotificationKind.FriendRequest),
            Body = "You have a new friend request.",
            ActionUrl = "/app/friends/requests",
            ActorUserId = access.ViewerId
        }, cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "Friendship request sent.",
            FriendshipState = CrewmateFriendshipStateDto.RequestSent
        };
    }
}

public class CancelFriendshipRequestCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    INotificationRepository notificationRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelFriendshipRequestCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(CancelFriendshipRequestCommand request, CancellationToken cancellationToken)
    {
        var access = await FriendAccessHelper.ValidateSocialTargetAsync(
            currentUser,
            userRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!access.Success)
        {
            return ManageFriendshipAccessMapping.Fail(access);
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(access.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.RequesterUserId != access.ViewerId)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "No pending request to cancel.",
                FriendshipState = CrewmateFriendshipStateDto.None
            };
        }

        friendshipRepository.Remove(friendship);
        await ManageFriendshipNotificationHelper.MarkFriendRequestNotificationsReadAsync(
            notificationRepository,
            notificationService,
            access.ViewerId,
            request.TargetUserId,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "Friendship request cancelled.",
            FriendshipState = CrewmateFriendshipStateDto.None
        };
    }
}

public class AcceptFriendshipCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<AcceptFriendshipCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(AcceptFriendshipCommand request, CancellationToken cancellationToken)
    {
        var access = await FriendAccessHelper.ValidateSocialTargetAsync(
            currentUser,
            userRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!access.Success)
        {
            return ManageFriendshipAccessMapping.Fail(access);
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(access.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.AddresseeUserId != access.ViewerId)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "No pending request to accept.",
                FriendshipState = CrewmateFriendshipStateDto.None
            };
        }

        friendship.Status = FriendshipStatus.Accepted;
        friendship.RespondedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = request.TargetUserId,
            Kind = NotificationKind.FriendRequestAccepted,
            Title = NotificationService.GetKindLabel(NotificationKind.FriendRequestAccepted),
            Body = "Your friend request was accepted.",
            ActionUrl = "/app/friends",
            ActorUserId = access.ViewerId
        }, cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "Friendship accepted.",
            FriendshipState = CrewmateFriendshipStateDto.Friends
        };
    }
}

public class RejectFriendshipCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    INotificationRepository notificationRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<RejectFriendshipCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(RejectFriendshipCommand request, CancellationToken cancellationToken)
    {
        var access = await FriendAccessHelper.ValidateSocialTargetAsync(
            currentUser,
            userRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!access.Success)
        {
            return ManageFriendshipAccessMapping.Fail(access);
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(access.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.AddresseeUserId != access.ViewerId)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "No pending request to reject.",
                FriendshipState = CrewmateFriendshipStateDto.None
            };
        }

        friendshipRepository.Remove(friendship);
        await ManageFriendshipNotificationHelper.MarkFriendRequestNotificationsReadAsync(
            notificationRepository,
            notificationService,
            access.ViewerId,
            request.TargetUserId,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "Friendship request rejected.",
            FriendshipState = CrewmateFriendshipStateDto.None
        };
    }
}

public class UnfriendCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UnfriendCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(UnfriendCommand request, CancellationToken cancellationToken)
    {
        var access = await FriendAccessHelper.ValidateSocialTargetAsync(
            currentUser,
            userRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!access.Success)
        {
            return ManageFriendshipAccessMapping.Fail(access);
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(access.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "You are not friends with this user.",
                FriendshipState = CrewmateFriendshipStateDto.None
            };
        }

        friendshipRepository.Remove(friendship);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "Unfriended.",
            FriendshipState = CrewmateFriendshipStateDto.None
        };
    }
}

public class BlockCrewmateCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<BlockCrewmateCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(BlockCrewmateCommand request, CancellationToken cancellationToken)
    {
        var access = await FriendAccessHelper.ValidateSocialTargetAsync(
            currentUser,
            userRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken,
            allowBlocked: true);
        if (!access.Success)
        {
            return ManageFriendshipAccessMapping.Fail(access);
        }

        if (await blockRepository.IsBlockedAsync(access.ViewerId, request.TargetUserId, cancellationToken))
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "User is already blocked.",
                FriendshipState = CrewmateFriendshipStateDto.Blocked
            };
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(access.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is not null)
        {
            friendshipRepository.Remove(friendship);
        }

        await blockRepository.AddAsync(new UserBlock
        {
            BlockerUserId = access.ViewerId,
            BlockedUserId = request.TargetUserId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "User blocked.",
            FriendshipState = CrewmateFriendshipStateDto.Blocked
        };
    }
}

public class UnblockCrewmateCommandHandler(
    ICurrentUserService currentUser,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UnblockCrewmateCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(UnblockCrewmateCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        if (viewerId == request.TargetUserId)
        {
            return new CrewmateOperationResponse { Success = false, Message = "You cannot perform this action on yourself." };
        }

        var removed = await blockRepository.RemoveAsync(viewerId, request.TargetUserId, cancellationToken);
        if (!removed)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "This user is not blocked.",
                FriendshipState = CrewmateFriendshipStateDto.None
            };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "User unblocked.",
            FriendshipState = CrewmateFriendshipStateDto.None
        };
    }
}

file static class ManageFriendshipAccessMapping
{
    public static CrewmateOperationResponse Fail(FriendAccessResult access) =>
        new() { Success = false, Message = access.Message };
}

file static class ManageFriendshipNotificationHelper
{
    public static async Task MarkFriendRequestNotificationsReadAsync(
        INotificationRepository notificationRepository,
        NotificationService notificationService,
        int userA,
        int userB,
        CancellationToken cancellationToken)
    {
        await notificationRepository.MarkReadByKindAsync(
            userA,
            NotificationKind.FriendRequest,
            actorUserId: userB,
            cancellationToken);
        await notificationRepository.MarkReadByKindAsync(
            userB,
            NotificationKind.FriendRequest,
            actorUserId: userA,
            cancellationToken);

        await notificationService.PushBadgeSummaryAndGetAsync(userA, cancellationToken);
        await notificationService.PushBadgeSummaryAndGetAsync(userB, cancellationToken);
    }
}
