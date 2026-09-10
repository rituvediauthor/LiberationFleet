using LiberationFleet.Server.Application.Features.Profile.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommand : IRequest<ProfileOperationResponse>
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Null clears location. Ciphertext-only; server does not validate plaintext country/zip.</summary>
    public EncryptedLocationDto? EncryptedLocation { get; set; }
    public bool ClearLocation { get; set; }
    public string? AvatarResourceId { get; set; }
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; } = 1;
    public int DisabilityLevel { get; set; }
    public List<string> IdentityGroups { get; set; } = [];
    public bool NeedsSurvivalAid { get; set; }
    public List<PaymentPlatformAccountDto> PaymentPlatforms { get; set; } = [];
}
