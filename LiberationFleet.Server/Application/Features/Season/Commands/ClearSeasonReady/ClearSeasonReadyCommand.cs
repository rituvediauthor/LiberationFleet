using LiberationFleet.Server.Application.Common.Interfaces;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Season.Commands.ClearSeasonReady;

public record ClearSeasonReadyCommand : IRequest<SeasonSetupSaveResultDto>;

public class ClearSeasonReadyCommandHandler(IMutualAidService mutualAidService, ICurrentUserService currentUser)
    : IRequestHandler<ClearSeasonReadyCommand, SeasonSetupSaveResultDto>
{
    public async Task<SeasonSetupSaveResultDto> Handle(ClearSeasonReadyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SeasonSetupSaveResultDto { Success = false, Message = "Unauthorized." };
        }

        return await mutualAidService.ClearSeasonReadyAsync(currentUser.UserId.Value, cancellationToken);
    }
}
