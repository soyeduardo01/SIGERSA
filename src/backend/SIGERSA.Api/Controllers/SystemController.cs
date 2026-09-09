using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK, "application/json")]
    public ActionResult<SystemStatusResponse> GetStatus()
    {
        return Ok(new SystemStatusResponse("SIGERSA.Api", "ok", "10.0"));
    }
}

public sealed record SystemStatusResponse(string Service, string Status, string DotNetVersion);
