using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.SetLibraryFavorite;

public record SetLibraryFavoriteCommand(int UnitId, bool Favorited) : IRequest<LibraryFavoriteOperationResponse>;

public class LibraryFavoriteOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Favorited { get; set; }
}

public class SetLibraryFavoriteCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<SetLibraryFavoriteCommand, LibraryFavoriteOperationResponse>
{
    public async Task<LibraryFavoriteOperationResponse> Handle(
        SetLibraryFavoriteCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryFavoriteOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.UnitId <= 0)
        {
            return new LibraryFavoriteOperationResponse { Success = false, Message = "Item not found." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryFavoriteOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!request.Favorited)
        {
            await libraryRepository.RemoveFavoriteAsync(userId, request.UnitId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new LibraryFavoriteOperationResponse
            {
                Success = true,
                Message = "Removed from favorites.",
                Favorited = false
            };
        }

        var crewIds = await LibraryScopeHelper.GetAccessibleCrewIdsAsync(
            membership.CrewId,
            fleetRepository,
            cancellationToken);
        var unit = await libraryRepository.GetUnitByIdForCrewIdsAsync(
            request.UnitId,
            crewIds,
            membership.CrewId,
            cancellationToken);
        if (unit is null)
        {
            return new LibraryFavoriteOperationResponse { Success = false, Message = "Item not found." };
        }

        if (!await libraryRepository.IsUnitFavoritedAsync(userId, request.UnitId, cancellationToken))
        {
            await libraryRepository.AddFavoriteAsync(new UserLibraryFavorite
            {
                UserId = userId,
                LibraryUnitId = request.UnitId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new LibraryFavoriteOperationResponse
        {
            Success = true,
            Message = "Saved to favorites.",
            Favorited = true
        };
    }
}
