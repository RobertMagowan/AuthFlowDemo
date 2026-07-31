using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OAuthFlowDemo.Controllers;

[ApiController]
[Route("/api")]
public sealed class ApiController : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetMe()
    {
        return Ok(new
        {
            Authenticated = true,
            IdentityType = User.Identity?.AuthenticationType,
            Name = User.Identity?.Name,
            Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Administrator")]
    public IActionResult GetAdminData()
    {
        return Ok(new { Message = "This data is only visible to administrators." });
    }

    [HttpGet("operator")]
    [Authorize(Roles = "Operator,Administrator")]
    public IActionResult GetOperatorData()
    {
        return Ok(new { Message = "This data is visible to operators and administrators." });
    }

    [HttpGet("scopes")]
    [Authorize(Policy = "RequireUserImpersonation")]
    public IActionResult GetScopedData()
    {
        return Ok(new { Message = "This data requires the User.Impersonation scope." });
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult GetPublic()
    {
        return Ok(new
        {
            Message = "This endpoint is public. No authentication required.",
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
