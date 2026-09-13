using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Safety;
using Hagale.Domain.Safety;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Administrator)]
[Route("api/v1/admin/safety/emergency-channels")]
public sealed class AdminEmergencyServiceChannelsController(
    IEmergencyServiceChannelService emergencyServiceChannelService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<EmergencyServiceChannelDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<EmergencyServiceChannelDto>>> List(CancellationToken cancellationToken) =>
        Ok(await emergencyServiceChannelService.ListForAdministrationAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<EmergencyServiceChannelDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EmergencyServiceChannelDto>> Create(
        CreateEmergencyServiceChannelRequest request,
        CancellationToken cancellationToken)
    {
        var result = await emergencyServiceChannelService.CreateAsync(
            new CreateEmergencyServiceChannelCommand(
                request.CityCode,
                request.ChannelType,
                request.DisplayName,
                request.ContactNumber,
                request.IsActive),
            cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("emergencyServiceChannel", result.Error!);
    }

    [HttpPut("{emergencyServiceChannelId:guid}")]
    [ProducesResponseType<EmergencyServiceChannelDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmergencyServiceChannelDto>> Update(
        Guid emergencyServiceChannelId,
        UpdateEmergencyServiceChannelRequest request,
        CancellationToken cancellationToken)
    {
        var result = await emergencyServiceChannelService.UpdateAsync(
            emergencyServiceChannelId,
            new UpdateEmergencyServiceChannelCommand(request.DisplayName, request.ContactNumber, request.IsActive),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("emergencyServiceChannel", result.Error!);
    }
}

public sealed record CreateEmergencyServiceChannelRequest(
    [Required, StringLength(20, MinimumLength = 2)] string CityCode,
    [EnumDataType(typeof(EmergencyChannelType))] EmergencyChannelType ChannelType,
    [Required, StringLength(100, MinimumLength = 2)] string DisplayName,
    [Required, RegularExpression(@"^\+?[0-9][0-9 -]{1,18}$")] string ContactNumber,
    bool IsActive);

public sealed record UpdateEmergencyServiceChannelRequest(
    [Required, StringLength(100, MinimumLength = 2)] string DisplayName,
    [Required, RegularExpression(@"^\+?[0-9][0-9 -]{1,18}$")] string ContactNumber,
    bool IsActive);
