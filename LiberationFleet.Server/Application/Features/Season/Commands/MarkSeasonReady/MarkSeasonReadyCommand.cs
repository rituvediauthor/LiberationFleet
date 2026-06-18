using LiberationFleet.Server.Application.Common.Interfaces;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Season.Commands.MarkSeasonReady;

public record MarkSeasonReadyCommand : IRequest<SeasonReadyResultDto>;

public class MarkSeasonReadyCommandHandler(IMutualAidService mutualAidService, ICurrentUserService currentUser)
    : IRequestHandler<MarkSeasonReadyCommand, SeasonReadyResultDto>
{
    public async Task<SeasonReadyResultDto> Handle(MarkSeasonReadyCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SeasonReadyResultDto { Success = false, Message = "Unauthorized." };
        }

        return await mutualAidService.MarkSeasonReadyAsync(currentUser.UserId.Value, cancellationToken);
    }
}
