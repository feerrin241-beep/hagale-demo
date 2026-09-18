using Hagale.Application.Common;
using Hagale.Domain.Drivers;
using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;

namespace Hagale.Application.Rides;

public sealed record CreateRideRequestCommand(
    string PickupAddress,
    string DestinationAddress,
    string OperatingCityCode,
    int ProposedPriceCop,
    RideServiceType ServiceType,
    decimal? PickupLatitude = null,
    decimal? PickupLongitude = null,
    decimal? DestinationLatitude = null,
    decimal? DestinationLongitude = null,
    RidePaymentMethod PaymentMethod = RidePaymentMethod.Cash,
    RideFareMode FareMode = RideFareMode.PassengerOffer);

public sealed record CancelRideRequestCommand(string? Reason);

public sealed record CreateCounterOfferCommand(int PriceCop);

public sealed record RideRequestDto(
    Guid Id,
    string PickupAddress,
    string DestinationAddress,
    string OperatingCityCode,
    RideServiceType ServiceType,
    RidePaymentMethod PaymentMethod,
    RideFareMode FareMode,
    int ProposedPriceCop,
    int MinimumFareCopAtRequest,
    RideRequestStatus Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string? CancellationReason,
    int? CounterOfferPriceCop,
    DateTimeOffset? CounterOfferAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? DriverEnRouteAtUtc,
    DateTimeOffset? DriverArrivedAtUtc,
    DateTimeOffset? WaitingStartedAtUtc,
    DateTimeOffset? WaitingEndedAtUtc,
    int? IncludedWaitingMinutesAtStart,
    int? AdditionalWaitingFarePerMinuteCopAtStart,
    int? AdditionalWaitingMinutes,
    int? WaitingAdditionalChargeCop,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    decimal? DestinationLatitude,
    decimal? DestinationLongitude);

public sealed record DriverRideRequestDto(
    Guid Id,
    string PickupAddress,
    string DestinationAddress,
    string OperatingCityCode,
    RideServiceType ServiceType,
    RidePaymentMethod PaymentMethod,
    RideFareMode FareMode,
    int ProposedPriceCop,
    int MinimumFareCopAtRequest,
    int? DirectDistanceReferenceFareCop,
    RideRequestStatus Status,
    DateTimeOffset RequestedAtUtc,
    decimal? PickupDistanceKilometers,
    decimal? TripDistanceKilometers,
    decimal? TotalDistanceKilometers,
    int? CounterOfferPriceCop,
    DateTimeOffset? CounterOfferAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? DriverEnRouteAtUtc,
    DateTimeOffset? DriverArrivedAtUtc,
    DateTimeOffset? WaitingStartedAtUtc,
    DateTimeOffset? WaitingEndedAtUtc,
    int? IncludedWaitingMinutesAtStart,
    int? AdditionalWaitingFarePerMinuteCopAtStart,
    int? AdditionalWaitingMinutes,
    int? WaitingAdditionalChargeCop,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    decimal? DestinationLatitude,
    decimal? DestinationLongitude,
    int FairOfferMinimumPercent = PricingRule.DefaultFairOfferMinimumPercent,
    int FavorableOfferMinimumPercent = PricingRule.DefaultFavorableOfferMinimumPercent);

public sealed record DriverActivitySummaryDto(
    int CompletedRideCount,
    int CompletedServiceValueCop,
    int CompletedWaitingChargeCop,
    int CompletedCashRideCount,
    int CompletedCashValueCop,
    int CompletedNequiRideCount,
    int CompletedNequiValueCop,
    decimal? CompletedDirectDistanceKilometers,
    DateTimeOffset? LastCompletedAtUtc);

public sealed record CustomerRideTrackingDto(
    Guid RideRequestId,
    RideRequestStatus Status,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? DriverEnRouteAtUtc,
    DateTimeOffset? DriverArrivedAtUtc,
    DateTimeOffset? WaitingStartedAtUtc,
    DateTimeOffset? WaitingEndedAtUtc,
    int? IncludedWaitingMinutesAtStart,
    int? AdditionalWaitingFarePerMinuteCopAtStart,
    int? AdditionalWaitingMinutes,
    int? WaitingAdditionalChargeCop,
    DateTimeOffset? StartedAtUtc,
    AssignedDriverSummaryDto? Driver,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    decimal? DestinationLatitude,
    decimal? DestinationLongitude,
    decimal? DriverLatitude,
    decimal? DriverLongitude,
    DateTimeOffset? DriverLocationUpdatedAtUtc);

public sealed record AssignedDriverSummaryDto(
    string FirstName,
    string LastName,
    string VehicleBrand,
    string VehicleModel,
    int VehicleYear,
    string VehicleColor,
    string VehiclePlate,
    VehicleType VehicleType);

public interface IRideRequestService
{
    Task<ApplicationResult<RideRequestDto>> CreateAsync(Guid customerUserId, CreateRideRequestCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RideRequestDto>> ListMineAsync(Guid customerUserId, CancellationToken cancellationToken = default);
    Task<CustomerRideTrackingDto?> GetTrackingForCustomerAsync(Guid customerUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<RideRequestDto>> CancelAsync(Guid customerUserId, Guid rideRequestId, CancelRideRequestCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<RideRequestDto>> AcceptCounterOfferAsync(Guid customerUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<RideRequestDto>> RejectCounterOfferAsync(Guid customerUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DriverRideRequestDto>> ListAvailableForDriverAsync(Guid driverUserId, decimal? maximumPickupDistanceKilometers = null, CancellationToken cancellationToken = default);
    Task<DriverRideRequestDto?> GetCurrentForDriverAsync(Guid driverUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DriverRideRequestDto>> ListCompletedForDriverAsync(Guid driverUserId, CancellationToken cancellationToken = default);
    Task<DriverActivitySummaryDto> GetActivitySummaryAsync(Guid driverUserId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverRideRequestDto>> AcceptAsync(Guid driverUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverRideRequestDto>> MakeCounterOfferAsync(Guid driverUserId, Guid rideRequestId, CreateCounterOfferCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverRideRequestDto>> MarkDriverEnRouteAsync(Guid driverUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverRideRequestDto>> MarkDriverArrivedAsync(Guid driverUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverRideRequestDto>> StartAsync(Guid driverUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverRideRequestDto>> CompleteAsync(Guid driverUserId, Guid rideRequestId, CancellationToken cancellationToken = default);
}
