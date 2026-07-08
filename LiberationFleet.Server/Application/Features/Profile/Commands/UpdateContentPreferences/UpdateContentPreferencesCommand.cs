using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Features.Security;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateContentPreferences;

public record UpdateContentPreferencesCommand(AdultContentPreference AdultContentPreference, string? SettingsPassword)
    : IRequest<ContentPreferencesResponse>;

public class UpdateContentPreferencesCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    ISecurityRepository securityRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateContentPreferencesCommand, ContentPreferencesResponse>
{
    public async Task<ContentPreferencesResponse> Handle(
        UpdateContentPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ContentPreferencesResponse { Success = false, Message = "Unauthorized." };
        }

        if (!Enum.IsDefined(request.AdultContentPreference))
        {
            return new ContentPreferencesResponse { Success = false, Message = "Invalid content preference." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return new ContentPreferencesResponse { Success = false, Message = "User not found." };
        }

        var lockCheck = await SettingsLockHelper.VerifySettingsPasswordAsync(user, request.SettingsPassword, passwordHasher);
        if (!lockCheck.Allowed)
        {
            return new ContentPreferencesResponse { Success = false, Message = lockCheck.Message };
        }

        user.AdultContentPreference = request.AdultContentPreference;
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await SettingsLockHelper.RecordSettingsChangedAlertAsync(
            user.Id,
            "Content",
            securityRepository,
            unitOfWork,
            cancellationToken);

        return new ContentPreferencesResponse
        {
            Success = true,
            Message = "Content preferences saved.",
            Preferences = new ContentPreferencesDto
            {
                AdultContentPreference = user.AdultContentPreference
            }
        };
    }
}
