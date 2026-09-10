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
    private readonly IConfiguration _configuration;

    public DevMutualAidController(
        IMutualAidDevService devService,
        DevEnvironmentResetService resetService,
        ICurrentUserService currentUser,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _devService = devService;
        _resetService = resetService;
        _currentUser = currentUser;
        _environment = environment;
        _configuration = configuration;
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

        try
        {
            await _resetService.ResetAsync(CancellationToken.None);
            return Ok(new DevActionResultDto
            {
                Success = true,
                Message = "All app data and deep-freeze storage were cleared. The app is back to first-run state."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new DevActionResultDto
            {
                Success = false,
                Message = $"Reset failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Local/dev only (Development, Docker, or explicit DevTools:Enabled).
    /// Staging is never enabled — including leftover App Settings or staging hostnames.
    /// </summary>
    private bool IsDevToolsEnabled()
    {
        if (_environment.IsStaging())
        {
            return false;
        }

        var host = HttpContext.Request.Host.Host;
        if (host.Contains("staging", StringComparison.OrdinalIgnoreCase)
            && !host.Contains("production", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_environment.IsDevelopment()
            || _environment.IsEnvironment("Docker"))
        {
            return true;
        }

        return _configuration.GetValue("DevTools:Enabled", false);
    }

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
