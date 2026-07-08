using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Commands.ManageDevice;

public record TrustDeviceCommand(int DeviceId) : IRequest<SecurityOperationResponse>;
public record BlockDeviceCommand(int DeviceId) : IRequest<SecurityOperationResponse>;
public record MarkSecurityAlertReadCommand(int AlertId) : IRequest<SecurityOperationResponse>;

public class TrustDeviceCommandHandler(
    ICurrentUserService currentUser,
    ISecurityRepository securityRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<TrustDeviceCommand, SecurityOperationResponse>
{
    public async Task<SecurityOperationResponse> Handle(TrustDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SecurityOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var device = await securityRepository.GetDeviceByIdAsync(currentUser.UserId.Value, request.DeviceId, cancellationToken);
        if (device is null)
        {
            return new SecurityOperationResponse { Success = false, Message = "Device not found." };
        }

        device.IsTrusted = true;
        device.IsBlocked = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SecurityOperationResponse { Success = true, Message = "Device registered as trusted." };
    }
}

public class BlockDeviceCommandHandler(
    ICurrentUserService currentUser,
    ISecurityRepository securityRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<BlockDeviceCommand, SecurityOperationResponse>
{
    public async Task<SecurityOperationResponse> Handle(BlockDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SecurityOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var device = await securityRepository.GetDeviceByIdAsync(currentUser.UserId.Value, request.DeviceId, cancellationToken);
        if (device is null)
        {
            return new SecurityOperationResponse { Success = false, Message = "Device not found." };
        }

        device.IsBlocked = true;
        device.IsTrusted = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SecurityOperationResponse { Success = true, Message = "Device blocked." };
    }
}

public class MarkSecurityAlertReadCommandHandler(
    ICurrentUserService currentUser,
    ISecurityRepository securityRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<MarkSecurityAlertReadCommand, SecurityOperationResponse>
{
    public async Task<SecurityOperationResponse> Handle(MarkSecurityAlertReadCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SecurityOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var alert = await securityRepository.GetAlertByIdAsync(currentUser.UserId.Value, request.AlertId, cancellationToken);
        if (alert is null)
        {
            return new SecurityOperationResponse { Success = false, Message = "Alert not found." };
        }

        alert.IsRead = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SecurityOperationResponse { Success = true, Message = "Alert marked as read." };
    }
}
