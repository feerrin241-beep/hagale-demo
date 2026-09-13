using Hagale.Domain.Common;
using Hagale.Domain.Rides;

namespace Hagale.Domain.Tests.Rides;

public sealed class RideRequestTests
{
    [Fact]
    public void Ride_request_keeps_payment_method_and_fare_mode()
    {
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            null,
            null,
            null,
            null,
            RidePaymentMethod.Nequi,
            RideFareMode.DynamicFare,
            DateTimeOffset.UtcNow);

        Assert.Equal(RidePaymentMethod.Nequi, request.PaymentMethod);
        Assert.Equal(RideFareMode.DynamicFare, request.FareMode);
    }

    [Fact]
    public void Ride_request_rejects_equal_pickup_and_destination()
    {
        Assert.Throws<DomainRuleViolationException>(() => new RideRequest(
            Guid.NewGuid(),
            "Carrera 7 # 72-41, Bogotá",
            "Carrera 7 # 72-41, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Pending_ride_request_can_be_cancelled_once()
    {
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);

        request.Cancel("Cambié de planes.", DateTimeOffset.UtcNow);

        Assert.Equal(RideRequestStatus.Cancelled, request.Status);
        Assert.Throws<DomainRuleViolationException>(() => request.Cancel(null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Pending_ride_request_can_be_accepted_once_and_cannot_then_be_cancelled()
    {
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);

        request.Accept(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(RideRequestStatus.Accepted, request.Status);
        Assert.NotNull(request.AssignedDriverProfileId);
        Assert.Throws<DomainRuleViolationException>(() => request.Cancel(null, DateTimeOffset.UtcNow));
        Assert.Throws<DomainRuleViolationException>(() => request.Accept(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Accepted_ride_request_follows_the_required_journey_state_order()
    {
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        request.Accept(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleViolationException>(() => request.Start(DateTimeOffset.UtcNow));
        request.MarkDriverEnRoute(DateTimeOffset.UtcNow);
        Assert.Throws<DomainRuleViolationException>(() => request.Start(DateTimeOffset.UtcNow));
        request.MarkDriverArrived(DateTimeOffset.UtcNow);
        request.Start(DateTimeOffset.UtcNow);
        request.Complete(DateTimeOffset.UtcNow);

        Assert.Equal(RideRequestStatus.Completed, request.Status);
        Assert.NotNull(request.DriverEnRouteAtUtc);
        Assert.NotNull(request.DriverArrivedAtUtc);
        Assert.NotNull(request.StartedAtUtc);
        Assert.NotNull(request.CompletedAtUtc);
    }

    [Fact]
    public void Starting_a_ride_closes_waiting_time_using_the_policy_snapshotted_at_arrival()
    {
        var arrivedAtUtc = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            arrivedAtUtc.AddMinutes(-2));
        request.Accept(Guid.NewGuid(), arrivedAtUtc.AddMinutes(-1));
        request.MarkDriverEnRoute(arrivedAtUtc.AddSeconds(-30));

        request.MarkDriverArrived(arrivedAtUtc, includedWaitingMinutes: 5, additionalWaitingFarePerMinuteCop: 1_000);
        request.Start(arrivedAtUtc.AddMinutes(6).AddSeconds(1));

        Assert.Equal(arrivedAtUtc, request.WaitingStartedAtUtc);
        Assert.Equal(arrivedAtUtc.AddMinutes(6).AddSeconds(1), request.WaitingEndedAtUtc);
        Assert.Equal(5, request.IncludedWaitingMinutesAtStart);
        Assert.Equal(1_000, request.AdditionalWaitingFarePerMinuteCopAtStart);
        Assert.Equal(2, request.AdditionalWaitingMinutes);
        Assert.Equal(2_000, request.WaitingAdditionalChargeCop);
    }

    [Fact]
    public void Ride_request_cannot_propose_less_than_current_minimum_fare()
    {
        Assert.Throws<DomainRuleViolationException>(() => new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_499,
            3_500,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Counter_offer_can_be_accepted_and_applies_the_agreed_price()
    {
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);

        request.MakeCounterOffer(Guid.NewGuid(), 4_200, DateTimeOffset.UtcNow);
        request.AcceptCounterOffer(DateTimeOffset.UtcNow);

        Assert.Equal(RideRequestStatus.Accepted, request.Status);
        Assert.Equal(4_200, request.ProposedPriceCop);
        Assert.Equal(4_200, request.CounterOfferPriceCop);
        Assert.NotNull(request.AcceptedAtUtc);
    }

    [Fact]
    public void Counter_offer_below_minimum_is_rejected_and_rejection_reopens_request()
    {
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleViolationException>(() => request.MakeCounterOffer(Guid.NewGuid(), 3_499, DateTimeOffset.UtcNow));
        request.MakeCounterOffer(Guid.NewGuid(), 4_200, DateTimeOffset.UtcNow);
        request.RejectCounterOffer();

        Assert.Equal(RideRequestStatus.Pending, request.Status);
        Assert.Null(request.AssignedDriverProfileId);
        Assert.Null(request.CounterOfferPriceCop);
    }
}
