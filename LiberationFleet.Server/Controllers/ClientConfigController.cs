using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/client-config")]
public class ClientConfigController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<object> Get()
    {
        var showFallibleAttribution = configuration.GetValue("Client:ShowFallibleAttribution", false);
        return Ok(new { showFallibleAttribution });
    }
}
