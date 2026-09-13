using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Pricing;
using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Administrator)]
[Route("api/v1/admin/pricing-rules")]
public sealed class AdminPricingRulesController(IPricingService pricingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<PricingRuleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PricingRuleDto>>> List(CancellationToken cancellationToken) =>
        Ok(await pricingService.ListForAdministrationAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<PricingRuleDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PricingRuleDto>> Create(CreatePricingRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await pricingService.CreateRuleAsync(
            new CreatePricingRuleCommand(
                request.CityCode,
                request.ServiceType,
                request.MinimumFareCop,
                request.BaseFareCop,
                request.FarePerKilometerCop,
                request.FarePerMinuteCop,
                request.IsActive,
                request.IncludedWaitingMinutes,
                request.AdditionalWaitingFarePerMinuteCop),
            cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("pricing", result.Error!);
    }

    [HttpPut("{pricingRuleId:guid}")]
    [ProducesResponseType<PricingRuleDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PricingRuleDto>> Update(
        Guid pricingRuleId,
        UpdatePricingRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pricingService.UpdateRuleAsync(
            pricingRuleId,
            new UpdatePricingRuleCommand(
                request.MinimumFareCop,
                request.BaseFareCop,
                request.FarePerKilometerCop,
                request.FarePerMinuteCop,
                request.IsActive,
                request.IncludedWaitingMinutes,
                request.AdditionalWaitingFarePerMinuteCop),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("pricing", result.Error!);
    }
}

public sealed record CreatePricingRuleRequest(
    [Required, StringLength(20, MinimumLength = 2)] string CityCode,
    [EnumDataType(typeof(RideServiceType))] RideServiceType ServiceType,
    [Range(1, int.MaxValue)] int MinimumFareCop,
    [Range(0, int.MaxValue)] int BaseFareCop,
    [Range(0, int.MaxValue)] int FarePerKilometerCop,
    [Range(0, int.MaxValue)] int FarePerMinuteCop,
    bool IsActive,
    [Range(0, 1_440)] int IncludedWaitingMinutes = PricingRule.DefaultIncludedWaitingMinutes,
    [Range(0, int.MaxValue)] int AdditionalWaitingFarePerMinuteCop = PricingRule.DefaultAdditionalWaitingFarePerMinuteCop);

public sealed record UpdatePricingRuleRequest(
    [Range(1, int.MaxValue)] int MinimumFareCop,
    [Range(0, int.MaxValue)] int BaseFareCop,
    [Range(0, int.MaxValue)] int FarePerKilometerCop,
    [Range(0, int.MaxValue)] int FarePerMinuteCop,
    bool IsActive,
    [Range(0, 1_440)] int IncludedWaitingMinutes = PricingRule.DefaultIncludedWaitingMinutes,
    [Range(0, int.MaxValue)] int AdditionalWaitingFarePerMinuteCop = PricingRule.DefaultAdditionalWaitingFarePerMinuteCop);
