using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Pricing;
using Hagale.Domain.Rides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Customer)]
[Route("api/v1/pricing")]
public sealed class PricingController(IPricingService pricingService) : ControllerBase
{
    [HttpGet("rules")]
    [ProducesResponseType<IReadOnlyCollection<PricingRuleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PricingRuleDto>>> ListRules(CancellationToken cancellationToken) =>
        Ok(await pricingService.ListActiveAsync(cancellationToken));

    [HttpGet("quote")]
    [ProducesResponseType<PricingQuoteDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PricingQuoteDto>> Quote(
        [FromQuery, Required, StringLength(20, MinimumLength = 2)] string cityCode,
        [FromQuery] RideServiceType serviceType,
        [FromQuery, Range(0, 100_000)] decimal estimatedDistanceKilometers,
        [FromQuery, Range(0, 100_000)] int estimatedDurationMinutes,
        CancellationToken cancellationToken)
    {
        var result = await pricingService.QuoteAsync(
            new PricingQuoteRequest(cityCode, serviceType, estimatedDistanceKilometers, estimatedDurationMinutes),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("pricing", result.Error!);
    }
}
