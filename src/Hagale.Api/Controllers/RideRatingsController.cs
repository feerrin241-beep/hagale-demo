using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Rides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ride-requests/{rideRequestId:guid}/ratings")]
public sealed class RideRatingsController(IRideRatingService rideRatingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<RideRatingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RideRatingDto>>> List(
        Guid rideRequestId,
        CancellationToken cancellationToken)
    {
        var result = await rideRatingService.ListAsync(
            User.GetRequiredUserId(),
            rideRequestId,
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("rideRating", result.Error!);
    }

    [HttpPost]
    [ProducesResponseType<RideRatingDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RideRatingDto>> Submit(
        Guid rideRequestId,
        SubmitRideRatingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rideRatingService.SubmitAsync(
            User.GetRequiredUserId(),
            rideRequestId,
            new SubmitRideRatingCommand(request.Score, request.Comment),
            cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("rideRating", result.Error!);
    }
}

public sealed record SubmitRideRatingRequest(
    [Range(1, 5)] int Score,
    [StringLength(240)] string? Comment);
