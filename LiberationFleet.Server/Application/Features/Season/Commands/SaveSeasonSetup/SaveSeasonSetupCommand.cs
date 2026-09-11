using LiberationFleet.Server.Application.Common.Interfaces;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Season.Commands.SaveSeasonSetup;

public class SaveSeasonSetupCommand : IRequest<SeasonSetupSaveResultDto>
{
    /// <summary>May be zero — recipients can join without pledging a positive estimate.</summary>
    public decimal EstimatedMonthlyContribution { get; set; }
}

public class SaveSeasonSetupCommandHandler(IMutualAidService mutualAidService, ICurrentUserService currentUser)
    : IRequestHandler<SaveSeasonSetupCommand, SeasonSetupSaveResultDto>
{
    public async Task<SeasonSetupSaveResultDto> Handle(SaveSeasonSetupCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SeasonSetupSaveResultDto { Success = false, Message = "Unauthorized." };
        }

        return await mutualAidService.SaveSeasonSetupAsync(
            currentUser.UserId.Value,
            request.EstimatedMonthlyContribution,
            cancellationToken);
    }
}
