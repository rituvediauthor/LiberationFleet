using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Queries.GetContentPreferences;

public record GetContentPreferencesQuery() : IRequest<ContentPreferencesResponse>;

public class GetContentPreferencesQueryHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository) : IRequestHandler<GetContentPreferencesQuery, ContentPreferencesResponse>
{
    public async Task<ContentPreferencesResponse> Handle(GetContentPreferencesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ContentPreferencesResponse { Success = false, Message = "Unauthorized." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return new ContentPreferencesResponse { Success = false, Message = "User not found." };
        }

        return new ContentPreferencesResponse
        {
            Success = true,
            Message = "Content preferences loaded.",
            Preferences = new ContentPreferencesDto
            {
                AdultContentPreference = user.AdultContentPreference
            }
        };
    }
}
