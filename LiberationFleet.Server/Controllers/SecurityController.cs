using LiberationFleet.Server.Application.Features.Security.Commands.ChangePassword;
using LiberationFleet.Server.Application.Features.Security.Commands.ManageDevice;
using LiberationFleet.Server.Application.Features.Security.Commands.UpdateSecuritySettings;
using LiberationFleet.Server.Application.Features.Security.Commands.VerifySettingsPassword;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using LiberationFleet.Server.Application.Features.Security.Queries.GetRegisteredDevices;
using LiberationFleet.Server.Application.Features.Security.Queries.GetSecurityAlerts;
using LiberationFleet.Server.Application.Features.Security.Queries.GetSecuritySettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/security")]
[Authorize]
public class SecurityController(IMediator mediator) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var result = await mediator.Send(new GetSecuritySettingsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSecuritySettingsRequest body)
    {
        var result = await mediator.Send(new UpdateSecuritySettingsCommand(body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var result = await mediator.Send(new GetSecurityAlertsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("alerts/{alertId:int}/read")]
    public async Task<IActionResult> MarkAlertRead(int alertId)
    {
        var result = await mediator.Send(new MarkSecurityAlertReadCommand(alertId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices([FromQuery] string? currentDeviceId = null)
    {
        var result = await mediator.Send(new GetRegisteredDevicesQuery(currentDeviceId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("devices/{deviceId:int}/trust")]
    public async Task<IActionResult> TrustDevice(int deviceId)
    {
        var result = await mediator.Send(new TrustDeviceCommand(deviceId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("devices/{deviceId:int}/block")]
    public async Task<IActionResult> BlockDevice(int deviceId)
    {
        var result = await mediator.Send(new BlockDeviceCommand(deviceId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body)
    {
        var result = await mediator.Send(new ChangePasswordCommand(body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("verify-settings-password")]
    public async Task<IActionResult> VerifySettingsPassword([FromBody] VerifySettingsPasswordRequest body)
    {
        var result = await mediator.Send(new VerifySettingsPasswordCommand(body.SettingsPassword));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
