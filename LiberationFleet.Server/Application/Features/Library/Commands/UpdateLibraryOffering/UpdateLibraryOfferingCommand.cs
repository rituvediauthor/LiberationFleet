using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.UpdateLibraryOffering;

public record UpdateLibraryOfferingCommand(int OfferingId, bool? IsOutOfStock)
    : IRequest<LibraryOfferingOperationResponse>;

public class UpdateLibraryOfferingCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateLibraryOfferingCommand, LibraryOfferingOperationResponse>
{
    public async Task<LibraryOfferingOperationResponse> Handle(
        UpdateLibraryOfferingCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var offering = await libraryRepository.GetTrackedOfferingByIdAsync(request.OfferingId, cancellationToken);
        if (offering is null || offering.CrewId != membership.CrewId)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Offering not found." };
        }

        if (!LibraryUnitAccess.CanEditOffering(offering, userId))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "You cannot edit this offering." };
        }

        if (request.IsOutOfStock.HasValue)
        {
            if (!offering.QuantityNotApplicable)
            {
                return new LibraryOfferingOperationResponse
                {
                    Success = false,
                    Message = "Only variable-quantity offerings can be manually marked out of stock."
                };
            }

            offering.IsOutOfStock = request.IsOutOfStock.Value;
            offering.UpdatedAt = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new LibraryOfferingOperationResponse
        {
            Success = true,
            Message = "Offering updated.",
            OfferingId = offering.Id
        };
    }
}
