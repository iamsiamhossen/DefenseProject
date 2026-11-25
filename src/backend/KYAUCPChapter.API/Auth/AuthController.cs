using KYAUCPChapter.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KYAUCPChapter.API.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AccountService accounts,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAntiforgery antiforgery,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    public ActionResult<AntiforgeryTokenResponse> GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new AntiforgeryTokenResponse(tokens.RequestToken!, tokens.HeaderName!);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserResponse>> Register(RegisterStudentRequest request, CancellationToken cancellationToken)
    {
        var (result, user) = await accounts.RegisterStudentAsync(request.FullName, request.StudentId, request.Email, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            return IdentityValidationProblem(result);
        }

        logger.LogInformation("Student account {UserId} registered", user!.Id);
        return StatusCode(StatusCodes.Status201Created, await ToResponseAsync(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim();
        var result = await signInManager.PasswordSignInAsync(email, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            logger.LogWarning("Sign-in failed (locked out: {IsLockedOut})", result.IsLockedOut);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
        }

        var user = await userManager.FindByNameAsync(email);
        logger.LogInformation("User {UserId} signed in", user!.Id);
        return await ToResponseAsync(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "The account is no longer available.");
        }

        return await ToResponseAsync(user);
    }

    private async Task<CurrentUserResponse> ToResponseAsync(ApplicationUser user) =>
        new(user.Id, user.Email!, user.FullName, user.StudentId, [.. await userManager.GetRolesAsync(user)]);

    private ActionResult IdentityValidationProblem(IdentityResult result)
    {
        var emailErrorPresent = result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.InvalidEmail));
        foreach (var error in result.Errors)
        {
            var key = error.Code switch
            {
                AccountService.DuplicateStudentIdCode => nameof(RegisterStudentRequest.StudentId),
                nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.InvalidEmail) => nameof(RegisterStudentRequest.Email),
                nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.InvalidUserName) when emailErrorPresent => null,
                nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.InvalidUserName) => nameof(RegisterStudentRequest.Email),
                _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => nameof(RegisterStudentRequest.Password),
                _ => string.Empty
            };

            if (key is not null)
            {
                ModelState.AddModelError(key, error.Description);
            }
        }

        return ValidationProblem(ModelState);
    }
}
