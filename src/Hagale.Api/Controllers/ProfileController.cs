using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
public sealed class ProfileController(IUserProfileService userProfileService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetMine(CancellationToken cancellationToken)
    {
        var result = await userProfileService.GetAsync(User.GetRequiredUserId(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new ProblemDetails { Title = "Perfil no disponible.", Detail = result.Error });
    }

    [HttpPut("me")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> UpdateMine(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await userProfileService.UpdateAsync(
            User.GetRequiredUserId(),
            new UpdateUserProfileCommand(request.FirstName, request.LastName, request.PhoneNumber),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("profile", result.Error!);
    }

    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMine(CancellationToken cancellationToken)
    {
        var result = await userProfileService.DeactivateAsync(User.GetRequiredUserId(), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : this.BusinessRuleViolation("profile", result.Error!);
    }
}

public sealed record UpdateProfileRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FirstName,
    [Required, StringLength(100, MinimumLength = 2)] string LastName,
    [Required, RegularExpression("^\\+[1-9]\\d{7,14}$", ErrorMessage = "Usa el formato internacional E.164, por ejemplo +573001234567.")] string PhoneNumber);
