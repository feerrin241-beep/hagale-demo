using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Safety;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/safety/emergency-contacts")]
public sealed class EmergencyContactsController(IEmergencyContactService emergencyContactService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyCollection<EmergencyContactDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<EmergencyContactDto>>> ListMine(CancellationToken cancellationToken) =>
        Ok(await emergencyContactService.ListMineAsync(User.GetRequiredUserId(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<EmergencyContactDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EmergencyContactDto>> Create(
        SaveEmergencyContactRequest request,
        CancellationToken cancellationToken)
    {
        var result = await emergencyContactService.CreateAsync(
            User.GetRequiredUserId(),
            new CreateEmergencyContactCommand(request.Name, request.PhoneNumber, request.Relationship),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("emergencyContact", result.Error!);
    }

    [HttpPut("{emergencyContactId:guid}")]
    [ProducesResponseType<EmergencyContactDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmergencyContactDto>> Update(
        Guid emergencyContactId,
        SaveEmergencyContactRequest request,
        CancellationToken cancellationToken)
    {
        var result = await emergencyContactService.UpdateAsync(
            User.GetRequiredUserId(),
            emergencyContactId,
            new UpdateEmergencyContactCommand(request.Name, request.PhoneNumber, request.Relationship),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("emergencyContact", result.Error!);
    }

    [HttpPost("{emergencyContactId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid emergencyContactId, CancellationToken cancellationToken)
    {
        var result = await emergencyContactService.ArchiveAsync(
            User.GetRequiredUserId(),
            emergencyContactId,
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : this.BusinessRuleViolation("emergencyContact", result.Error!);
    }
}

public sealed record SaveEmergencyContactRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [Required, RegularExpression(@"^\+[1-9]\d{7,14}$")] string PhoneNumber,
    [StringLength(100)] string? Relationship);
