using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Queries.GetSecurityAlerts;

public record GetSecurityAlertsQuery : IRequest<SecurityAlertsResponse>;

public class GetSecurityAlertsQueryHandler(
    ICurrentUserService currentUser,
    ISecurityRepository securityRepository) : IRequestHandler<GetSecurityAlertsQuery, SecurityAlertsResponse>
{
    public async Task<SecurityAlertsResponse> Handle(GetSecurityAlertsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SecurityAlertsResponse { Success = false, Message = "Unauthorized." };
        }

        var alerts = await securityRepository.GetAlertsForUserAsync(currentUser.UserId.Value, cancellationToken);
        return new SecurityAlertsResponse
        {
            Success = true,
            Message = "Security alerts loaded.",
            Alerts = alerts.Select(alert => new SecurityAlertDto
            {
                Id = alert.Id,
                AlertType = alert.AlertType,
                Title = alert.Title,
                Message = alert.Message,
                OccurredAt = alert.OccurredAt,
                IsRead = alert.IsRead,
                RelatedDeviceId = alert.RelatedDeviceId,
                RelatedDeviceName = alert.RelatedDevice?.DisplayName,
                CanManageDevice = alert.AlertType == SecurityAlertType.StrangeDeviceSignIn && alert.RelatedDeviceId.HasValue
            }).ToList()
        };
    }
}
