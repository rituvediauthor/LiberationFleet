using System.Security.Claims;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Application.Features.Security;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateJwtToken(User user, int? registeredDeviceId = null);
    ClaimsPrincipal? ValidateJwtToken(string token);
}
