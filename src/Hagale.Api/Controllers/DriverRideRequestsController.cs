using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Common;
using Hagale.Application.Rides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Driver)]
[Route("api/v1/driver/ride-requests")]
public sealed class DriverRideRequestsController(IRideRequestService rideRequestService) : ControllerBase
{
    [HttpGet("available")]
    [ProducesResponseType<IReadOnlyCollection<DriverRideRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<DriverRideRequestDto>>> ListAvailable(
        [FromQuery, Range(1, 50)] decimal? maximumPickupDistanceKilometers,
        CancellationToken cancellationToken) =>
        Ok(await rideRequestService.ListAvailableForDriverAsync(
            User.GetRequiredUserId(),
            maximumPickupDistanceKilometers,
            cancellationToken));

    [HttpGet("current")]
    [ProducesResponseType<DriverRideRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<DriverRideRequestDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var request = await rideRequestService.GetCurrentForDriverAsync(User.GetRequiredUserId(), cancellationToken);
        return request is null ? NoContent() : Ok(request);
    }

    [HttpGet("completed")]
    [ProducesResponseType<IReadOnlyCollection<DriverRideRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<DriverRideRequestDto>>> ListCompleted(CancellationToken cancellationToken) =>
        Ok(await rideRequestService.ListCompletedForDriverAsync(User.GetRequiredUserId(), cancellationToken));

    [HttpGet("activity-summary")]
    [ProducesResponseType<DriverActivitySummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverActivitySummaryDto>> GetActivitySummary(CancellationToken cancellationToken) =>
        Ok(await rideRequestService.GetActivitySummaryAsync(User.GetRequiredUserId(), cancellationToken));

    [HttpPost("{rideRequestId:guid}/accept")]
    [ProducesResponseType<DriverRideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverRideRequestDto>> Accept(Guid rideRequestId, CancellationToken cancellationToken)
    {
        var result = await rideRequestService.AcceptAsync(User.GetRequiredUserId(), rideRequestId, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("rideAssignment", result.Error!);
    }

    [HttpPost("{rideRequestId:guid}/counter-offer")]
    [ProducesResponseType<DriverRideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverRideRequestDto>> MakeCounterOffer(
        Guid rideRequestId,
        CreateCounterOfferRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rideRequestService.MakeCounterOfferAsync(
            User.GetRequiredUserId(),
            rideRequestId,
            new CreateCounterOfferCommand(request.PriceCop),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.BusinessRuleViolation("counterOffer", result.Error!);
    }

    [HttpPost("{rideRequestId:guid}/en-route")]
    [ProducesResponseType<DriverRideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverRideRequestDto>> MarkEnRoute(Guid rideRequestId, CancellationToken cancellationToken) =>
        await UpdateJourneyAsync(rideRequestService.MarkDriverEnRouteAsync(User.GetRequiredUserId(), rideRequestId, cancellationToken));

    [HttpPost("{rideRequestId:guid}/arrived")]
    [ProducesResponseType<DriverRideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverRideRequestDto>> MarkArrived(Guid rideRequestId, CancellationToken cancellationToken) =>
        await UpdateJourneyAsync(rideRequestService.MarkDriverArrivedAsync(User.GetRequiredUserId(), rideRequestId, cancellationToken));

    [HttpPost("{rideRequestId:guid}/start")]
    [ProducesResponseType<DriverRideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverRideRequestDto>> Start(Guid rideRequestId, CancellationToken cancellationToken) =>
        await UpdateJourneyAsync(rideRequestService.StartAsync(User.GetRequiredUserId(), rideRequestId, cancellationToken));

    [HttpPost("{rideRequestId:guid}/complete")]
    [ProducesResponseType<DriverRideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverRideRequestDto>> Complete(Guid rideRequestId, CancellationToken cancellationToken) =>
        await UpdateJourneyAsync(rideRequestService.CompleteAsync(User.GetRequiredUserId(), rideRequestId, cancellationToken));

    private async Task<ActionResult<DriverRideRequestDto>> UpdateJourneyAsync(Task<ApplicationResult<DriverRideRequestDto>> action)
    {
        var result = await action;
        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("rideJourney", result.Error!);
    }
}

public sealed record CreateCounterOfferRequest([System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int PriceCop);
