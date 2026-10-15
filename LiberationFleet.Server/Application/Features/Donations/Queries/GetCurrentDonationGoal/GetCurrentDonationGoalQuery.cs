using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Donations.Queries.GetCurrentDonationGoal;

public record GetCurrentDonationGoalQuery : IRequest<DonationGoalCurrentDto>;

public class GetCurrentDonationGoalQueryHandler(IDonationGoalProgressService goalProgress)
    : IRequestHandler<GetCurrentDonationGoalQuery, DonationGoalCurrentDto>
{
    public Task<DonationGoalCurrentDto> Handle(
        GetCurrentDonationGoalQuery request,
        CancellationToken cancellationToken) =>
        goalProgress.GetCurrentAsync(cancellationToken: cancellationToken);
}
