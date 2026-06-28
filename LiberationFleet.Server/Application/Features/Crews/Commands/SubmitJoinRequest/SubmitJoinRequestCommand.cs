using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.SubmitJoinRequest;

public record SubmitJoinRequestCommand(int? CrewId, string? JoinCode, IReadOnlyList<int> AcceptedRuleIds)
    : IRequest<JoinRequestOperationResponse>;

public class SubmitJoinRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewRepository crewRepository,
    IRuleRepository ruleRepository,
    CrewJoinRequestProposalService joinRequestProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<SubmitJoinRequestCommand, JoinRequestOperationResponse>
{
    public async Task<JoinRequestOperationResponse> Handle(
        SubmitJoinRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new JoinRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var crew = !string.IsNullOrWhiteSpace(request.JoinCode)
            ? await crewRepository.GetByJoinCodeAsync(request.JoinCode.Trim().ToUpperInvariant(), cancellationToken)
            : request.CrewId.HasValue
                ? await crewRepository.GetByIdAsync(request.CrewId.Value, cancellationToken)
                : null;

        if (crew is null)
        {
            return new JoinRequestOperationResponse
            {
                Success = false,
                Message = !string.IsNullOrWhiteSpace(request.JoinCode)
                    ? "No crew found with that join code"
                    : "Crew not found"
            };
        }

        var publicRules = await ruleRepository.GetPublicByCrewIdAsync(crew.Id, cancellationToken);
        var requiredRuleIds = publicRules.Select(r => r.Id).OrderBy(id => id).ToList();
        var acceptedRuleIds = request.AcceptedRuleIds.Distinct().OrderBy(id => id).ToList();

        if (!requiredRuleIds.SequenceEqual(acceptedRuleIds))
        {
            return new JoinRequestOperationResponse
            {
                Success = false,
                Message = "You must accept all public rules before requesting to join."
            };
        }

        var result = await joinRequestProposalService.CreateJoinRequestAsync(
            userId,
            crew.Id,
            acceptedRuleIds,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new JoinRequestOperationResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
