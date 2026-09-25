using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Domain.Platform;
using Hagale.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Api.Controllers;

[ApiController]
[Route("api/v1/platform-appearance")]
public sealed class PlatformAppearanceController(
    HagaleDbContext database,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType<PlatformAppearanceDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformAppearanceDto>> Get(CancellationToken cancellationToken)
    {
        var appearance = await GetOrCreateAsync(cancellationToken);
        return Ok(ToDto(appearance));
    }

    [HttpPut]
    [Authorize(Roles = HagaleRoles.Administrator)]
    [ProducesResponseType<PlatformAppearanceDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformAppearanceDto>> Update(UpdatePlatformAppearanceRequest request, CancellationToken cancellationToken)
    {
        var appearance = await GetOrCreateAsync(cancellationToken);
        appearance.Update(
            request.AccentColor,
            request.ActionColor,
            request.BusyColor,
            request.CustomerModeLabel.Trim(),
            request.DriverModeLabel.Trim(),
            request.FreeStatusLabel.Trim(),
            request.BusyStatusLabel.Trim(),
            request.RequestActionLabel.Trim(),
            request.DriverOfferVoiceTemplate.Trim(),
            timeProvider.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(appearance));
    }

    private async Task<PlatformAppearance> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var appearance = await database.PlatformAppearances.SingleOrDefaultAsync(item => item.Id == PlatformAppearance.SingletonId, cancellationToken);
        if (appearance is not null) return appearance;
        appearance = new PlatformAppearance(timeProvider.GetUtcNow());
        database.PlatformAppearances.Add(appearance);
        await database.SaveChangesAsync(cancellationToken);
        return appearance;
    }

    private static PlatformAppearanceDto ToDto(PlatformAppearance appearance) => new(
        appearance.Id,
        appearance.AccentColor,
        appearance.ActionColor,
        appearance.BusyColor,
        appearance.CustomerModeLabel,
        appearance.DriverModeLabel,
        appearance.FreeStatusLabel,
        appearance.BusyStatusLabel,
        appearance.RequestActionLabel,
        appearance.DriverOfferVoiceTemplate,
        appearance.UpdatedAtUtc);
}

public sealed record UpdatePlatformAppearanceRequest(
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string AccentColor,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string ActionColor,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string BusyColor,
    [Required, StringLength(40, MinimumLength = 2)] string CustomerModeLabel,
    [Required, StringLength(40, MinimumLength = 2)] string DriverModeLabel,
    [Required, StringLength(40, MinimumLength = 2)] string FreeStatusLabel,
    [Required, StringLength(40, MinimumLength = 2)] string BusyStatusLabel,
    [Required, StringLength(80, MinimumLength = 2)] string RequestActionLabel,
    [Required, StringLength(500, MinimumLength = 10)] string DriverOfferVoiceTemplate);

public sealed record PlatformAppearanceDto(
    int Id,
    string AccentColor,
    string ActionColor,
    string BusyColor,
    string CustomerModeLabel,
    string DriverModeLabel,
    string FreeStatusLabel,
    string BusyStatusLabel,
    string RequestActionLabel,
    string DriverOfferVoiceTemplate,
    DateTimeOffset UpdatedAtUtc);
