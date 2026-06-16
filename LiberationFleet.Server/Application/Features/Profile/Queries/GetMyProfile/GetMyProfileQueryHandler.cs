using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, UserProfileDto?>
{
    private readonly IUserRepository _userRepository;
    private readonly IGiftRepository _giftRepository;
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyProfileQueryHandler(
        IUserRepository userRepository,
        IGiftRepository giftRepository,
        ICrewMembershipRepository membershipRepository,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _giftRepository = giftRepository;
        _membershipRepository = membershipRepository;
        _currentUserService = currentUserService;
    }

    public async Task<UserProfileDto?> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return null;
        }

        var user = await _userRepository.GetByIdWithProfileAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var giftStats = await _giftRepository.GetUserGiftStatsAsync(userId.Value, cancellationToken);
        var membership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);

        return ProfileMapper.MapUser(user, giftStats, membership is not null);
    }
}
