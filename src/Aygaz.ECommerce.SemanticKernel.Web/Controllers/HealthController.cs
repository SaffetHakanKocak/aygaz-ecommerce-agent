using Microsoft.AspNetCore.Mvc;

namespace Aygaz.ECommerce.SemanticKernel.Web.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get()
    {
        return Ok(new { status = "ok" });
    }
}
