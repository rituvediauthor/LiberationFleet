using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.AccountRecovery.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.AccountRecovery.Commands.BeginAccountRecovery;

public record BeginAccountRecoveryCommand(BeginAccountRecoveryRequest Request)
    : IRequest<AccountRecoveryChallengeResponse>;

public class BeginAccountRecoveryCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    ICrewMembershipRepository membershipRepository,
    IEmailMfaService emailMfaService) : IRequestHandler<BeginAccountRecoveryCommand, AccountRecoveryChallengeResponse>
{
    public async Task<AccountRecoveryChallengeResponse> Handle(
        BeginAccountRecoveryCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        var newUserId = currentUser.UserId.Value;
        var oldUserId = request.Request.OldUserId;
        if (oldUserId <= 0 || oldUserId == newUserId)
        {
            return Fail("Select a different crewmate account to recover.");
        }

        var validation = await AccountRecoveryAccess.ValidateSameCrewAsync(
            membershipRepository,
            userRepository,
            newUserId,
            oldUserId,
            cancellationToken);
        if (!validation.Success || validation.OldUser is null)
        {
            return Fail(validation.Message);
        }

        var sent = await emailMfaService.CreateAndSendAsync(
            validation.OldUser,
            EmailMfaPurpose.AccountRecovery,
            null,
            null,
            null,
            cancellationToken);

        if (!sent.Success || string.IsNullOrWhiteSpace(sent.ChallengeToken))
        {
            return Fail(sent.Message);
        }

        return new AccountRecoveryChallengeResponse
        {
            Success = true,
            Message = sent.Message,
            ChallengeToken = sent.ChallengeToken,
            MaskedEmail = MaskEmail(validation.OldUser.Email),
            OldUsername = validation.OldUser.Username
        };
    }

    private static AccountRecoveryChallengeResponse Fail(string message) =>
        new() { Success = false, Message = message };

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return email;
        }

        return $"{email[0]}***{email[at..]}";
    }
}
