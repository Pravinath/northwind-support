using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth;
using SupportDesk.Presentation.Services;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(LoginCommandHandler loginHandler, JwtTokenService tokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await loginHandler.HandleAsync(request, ct);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(tokens.Create(result));
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<CurrentAgentDto> Me()
    {
        var agentId = User.FindFirstValue("agentId");
        if (!int.TryParse(agentId, out var id))
        {
            return Unauthorized();
        }

        return Ok(new CurrentAgentDto(id, User.Identity?.Name ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty));
    }
}
