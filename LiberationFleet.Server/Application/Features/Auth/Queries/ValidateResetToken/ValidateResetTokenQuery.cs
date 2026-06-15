using LiberationFleet.Server.Application.Features.Auth.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Queries.ValidateResetToken;

public class ValidateResetTokenQuery : IRequest<ValidateResetTokenResponse>
{
    public string Token { get; set; } = string.Empty;
}
