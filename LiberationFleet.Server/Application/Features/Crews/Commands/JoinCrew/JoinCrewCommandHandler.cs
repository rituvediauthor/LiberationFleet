using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;

public class JoinCrewCommandHandler : IRequestHandler<JoinCrewCommand, CrewOperationResponse>
{
    private readonly ICrewRepository _crewRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public JoinCrewCommandHandler(
        ICrewRepository crewRepository,
        ICrewMembershipRepository membershipRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _crewRepository = crewRepository;
        _membershipRepository = membershipRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CrewOperationResponse> Handle(JoinCrewCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return new CrewOperationResponse { Success = false, Message = "Unauthorized" };
        }

        var existingMembership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
        if (existingMembership is not null)
        {
            return new CrewOperationResponse { Success = false, Message = "You are already a member of a crew" };
        }

        var crew = !string.IsNullOrWhiteSpace(request.JoinCode)
            ? await _crewRepository.GetByJoinCodeAsync(request.JoinCode.Trim().ToUpperInvariant(), cancellationToken)
            : await _crewRepository.GetByIdAsync(request.CrewId!.Value, cancellationToken);

        if (crew is null)
        {
            return new CrewOperationResponse
            {
                Success = false,
                Message = !string.IsNullOrWhiteSpace(request.JoinCode)
                    ? "No crew found with that join code"
                    : "Crew not found"
            };
        }

        if (await _membershipRepository.IsUserBannedFromCrewAsync(userId.Value, crew.Id, cancellationToken))
        {
            return new CrewOperationResponse { Success = false, Message = "You are banned from this crew" };
        }

        var memberCount = await _crewRepository.CountMembersAsync(crew.Id, cancellationToken);
        if (memberCount >= crew.MaxSize)
        {
            return new CrewOperationResponse { Success = false, Message = "This crew is full" };
        }

        await _membershipRepository.AddAsync(new CrewMembership
        {
            UserId = userId.Value,
            CrewId = crew.Id,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewOperationResponse
        {
            Success = true,
            Message = "Joined crew successfully",
            Crew = new CrewDto
            {
                Id = crew.Id,
                Name = crew.Name,
                MaxSize = crew.MaxSize,
                MemberCount = memberCount + 1,
                Privacy = crew.Privacy.ToString(),
                Scope = crew.Scope.ToString(),
                ZipCode = crew.ZipCode,
                RadiusMiles = crew.RadiusMiles,
                JoinCode = crew.JoinCode
            }
        };
    }
}
