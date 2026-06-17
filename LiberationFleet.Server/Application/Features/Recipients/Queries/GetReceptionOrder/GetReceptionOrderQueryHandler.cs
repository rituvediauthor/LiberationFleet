using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Recipients.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Recipients.Queries.GetReceptionOrder;

public class GetReceptionOrderQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IReceptionOrderService receptionOrderService) : IRequestHandler<GetReceptionOrderQuery, ReceptionOrderResponse>
{
    public async Task<ReceptionOrderResponse> Handle(GetReceptionOrderQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ReceptionOrderResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ReceptionOrderResponse { Success = false, Message = "You are not in a crew." };
        }

        var recipients = await receptionOrderService.GetOrderedRecipientsAsync(
            membership.CrewId, 
            userId, 
            request.Limit);

        var recipientDtos = recipients.Select(r => new RecipientNeedDto
        {
            UserId = r.UserId,
            Username = r.Username,
            AmountNeeded = r.AmountNeeded,
            IsSurvivalThreshold = r.IsSurvivalThreshold,
            ReceptionOrderPosition = r.ReceptionOrderPosition,
            CommonPaymentPlatforms = r.CommonPaymentPlatforms,
            SuggestedMiddlemanId = r.SuggestedMiddlemanId,
            SuggestedMiddlemanName = r.SuggestedMiddlemanName,
            PaymentNote = GetPaymentNote(r)
        }).ToList();

        return new ReceptionOrderResponse
        {
            Success = true,
            Message = "Reception order retrieved.",
            Recipients = recipientDtos
        };
    }

    private static string GetPaymentNote(RecipientNeed need)
    {
        if (need.CommonPaymentPlatforms.Any())
            return "Direct payment available";
        
        if (need.SuggestedMiddlemanId.HasValue)
            return $"Via {need.SuggestedMiddlemanName}";
        
        return "No suitable middleman";
    }
}
