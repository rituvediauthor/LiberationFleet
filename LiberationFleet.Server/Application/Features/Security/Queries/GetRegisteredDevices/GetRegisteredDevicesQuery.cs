using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Queries.GetRegisteredDevices;

public record GetRegisteredDevicesQuery(string? CurrentDeviceId) : IRequest<RegisteredDevicesResponse>;

public class GetRegisteredDevicesQueryHandler(
    ICurrentUserService currentUser,
    ISecurityRepository securityRepository) : IRequestHandler<GetRegisteredDevicesQuery, RegisteredDevicesResponse>
{
    public async Task<RegisteredDevicesResponse> Handle(GetRegisteredDevicesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new RegisteredDevicesResponse { Success = false, Message = "Unauthorized." };
        }

        var devices = await securityRepository.GetDevicesForUserAsync(currentUser.UserId.Value, cancellationToken);
        var currentDeviceId = request.CurrentDeviceId?.Trim();

        return new RegisteredDevicesResponse
        {
            Success = true,
            Message = "Registered devices loaded.",
            Devices = devices.Select(device => new RegisteredDeviceDto
            {
                Id = device.Id,
                DeviceId = device.DeviceId,
                DisplayName = device.DisplayName,
                UserAgent = device.UserAgent,
                FirstSeenAt = device.FirstSeenAt,
                LastSeenAt = device.LastSeenAt,
                IsTrusted = device.IsTrusted,
                IsBlocked = device.IsBlocked,
                IsCurrent = !string.IsNullOrWhiteSpace(currentDeviceId) && device.DeviceId == currentDeviceId
            }).ToList()
        };
    }
}
