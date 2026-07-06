using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.AddPlaceholderCrewmate;

public record AddPlaceholderCrewmateCommand(
    string Name,
    IReadOnlyList<PaymentPlatformAccountDto> PaymentPlatforms) : IRequest<AddPlaceholderCrewmateResponse>;

public class AddPlaceholderCrewmateCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    PlaceholderCrewmateService placeholderCrewmateService,
    IUnitOfWork unitOfWork) : IRequestHandler<AddPlaceholderCrewmateCommand, AddPlaceholderCrewmateResponse>
{
    public async Task<AddPlaceholderCrewmateResponse> Handle(
        AddPlaceholderCrewmateCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new AddPlaceholderCrewmateResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new AddPlaceholderCrewmateResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!membership.IsInSeason)
        {
            return new AddPlaceholderCrewmateResponse
            {
                Success = false,
                Message = "You must be in an active season to add a non-member."
            };
        }

        var result = await placeholderCrewmateService.AddPlaceholderAsync(
            membership.CrewId,
            userId,
            request.Name,
            request.PaymentPlatforms,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddPlaceholderCrewmateResponse
        {
            Success = result.Success,
            Message = result.Message,
            UserId = result.UserId
        };
    }
}
