using System.Security.Cryptography;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;

public class CreateCrewCommandHandler : IRequestHandler<CreateCrewCommand, CrewOperationResponse>
{
    private readonly ICrewRepository _crewRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCrewCommandHandler(
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

    public async Task<CrewOperationResponse> Handle(CreateCrewCommand request, CancellationToken cancellationToken)
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

        var privacy = Enum.Parse<CrewPrivacy>(request.Privacy, ignoreCase: true);
        var scope = Enum.Parse<CrewScope>(request.Scope, ignoreCase: true);

        var crew = new Crew
        {
            Name = request.Name.Trim(),
            MaxSize = request.MaxSize,
            Privacy = privacy,
            Scope = scope,
            ZipCode = scope == CrewScope.Local ? request.ZipCode : null,
            RadiusMiles = scope == CrewScope.Local ? request.RadiusMiles : null,
            JoinCode = await GenerateUniqueJoinCodeAsync(cancellationToken),
            CreatedByUserId = userId.Value,
            CreatedAt = DateTime.UtcNow
        };

        await _crewRepository.AddAsync(crew, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _membershipRepository.AddAsync(new CrewMembership
        {
            UserId = userId.Value,
            CrewId = crew.Id,
            IsBanned = false,
            IsOrganizer = true,
            JoinedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewOperationResponse
        {
            Success = true,
            Message = "Crew created successfully",
            Crew = MapCrew(crew, 1)
        };
    }

    private async Task<string> GenerateUniqueJoinCodeAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateJoinCode();
            if (await _crewRepository.GetByJoinCodeAsync(code, cancellationToken) is null)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique join code.");
    }

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> result = stackalloc char[8];
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < 8; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
    }

    private static CrewDto MapCrew(Crew crew, int memberCount) => new()
    {
        Id = crew.Id,
        Name = crew.Name,
        MaxSize = crew.MaxSize,
        MemberCount = memberCount,
        Privacy = crew.Privacy.ToString(),
        Scope = crew.Scope.ToString(),
        ZipCode = crew.ZipCode,
        RadiusMiles = crew.RadiusMiles,
        JoinCode = crew.JoinCode
    };
}
