namespace LiberationFleet.Server.Application.Features.Security;

public static class SecurityStampHelper
{
    public const string SecurityStampClaimType = "security_stamp";
    public const string DeviceIdClaimType = "device_id";

    public static string CreateNew() => Guid.NewGuid().ToString("N");

    public static void EnsureStamp(Domain.Entities.User user)
    {
        if (string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            user.SecurityStamp = CreateNew();
        }
    }

    public static void Bump(Domain.Entities.User user) => user.SecurityStamp = CreateNew();
}
