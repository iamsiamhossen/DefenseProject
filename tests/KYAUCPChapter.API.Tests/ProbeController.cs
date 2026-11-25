using KYAUCPChapter.API.Security;
using KYAUCPChapter.Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYAUCPChapter.API.Tests;

[ApiController]
[Route("api/test-probe")]
public sealed class ProbeController : ControllerBase
{
    public const string InternalDetail = "Sensitive internal failure detail";

    [HttpGet("authenticated")]
    public IActionResult Authenticated() => NoContent();

    [HttpGet("student")]
    [Authorize(Policy = AuthorizationPolicies.Student)]
    public IActionResult Student() => NoContent();

    [HttpGet("organizer")]
    [Authorize(Policy = AuthorizationPolicies.Organizer)]
    public IActionResult Organizer() => NoContent();

    [HttpPost("domain-error")]
    [AllowAnonymous]
    public IActionResult DomainError() => throw new DomainException("Registration is not open for this contest.");

    [HttpGet("unhandled-error")]
    [AllowAnonymous]
    public IActionResult UnhandledError() => throw new InvalidOperationException(InternalDetail);
}
