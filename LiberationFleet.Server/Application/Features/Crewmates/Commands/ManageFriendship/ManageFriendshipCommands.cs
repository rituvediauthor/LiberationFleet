using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.ManageFriendship;

public record RequestFriendshipCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record CancelFriendshipRequestCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record AcceptFriendshipCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record RejectFriendshipCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record UnfriendCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;
public record BlockCrewmateCommand(int TargetUserId) : IRequest<CrewmateOperationResponse>;

public class RequestFriendshipCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RequestFriendshipCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(RequestFriendshipCommand request, CancellationToken cancellationToken)
    {
        var context = await CrewmateCommandHelper.ValidateCrewmateTargetAsync(
            currentUser,
            membershipRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!context.Success)
        {
            return context.ToOperationResponse();
        }

        var existing = await friendshipRepository.GetBetweenUsersAsync(context.ViewerId, request.TargetUserId, cancellationToken);
        if (existing is not null)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = existing.Status == FriendshipStatus.Accepted
                    ? "You are already friends."
                    : "A friendship request already exists.",
                FriendshipState = CrewmateMapper.MapFriendshipState(
                    context.ViewerId,
                    request.TargetUserId,
                    existing,
                    false,
                    false)
            };
        }

        var friendship = new Friendship
        {
            RequesterUserId = context.ViewerId,
            AddresseeUserId = request.TargetUserId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await friendshipRepository.AddAsync(friendship, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

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
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelFriendshipRequestCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(CancelFriendshipRequestCommand request, CancellationToken cancellationToken)
    {
        var context = await CrewmateCommandHelper.ValidateCrewmateTargetAsync(
            currentUser,
            membershipRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!context.Success)
        {
            return context.ToOperationResponse();
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(context.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.RequesterUserId != context.ViewerId)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "No pending request to cancel.",
                FriendshipState = CrewmateFriendshipStateDto.None
            };
        }

        friendshipRepository.Remove(friendship);
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
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AcceptFriendshipCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(AcceptFriendshipCommand request, CancellationToken cancellationToken)
    {
        var context = await CrewmateCommandHelper.ValidateCrewmateTargetAsync(
            currentUser,
            membershipRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!context.Success)
        {
            return context.ToOperationResponse();
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(context.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.AddresseeUserId != context.ViewerId)
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
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RejectFriendshipCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(RejectFriendshipCommand request, CancellationToken cancellationToken)
    {
        var context = await CrewmateCommandHelper.ValidateCrewmateTargetAsync(
            currentUser,
            membershipRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!context.Success)
        {
            return context.ToOperationResponse();
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(context.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null
            || friendship.Status != FriendshipStatus.Pending
            || friendship.AddresseeUserId != context.ViewerId)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "No pending request to reject.",
                FriendshipState = CrewmateFriendshipStateDto.None
            };
        }

        friendshipRepository.Remove(friendship);
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
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UnfriendCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(UnfriendCommand request, CancellationToken cancellationToken)
    {
        var context = await CrewmateCommandHelper.ValidateCrewmateTargetAsync(
            currentUser,
            membershipRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken);
        if (!context.Success)
        {
            return context.ToOperationResponse();
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(context.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "You are not friends with this crewmate.",
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
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<BlockCrewmateCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(BlockCrewmateCommand request, CancellationToken cancellationToken)
    {
        var context = await CrewmateCommandHelper.ValidateCrewmateTargetAsync(
            currentUser,
            membershipRepository,
            blockRepository,
            request.TargetUserId,
            cancellationToken,
            allowBlocked: true);
        if (!context.Success)
        {
            return context.ToOperationResponse();
        }

        if (await blockRepository.IsBlockedAsync(context.ViewerId, request.TargetUserId, cancellationToken))
        {
            return new CrewmateOperationResponse
            {
                Success = false,
                Message = "Crewmate is already blocked.",
                FriendshipState = CrewmateFriendshipStateDto.Blocked
            };
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(context.ViewerId, request.TargetUserId, cancellationToken);
        if (friendship is not null)
        {
            friendshipRepository.Remove(friendship);
        }

        await blockRepository.AddAsync(new UserBlock
        {
            BlockerUserId = context.ViewerId,
            BlockedUserId = request.TargetUserId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = "Crewmate blocked.",
            FriendshipState = CrewmateFriendshipStateDto.Blocked
        };
    }
}

internal static class CrewmateCommandHelper
{
    public static async Task<CrewmateCommandContext> ValidateCrewmateTargetAsync(
        ICurrentUserService currentUser,
        ICrewMembershipRepository membershipRepository,
        IUserBlockRepository blockRepository,
        int targetUserId,
        CancellationToken cancellationToken,
        bool allowBlocked = false)
    {
        if (!currentUser.UserId.HasValue)
        {
            return CrewmateCommandContext.Fail("Unauthorized.");
        }

        var viewerId = currentUser.UserId.Value;
        if (viewerId == targetUserId)
        {
            return CrewmateCommandContext.Fail("You cannot perform this action on yourself.");
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (membership is null)
        {
            return CrewmateCommandContext.Fail("You are not in a crew.");
        }

        if (!await membershipRepository.IsUserInCrewAsync(targetUserId, membership.CrewId, cancellationToken))
        {
            return CrewmateCommandContext.Fail("Crewmate not found.");
        }

        if (!allowBlocked && await blockRepository.IsBlockedAsync(viewerId, targetUserId, cancellationToken))
        {
            return CrewmateCommandContext.Fail("You have blocked this crewmate.");
        }

        return CrewmateCommandContext.Ok(viewerId);
    }
}

internal sealed class CrewmateCommandContext
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ViewerId { get; init; }

    public static CrewmateCommandContext Ok(int viewerId) =>
        new() { Success = true, ViewerId = viewerId };

    public static CrewmateCommandContext Fail(string message) =>
        new() { Success = false, Message = message };

    public CrewmateOperationResponse ToOperationResponse() =>
        new() { Success = false, Message = Message };
}
