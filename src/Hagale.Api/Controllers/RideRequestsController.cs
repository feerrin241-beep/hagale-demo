using System.ComponentModel.DataAnnotations;
using Hagale.Application.Authentication;
using Hagale.Application.Rides;
using Hagale.Domain.Rides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Customer + "," + HagaleRoles.Administrator)]
[Route("api/v1/ride-requests")]
public sealed class RideRequestsController(IRideRequestService rideRequestService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RideRequestDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RideRequestDto>> Create(CreateRideRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await rideRequestService.CreateAsync(
            User.GetRequiredUserId(),
            new CreateRideRequestCommand(
                request.PickupAddress,
                request.DestinationAddress,
                request.OperatingCityCode,
                request.ProposedPriceCop,
                request.ServiceType,
                request.PickupLatitude,
                request.PickupLongitude,
                request.DestinationLatitude,
                request.DestinationLongitude,
                request.PaymentMethod,
                request.FareMode),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.BusinessRuleViolation("rideRequest", result.Error!);
    }

    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyCollection<RideRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RideRequestDto>>> ListMine(CancellationToken cancellationToken) =>
        Ok(await rideRequestService.ListMineAsync(User.GetRequiredUserId(), cancellationToken));

    [HttpGet("{rideRequestId:guid}/tracking")]
    [ProducesResponseType<CustomerRideTrackingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<CustomerRideTrackingDto>> GetTracking(
        Guid rideRequestId,
        CancellationToken cancellationToken)
    {
        var tracking = await rideRequestService.GetTrackingForCustomerAsync(
            User.GetRequiredUserId(),
            rideRequestId,
            cancellationToken);
        return tracking is null ? NoContent() : Ok(tracking);
    }

    [HttpPost("{rideRequestId:guid}/cancel")]
    [ProducesResponseType<RideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RideRequestDto>> Cancel(
        Guid rideRequestId,
        CancelRideRequestRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rideRequestService.CancelAsync(
            User.GetRequiredUserId(),
            rideRequestId,
            new CancelRideRequestCommand(request.Reason),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : this.BusinessRuleViolation("rideRequest", result.Error!);
    }

    [HttpPost("{rideRequestId:guid}/counter-offer/accept")]
    [ProducesResponseType<RideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RideRequestDto>> AcceptCounterOffer(Guid rideRequestId, CancellationToken cancellationToken)
    {
        var result = await rideRequestService.AcceptCounterOfferAsync(User.GetRequiredUserId(), rideRequestId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.BusinessRuleViolation("counterOffer", result.Error!);
    }

    [HttpPost("{rideRequestId:guid}/counter-offer/reject")]
    [ProducesResponseType<RideRequestDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RideRequestDto>> RejectCounterOffer(Guid rideRequestId, CancellationToken cancellationToken)
    {
        var result = await rideRequestService.RejectCounterOfferAsync(User.GetRequiredUserId(), rideRequestId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.BusinessRuleViolation("counterOffer", result.Error!);
    }
}

public sealed record CreateRideRequestRequest(
    [Required, StringLength(250, MinimumLength = 5)] string PickupAddress,
    [Required, StringLength(250, MinimumLength = 5)] string DestinationAddress,
    [Required, StringLength(20, MinimumLength = 2)] string OperatingCityCode,
    [Range(1, int.MaxValue)] int ProposedPriceCop,
    [EnumDataType(typeof(RideServiceType))] RideServiceType ServiceType,
    [Range(typeof(decimal), "-90", "90")] decimal? PickupLatitude = null,
    [Range(typeof(decimal), "-180", "180")] decimal? PickupLongitude = null,
    [Range(typeof(decimal), "-90", "90")] decimal? DestinationLatitude = null,
    [Range(typeof(decimal), "-180", "180")] decimal? DestinationLongitude = null,
    [EnumDataType(typeof(RidePaymentMethod))] RidePaymentMethod PaymentMethod = RidePaymentMethod.Cash,
    [EnumDataType(typeof(RideFareMode))] RideFareMode FareMode = RideFareMode.PassengerOffer);

public sealed record CancelRideRequestRequest([StringLength(500)] string? Reason);
