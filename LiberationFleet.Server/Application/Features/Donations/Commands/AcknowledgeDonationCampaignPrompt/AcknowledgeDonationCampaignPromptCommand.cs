using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Donations.Commands.AcknowledgeDonationCampaignPrompt;

public record AcknowledgeDonationCampaignPromptCommand : IRequest<AcknowledgeDonationCampaignPromptResponse>;

public class AcknowledgeDonationCampaignPromptResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AcknowledgeDonationCampaignPromptCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AcknowledgeDonationCampaignPromptCommand, AcknowledgeDonationCampaignPromptResponse>
{
    public async Task<AcknowledgeDonationCampaignPromptResponse> Handle(
        AcknowledgeDonationCampaignPromptCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new AcknowledgeDonationCampaignPromptResponse { Success = false, Message = "Unauthorized." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return new AcknowledgeDonationCampaignPromptResponse { Success = false, Message = "User not found." };
        }

        user.LastDonationCampaignPromptAt = DateTime.UtcNow;

        if (user.DonationCampaignPhaseTarget == 0)
        {
            user.DonationCampaignPhaseTarget = Random.Shared.Next(2, 5);
            user.DonationCampaignUrgencyPhase = 0;
            user.DonationCampaignPhaseShownCount = 0;
        }

        user.DonationCampaignPhaseShownCount++;

        if (user.DonationCampaignPhaseShownCount >= user.DonationCampaignPhaseTarget)
        {
            user.DonationCampaignPhaseShownCount = 0;
            user.DonationCampaignUrgencyPhase = (user.DonationCampaignUrgencyPhase + 1) % 3;
            user.DonationCampaignPhaseTarget = Random.Shared.Next(2, 5);
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcknowledgeDonationCampaignPromptResponse { Success = true, Message = "Recorded." };
    }
}
