using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.ProposeCrewmateAidStatChange;

public record ProposeCrewmateAidStatChangeCommand(
    int TargetUserId,
    IReadOnlyList<ProposeCrewmateAidStatChangeItemDto> Changes) : IRequest<CrewRoleChangeResponse>;

public class ProposeCrewmateAidStatChangeCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    CrewmateAidStatProposalService aidStatProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<ProposeCrewmateAidStatChangeCommand, CrewRoleChangeResponse>
{
    public async Task<CrewRoleChangeResponse> Handle(
        ProposeCrewmateAidStatChangeCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!CrewRoleAuthorizationService.CanProposeCrewmateAidStatEdits(viewerMembership))
        {
            return new CrewRoleChangeResponse
            {
                Success = false,
                Message = "Only organizers and accountants can propose aid statistic edits."
            };
        }

        var parsedChanges = new List<CrewmateAidStatChangeItem>();
        foreach (var item in request.Changes ?? [])
        {
            if (!Enum.TryParse<CrewmateAidStatField>(item.Field, ignoreCase: true, out var field))
            {
                return new CrewRoleChangeResponse
                {
                    Success = false,
                    Message = $"Unknown aid statistic field '{item.Field}'."
                };
            }

            parsedChanges.Add(new CrewmateAidStatChangeItem
            {
                Field = field,
                NewValue = item.NewValue ?? string.Empty
            });
        }

        var result = await aidStatProposalService.CreateAsync(
            viewerMembership.CrewId,
            currentUser.UserId.Value,
            request.TargetUserId,
            parsedChanges,
            cancellationToken);

        if (!result.Success)
        {
            return new CrewRoleChangeResponse
            {
                Success = false,
                Message = result.Message,
                ProposalId = result.ProposalId
            };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewRoleChangeResponse
        {
            Success = true,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
