using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Season.Commands.ProposeStartSeason;

public record ProposeStartSeasonCommand : IRequest<ProposeStartSeasonResponse>;

public class ProposeStartSeasonResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProposalId { get; set; }
}

public class ProposeStartSeasonCommandHandler(
    ICurrentUserService currentUser,
    CrewStartSeasonProposalService startSeasonProposalService)
    : IRequestHandler<ProposeStartSeasonCommand, ProposeStartSeasonResponse>
{
    public async Task<ProposeStartSeasonResponse> Handle(
        ProposeStartSeasonCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposeStartSeasonResponse { Success = false, Message = "Unauthorized." };
        }

        var result = await startSeasonProposalService.CreateAsync(currentUser.UserId.Value, cancellationToken);
        return new ProposeStartSeasonResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
