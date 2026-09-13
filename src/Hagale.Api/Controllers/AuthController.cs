using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hagale.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthenticationService authenticationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthenticatedUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthenticatedUserDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterAsync(
            new RegisterUserCommand(request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Password),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("registration", result.Error!);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthenticatedUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No fue posible iniciar sesión.", detail: result.Error);
    }

    [AllowAnonymous]
    [HttpGet("google/status")]
    [ProducesResponseType<ExternalAuthProviderStatusDto>(StatusCodes.Status200OK)]
    public ActionResult<ExternalAuthProviderStatusDto> GetGoogleStatus() =>
        Ok(authenticationService.GetGoogleProviderStatus());

    [AllowAnonymous]
    [HttpPost("google")]
    [ProducesResponseType<AuthenticatedUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserDto>> SignInWithGoogle(
        GoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.SignInWithGoogleAsync(
            new GoogleSignInCommand(request.Credential),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No fue posible ingresar con Google.", detail: result.Error);
    }

    [Authorize]
    [HttpPost("renew-session")]
    [ProducesResponseType<AuthenticatedUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedUserDto>> RenewCurrentSession(CancellationToken cancellationToken)
    {
        var result = await authenticationService.RenewCurrentSessionAsync(User.GetRequiredUserId(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No fue posible renovar la sesión.", detail: result.Error);
    }
}

public sealed record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FirstName,
    [Required, StringLength(100, MinimumLength = 2)] string LastName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, RegularExpression("^\\+[1-9]\\d{7,14}$", ErrorMessage = "Usa el formato internacional E.164, por ejemplo +573001234567.")] string PhoneNumber,
    [Required, StringLength(128, MinimumLength = 12)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 1)] string Password);

public sealed record GoogleSignInRequest(
    [Required, StringLength(4096, MinimumLength = 20)] string Credential);
