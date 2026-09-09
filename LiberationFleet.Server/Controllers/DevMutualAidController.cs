using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/dev/mutual-aid")]
public class DevMutualAidController : ControllerBase
{
    private readonly IMutualAidDevService _devService;
    private readonly DevEnvironmentResetService _resetService;
    private readonly ICurrentUserService _currentUser;
    private readonly IWebHostEnvironment _environment;

    public DevMutualAidController(
        IMutualAidDevService devService,
        DevEnvironmentResetService resetService,
        ICurrentUserService currentUser,
        IWebHostEnvironment environment)
    {
        _devService = devService;
        _resetService = resetService;
        _currentUser = currentUser;
        _environment = environment;
    }

    [HttpGet("enabled")]
    [AllowAnonymous]
    public IActionResult GetEnabled()
    {
        return Ok(new { enabled = IsDevToolsEnabled() });
    }

    [HttpPost("new-month")]
    [Authorize]
    public Task<IActionResult> NewMonth() => RunAsync(_devService.SimulateNewMonthAsync);

    [HttpPost("new-season")]
    [Authorize]
    public Task<IActionResult> NewSeason() => RunAsync(_devService.SimulateNewSeasonAsync);

    [HttpPost("complete-cycles")]
    [Authorize]
    public Task<IActionResult> CompleteCycles() => RunAsync(_devService.CompleteAllCyclesAsync);

    [HttpPost("reset-season")]
    [Authorize]
    public Task<IActionResult> ResetSeason() => RunAsync(_devService.ResetSeasonAsync);

    [HttpPost("recalculate-caps")]
    [Authorize]
    public Task<IActionResult> RecalculateCaps() => RunAsync(_devService.RecalculateCapsAsync);

    [HttpPost("reset-app")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetApp()
    {
        if (!IsDevToolsEnabled())
        {
            return NotFound();
        }

        await _resetService.ResetAsync(HttpContext.RequestAborted);
        return Ok(new DevActionResultDto
        {
            Success = true,
            Message = "All app data and deep-freeze storage were cleared. The app is back to first-run state."
        });
    }

    private bool IsDevToolsEnabled() =>
        _environment.IsDevelopment() || _environment.IsStaging();

    private async Task<IActionResult> RunAsync(Func<int, CancellationToken, Task<DevActionResultDto>> action)
    {
        if (!IsDevToolsEnabled())
        {
            return NotFound();
        }

        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await action(_currentUser.UserId.Value, HttpContext.RequestAborted);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
