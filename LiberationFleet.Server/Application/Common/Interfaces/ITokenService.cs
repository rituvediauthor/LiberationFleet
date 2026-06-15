using System.Security.Claims;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateJwtToken(User user);
    ClaimsPrincipal? ValidateJwtToken(string token);
}
