using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Customer + "," + HagaleRoles.Driver + "," + HagaleRoles.Administrator)]
[Route("api/v1/routes")]
public sealed class RoutingController(IRoadRoutingService roadRoutingService) : ControllerBase
{
    [HttpGet("estimate")]
    [ProducesResponseType<RoadRouteDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RoadRouteDto>> Estimate(
        [FromQuery, Required, Range(-90, 90)] decimal originLatitude,
        [FromQuery, Required, Range(-180, 180)] decimal originLongitude,
        [FromQuery, Required, Range(-90, 90)] decimal destinationLatitude,
        [FromQuery, Required, Range(-180, 180)] decimal destinationLongitude,
        CancellationToken cancellationToken)
    {
        var result = await roadRoutingService.EstimateAsync(
            new RouteEstimateRequest(
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("routing", result.Error!);
    }
}
