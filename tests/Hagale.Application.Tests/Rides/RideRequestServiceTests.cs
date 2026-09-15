using Hagale.Application.Contracts;
using Hagale.Application.Rides;
using Hagale.Domain.Pricing;
using Hagale.Domain.Drivers;
using Hagale.Domain.Rides;

namespace Hagale.Application.Tests.Rides;

public sealed class RideRequestServiceTests
{
    [Fact]
    public async Task Customer_can_create_and_cancel_own_request()
    {
        var customerUserId = Guid.NewGuid();
        var repository = new InMemoryRideRequestRepository();
        var pricingRepository = new InMemoryPricingRuleRepository();
        var service = new RideRequestService(repository, pricingRepository, new InMemoryDriverRepository(), new ExistingUserDirectory(customerUserId), TimeProvider.System);

        var created = await service.CreateAsync(customerUserId, new CreateRideRequestCommand(
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            3_500,
            RideServiceType.Motorcycle));
        var cancelled = await service.CancelAsync(customerUserId, created.Value!.Id, new CancelRideRequestCommand("Cambio de planes."));

        Assert.True(created.IsSuccess);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(RideRequestStatus.Cancelled, cancelled.Value!.Status);
    }

    [Fact]
    public async Task Created_request_exposes_payment_method_and_fare_mode()
    {
        var customerUserId = Guid.NewGuid();
        var service = new RideRequestService(
            new InMemoryRideRequestRepository(),
            new InMemoryPricingRuleRepository(),
            new InMemoryDriverRepository(),
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        var created = await service.CreateAsync(customerUserId, new CreateRideRequestCommand(
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            4_000,
            RideServiceType.Motorcycle,
            PaymentMethod: RidePaymentMethod.Nequi,
            FareMode: RideFareMode.DynamicFare));

        Assert.True(created.IsSuccess);
        Assert.Equal(RidePaymentMethod.Nequi, created.Value!.PaymentMethod);
        Assert.Equal(RideFareMode.DynamicFare, created.Value!.FareMode);
    }

    [Fact]
    public async Task Creating_a_request_notifies_only_its_customer_and_the_driver_dispatch()
    {
        var customerUserId = Guid.NewGuid();
        var notifier = new RecordingRideRealtimeNotifier();
        var service = new RideRequestService(
            new InMemoryRideRequestRepository(),
            new InMemoryPricingRuleRepository(),
            new InMemoryDriverRepository(),
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System,
            notifier);

        var created = await service.CreateAsync(customerUserId, new CreateRideRequestCommand(
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            3_500,
            RideServiceType.Motorcycle));

        Assert.True(created.IsSuccess);
        var rideChange = Assert.Single(notifier.RideChanges);
        Assert.Equal(customerUserId, rideChange.CustomerUserId);
        Assert.Null(rideChange.DriverUserId);
        Assert.Equal(created.Value!.Id, rideChange.RideRequestId);
        var dispatchChange = Assert.Single(notifier.DispatchChanges);
        Assert.Equal("BUC", dispatchChange.OperatingCityCode);
        Assert.Equal(RideServiceType.Motorcycle, dispatchChange.ServiceType);
        Assert.Empty(notifier.DriverLocationChanges);
    }

    [Fact]
    public async Task Customer_cannot_create_a_second_open_request_but_can_create_after_cancelling()
    {
        var customerUserId = Guid.NewGuid();
        var repository = new InMemoryRideRequestRepository();
        var service = new RideRequestService(
            repository,
            new InMemoryPricingRuleRepository(),
            new InMemoryDriverRepository(),
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);
        var first = await service.CreateAsync(customerUserId, new CreateRideRequestCommand(
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            3_500,
            RideServiceType.Motorcycle));
        var second = await service.CreateAsync(customerUserId, new CreateRideRequestCommand(
            "Calle 45 # 20-18, Bucaramanga",
            "Cabecera del Llano, Bucaramanga",
            "BUC",
            3_500,
            RideServiceType.Motorcycle));
        var cancelled = await service.CancelAsync(customerUserId, first.Value!.Id, new CancelRideRequestCommand(null));
        var afterCancellation = await service.CreateAsync(customerUserId, new CreateRideRequestCommand(
            "Calle 45 # 20-18, Bucaramanga",
            "Cabecera del Llano, Bucaramanga",
            "BUC",
            3_500,
            RideServiceType.Motorcycle));

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Contains("solicitud en curso", second.Error!);
        Assert.True(cancelled.IsSuccess);
        Assert.True(afterCancellation.IsSuccess);
    }

    [Fact]
    public async Task Customer_cannot_cancel_another_customers_request()
    {
        var customerUserId = Guid.NewGuid();
        var requestOwnedByAnotherCustomer = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var repository = new InMemoryRideRequestRepository();
        repository.Add(requestOwnedByAnotherCustomer);
        var service = new RideRequestService(repository, new InMemoryPricingRuleRepository(), new InMemoryDriverRepository(), new ExistingUserDirectory(customerUserId), TimeProvider.System);

        var result = await service.CancelAsync(customerUserId, requestOwnedByAnotherCustomer.Id, new CancelRideRequestCommand(null));

        Assert.False(result.IsSuccess);
        Assert.Equal(RideRequestStatus.Pending, requestOwnedByAnotherCustomer.Status);
    }

    [Fact]
    public async Task Customer_cannot_create_request_for_a_city_without_active_pricing()
    {
        var customerUserId = Guid.NewGuid();
        var service = new RideRequestService(
            new InMemoryRideRequestRepository(),
            new InMemoryPricingRuleRepository(includeDefaultRule: false),
            new InMemoryDriverRepository(),
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        var result = await service.CreateAsync(customerUserId, new CreateRideRequestCommand(
            "Calle 72 # 10-07, Bogotá",
            "Aeropuerto El Dorado, Bogotá",
            "BUC",
            3_500,
            RideServiceType.Motorcycle));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Available_driver_cannot_request_a_service_as_customer()
    {
        var userId = Guid.NewGuid();
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(CreateAvailableDriver(userId, "BUC", VehicleType.Motorcycle));
        var service = new RideRequestService(
            new InMemoryRideRequestRepository(),
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(userId),
            TimeProvider.System);

        var result = await service.CreateAsync(userId, new CreateRideRequestCommand(
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            3_500,
            RideServiceType.Motorcycle));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Available_approved_driver_can_accept_one_compatible_request()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            customerUserId,
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(request);
        var driverRepository = new InMemoryDriverRepository();
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        driverRepository.Add(driver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        var accepted = await service.AcceptAsync(driverUserId, request.Id);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(RideRequestStatus.Accepted, request.Status);
        Assert.Equal(DriverAvailabilityStatus.Busy, driver.AvailabilityStatus);
        Assert.Empty(await service.ListAvailableForDriverAsync(driverUserId));
    }

    [Fact]
    public async Task Assigning_and_advancing_a_service_notifies_only_its_participants()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            customerUserId,
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(request);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle));
        var notifier = new RecordingRideRealtimeNotifier();
        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(customerUserId, driverUserId),
            TimeProvider.System,
            notifier);

        var accepted = await service.AcceptAsync(driverUserId, request.Id);

        Assert.True(accepted.IsSuccess);
        var assignmentNotice = Assert.Single(notifier.RideChanges);
        Assert.Equal(customerUserId, assignmentNotice.CustomerUserId);
        Assert.Equal(driverUserId, assignmentNotice.DriverUserId);
        Assert.Equal(request.Id, assignmentNotice.RideRequestId);
        Assert.Single(notifier.DispatchChanges);

        notifier.Clear();
        var enRoute = await service.MarkDriverEnRouteAsync(driverUserId, request.Id);

        Assert.True(enRoute.IsSuccess);
        var journeyNotice = Assert.Single(notifier.RideChanges);
        Assert.Equal(customerUserId, journeyNotice.CustomerUserId);
        Assert.Equal(driverUserId, journeyNotice.DriverUserId);
        Assert.Empty(notifier.DispatchChanges);
        Assert.Empty(notifier.DriverLocationChanges);
    }

    [Fact]
    public async Task Driver_cannot_accept_request_from_another_city()
    {
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(request);
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(CreateAvailableDriver(driverUserId, "BOG", VehicleType.Motorcycle));
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(Guid.NewGuid()),
            TimeProvider.System);

        var accepted = await service.AcceptAsync(driverUserId, request.Id);

        Assert.False(accepted.IsSuccess);
        Assert.Equal(RideRequestStatus.Pending, request.Status);
    }

    [Fact]
    public async Task Driver_city_name_can_match_the_short_dispatch_code_used_by_a_request()
    {
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 52 # 31-27, Bucaramanga",
            "Calle 103 # 13-27, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            5_000,
            3_500,
            DateTimeOffset.UtcNow);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(request);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(CreateAvailableDriver(driverUserId, "BUCARAMANGA", VehicleType.Motorcycle));
        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(Guid.NewGuid()),
            TimeProvider.System);

        var offers = await service.ListAvailableForDriverAsync(driverUserId);

        Assert.Single(offers);
    }

    [Fact]
    public async Task Available_driver_receives_only_nearby_location_aware_offers_with_distances()
    {
        var driverUserId = Guid.NewGuid();
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        driver.UpdateCurrentLocation(7.1193m, -73.1227m, DateTimeOffset.UtcNow);

        var closeRequest = new RideRequest(
            Guid.NewGuid(),
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            6_000,
            3_500,
            7.1210m,
            -73.1200m,
            7.1300m,
            -73.1100m,
            DateTimeOffset.UtcNow);
        var distantRequest = new RideRequest(
            Guid.NewGuid(),
            "Floridablanca, Santander",
            "Piedecuesta, Santander",
            "BUC",
            RideServiceType.Motorcycle,
            6_000,
            3_500,
            7.2300m,
            -73.0600m,
            7.2400m,
            -73.0500m,
            DateTimeOffset.UtcNow);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(closeRequest);
        rides.Add(distantRequest);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(driver);
        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(Guid.NewGuid()),
            TimeProvider.System);

        var offers = await service.ListAvailableForDriverAsync(driverUserId, 3m);

        var offer = Assert.Single(offers);
        Assert.Equal(closeRequest.Id, offer.Id);
        Assert.NotNull(offer.PickupDistanceKilometers);
        Assert.NotNull(offer.TripDistanceKilometers);
        Assert.NotNull(offer.TotalDistanceKilometers);
        Assert.True(offer.PickupDistanceKilometers < 3m);
        Assert.Equal(7.1210m, offer.PickupLatitude);
        Assert.Equal(-73.1200m, offer.PickupLongitude);
        Assert.Equal(7.1300m, offer.DestinationLatitude);
        Assert.Equal(-73.1100m, offer.DestinationLongitude);
    }

    [Fact]
    public async Task Location_aware_offer_includes_a_distance_only_price_reference_from_the_active_rule()
    {
        var driverUserId = Guid.NewGuid();
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        driver.UpdateCurrentLocation(7.1193m, -73.1227m, DateTimeOffset.UtcNow);
        var request = new RideRequest(
            Guid.NewGuid(),
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            6_000,
            3_500,
            7.1210m,
            -73.1200m,
            7.1300m,
            -73.1100m,
            DateTimeOffset.UtcNow);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(request);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(driver);
        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(baseFareCop: 1_000, farePerKilometerCop: 3_000),
            drivers,
            new ExistingUserDirectory(Guid.NewGuid()),
            TimeProvider.System);

        var offer = Assert.Single(await service.ListAvailableForDriverAsync(driverUserId));

        Assert.NotNull(offer.DirectDistanceReferenceFareCop);
        Assert.True(offer.DirectDistanceReferenceFareCop > offer.MinimumFareCopAtRequest);
    }

    [Fact]
    public async Task Customer_tracking_shares_an_assigned_drivers_latest_location_only_during_an_active_service()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            customerUserId,
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            6_000,
            3_500,
            7.1210m,
            -73.1200m,
            7.1300m,
            -73.1100m,
            DateTimeOffset.UtcNow);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(request);
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(driver);
        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(customerUserId, driverUserId),
            TimeProvider.System);

        var accepted = await service.AcceptAsync(driverUserId, request.Id);
        driver.UpdateCurrentLocation(7.1222m, -73.1211m, DateTimeOffset.UtcNow);
        var tracking = await service.GetTrackingForCustomerAsync(customerUserId, request.Id);
        var otherCustomerTracking = await service.GetTrackingForCustomerAsync(Guid.NewGuid(), request.Id);

        Assert.True(accepted.IsSuccess);
        Assert.NotNull(tracking);
        Assert.Equal(RideRequestStatus.Accepted, tracking!.Status);
        Assert.Equal(7.1222m, tracking.DriverLatitude);
        Assert.Equal(-73.1211m, tracking.DriverLongitude);
        Assert.Equal(7.1210m, tracking.PickupLatitude);
        Assert.Equal(7.1300m, tracking.DestinationLatitude);
        Assert.NotNull(tracking.AcceptedAtUtc);
        Assert.NotNull(tracking.Driver);
        Assert.Equal("Perfil", tracking.Driver!.FirstName);
        Assert.Equal("HÁGALE", tracking.Driver.LastName);
        Assert.Equal("Honda", tracking.Driver.VehicleBrand);
        Assert.Equal(VehicleType.Motorcycle, tracking.Driver.VehicleType);
        Assert.NotEqual(string.Empty, tracking.Driver.VehiclePlate);
        Assert.Null(otherCustomerTracking);

        await service.MarkDriverEnRouteAsync(driverUserId, request.Id);
        await service.MarkDriverArrivedAsync(driverUserId, request.Id);
        var arrivedTracking = await service.GetTrackingForCustomerAsync(customerUserId, request.Id);

        Assert.NotNull(arrivedTracking);
        Assert.Equal(RideRequestStatus.DriverArrived, arrivedTracking!.Status);
        Assert.NotNull(arrivedTracking.DriverEnRouteAtUtc);
        Assert.NotNull(arrivedTracking.DriverArrivedAtUtc);
        Assert.Equal(7.1222m, arrivedTracking.DriverLatitude);
    }

    [Fact]
    public async Task Assigned_driver_can_progress_and_complete_ride_then_becomes_available()
    {
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(request);
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(driver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(Guid.NewGuid()),
            TimeProvider.System);

        await service.AcceptAsync(driverUserId, request.Id);
        var enRoute = await service.MarkDriverEnRouteAsync(driverUserId, request.Id);
        var arrived = await service.MarkDriverArrivedAsync(driverUserId, request.Id);
        var started = await service.StartAsync(driverUserId, request.Id);
        var completed = await service.CompleteAsync(driverUserId, request.Id);

        Assert.True(enRoute.IsSuccess);
        Assert.True(arrived.IsSuccess);
        Assert.True(started.IsSuccess);
        Assert.True(completed.IsSuccess);
        Assert.Equal(RideRequestStatus.Completed, request.Status);
        Assert.Equal(DriverAvailabilityStatus.Available, driver.AvailabilityStatus);
        Assert.Null(await service.GetCurrentForDriverAsync(driverUserId));
    }

    [Fact]
    public async Task Driver_activity_summary_uses_completed_services_only()
    {
        var driverUserId = Guid.NewGuid();
        var completedRequest = new RideRequest(
            Guid.NewGuid(),
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            4_200,
            3_500,
            7.1210m,
            -73.1200m,
            7.1300m,
            -73.1100m,
            DateTimeOffset.UtcNow);
        var nequiCompletedRequest = new RideRequest(
            Guid.NewGuid(),
            "Carrera 27 # 45-10, Bucaramanga",
            "Terminal de Transportes, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            6_000,
            3_500,
            7.1180m,
            -73.1190m,
            7.1030m,
            -73.1220m,
            RidePaymentMethod.Nequi,
            RideFareMode.PassengerOffer,
            DateTimeOffset.UtcNow);
        var pendingRequest = new RideRequest(
            Guid.NewGuid(),
            "Carrera 33 # 48-12, Bucaramanga",
            "Cabecera del Llano, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(completedRequest);
        rideRepository.Add(nequiCompletedRequest);
        rideRepository.Add(pendingRequest);
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(driver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(Guid.NewGuid()),
            TimeProvider.System);

        await service.AcceptAsync(driverUserId, completedRequest.Id);
        await service.MarkDriverEnRouteAsync(driverUserId, completedRequest.Id);
        await service.MarkDriverArrivedAsync(driverUserId, completedRequest.Id);
        await service.StartAsync(driverUserId, completedRequest.Id);
        await service.CompleteAsync(driverUserId, completedRequest.Id);
        await service.AcceptAsync(driverUserId, nequiCompletedRequest.Id);
        await service.MarkDriverEnRouteAsync(driverUserId, nequiCompletedRequest.Id);
        await service.MarkDriverArrivedAsync(driverUserId, nequiCompletedRequest.Id);
        await service.StartAsync(driverUserId, nequiCompletedRequest.Id);
        await service.CompleteAsync(driverUserId, nequiCompletedRequest.Id);

        var summary = await service.GetActivitySummaryAsync(driverUserId);

        Assert.Equal(2, summary.CompletedRideCount);
        Assert.Equal(10_200, summary.CompletedServiceValueCop);
        Assert.Equal(1, summary.CompletedCashRideCount);
        Assert.Equal(4_200, summary.CompletedCashValueCop);
        Assert.Equal(1, summary.CompletedNequiRideCount);
        Assert.Equal(6_000, summary.CompletedNequiValueCop);
        Assert.NotNull(summary.CompletedDirectDistanceKilometers);
        Assert.NotNull(summary.LastCompletedAtUtc);
    }

    [Fact]
    public async Task Customer_can_accept_a_counter_offer_from_a_compatible_driver()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            customerUserId,
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(request);
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(driver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        var offered = await service.MakeCounterOfferAsync(driverUserId, request.Id, new CreateCounterOfferCommand(4_200));
        var accepted = await service.AcceptCounterOfferAsync(customerUserId, request.Id);

        Assert.True(offered.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(RideRequestStatus.Accepted, request.Status);
        Assert.Equal(4_200, request.ProposedPriceCop);
        Assert.Equal(DriverAvailabilityStatus.Busy, driver.AvailabilityStatus);
    }

    [Fact]
    public async Task Rejecting_counter_offer_makes_the_request_and_driver_available_again()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            customerUserId,
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(request);
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(driver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        await service.MakeCounterOfferAsync(driverUserId, request.Id, new CreateCounterOfferCommand(4_200));
        var rejected = await service.RejectCounterOfferAsync(customerUserId, request.Id);

        Assert.True(rejected.IsSuccess);
        Assert.Equal(RideRequestStatus.Pending, request.Status);
        Assert.Equal(DriverAvailabilityStatus.Available, driver.AvailabilityStatus);
        Assert.Null(request.CounterOfferPriceCop);
    }

    private sealed class ExistingUserDirectory : IUserDirectory
    {
        private readonly HashSet<Guid> existingUserIds;

        public ExistingUserDirectory(params Guid[] existingUserIds) => this.existingUserIds = existingUserIds.ToHashSet();

        public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(existingUserIds.Contains(userId));

        public Task<bool> EnsureRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<BasicUserProfile?> GetBasicProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BasicUserProfile?>(existingUserIds.Contains(userId)
                ? new BasicUserProfile("Perfil", "HÁGALE")
                : null);
    }

    private sealed class RecordingRideRealtimeNotifier : IRideRealtimeNotifier
    {
        public List<(Guid CustomerUserId, Guid? DriverUserId, Guid RideRequestId)> RideChanges { get; } = [];
        public List<(string OperatingCityCode, RideServiceType ServiceType)> DispatchChanges { get; } = [];
        public List<(Guid CustomerUserId, Guid RideRequestId)> DriverLocationChanges { get; } = [];

        public Task NotifyRideChangedAsync(
            Guid customerUserId,
            Guid? driverUserId,
            Guid rideRequestId,
            CancellationToken cancellationToken = default)
        {
            RideChanges.Add((customerUserId, driverUserId, rideRequestId));
            return Task.CompletedTask;
        }

        public Task NotifyDispatchChangedAsync(
            string operatingCityCode,
            RideServiceType serviceType,
            CancellationToken cancellationToken = default)
        {
            DispatchChanges.Add((operatingCityCode, serviceType));
            return Task.CompletedTask;
        }

        public Task NotifyDriverLocationChangedAsync(
            Guid customerUserId,
            Guid rideRequestId,
            CancellationToken cancellationToken = default)
        {
            DriverLocationChanges.Add((customerUserId, rideRequestId));
            return Task.CompletedTask;
        }

        public Task NotifyRideChatMessageAsync(
            Guid customerUserId,
            Guid? driverUserId,
            Guid rideRequestId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Clear()
        {
            RideChanges.Clear();
            DispatchChanges.Clear();
            DriverLocationChanges.Clear();
        }
    }

    private sealed class InMemoryRideRequestRepository : IRideRequestRepository
    {
        private readonly List<RideRequest> _requests = [];

        public void Add(RideRequest rideRequest) => _requests.Add(rideRequest);

        public Task<RideRequest?> GetByIdAsync(Guid rideRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.SingleOrDefault(request => request.Id == rideRequestId));

        public Task<IReadOnlyCollection<RideRequest>> ListByCustomerUserIdAsync(
            Guid customerUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<RideRequest>>(
                _requests.Where(request => request.CustomerUserId == customerUserId)
                    .OrderByDescending(request => request.RequestedAtUtc)
                    .ToArray());

        public Task<IReadOnlyCollection<RideRequest>> ListPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<RideRequest>>(
                _requests.Where(request => request.Status == RideRequestStatus.Pending).ToArray());

        public Task<IReadOnlyCollection<RideRequest>> ListCompletedByDriverProfileIdAsync(
            Guid driverProfileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<RideRequest>>(
                _requests.Where(request =>
                    request.AssignedDriverProfileId == driverProfileId &&
                    request.Status == RideRequestStatus.Completed)
                    .OrderByDescending(request => request.CompletedAtUtc)
                    .ToArray());

        public Task<RideRequest?> GetActiveByDriverProfileIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.SingleOrDefault(request =>
                request.AssignedDriverProfileId == driverProfileId &&
                request.Status is RideRequestStatus.CounterOfferPending or RideRequestStatus.Accepted or RideRequestStatus.DriverEnRoute or RideRequestStatus.DriverArrived or RideRequestStatus.InProgress));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryDriverRepository : IDriverRepository
    {
        private readonly List<DriverProfile> _drivers = [];

        public void Add(DriverProfile driverProfile) => _drivers.Add(driverProfile);
        public void AddVehicle(Vehicle vehicle) { }
        public void AddDocument(DriverDocument document) { }
        public Task<DriverProfile?> GetByIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drivers.SingleOrDefault(driver => driver.Id == driverProfileId));
        public Task<DriverProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drivers.SingleOrDefault(driver => driver.UserId == userId));
        public Task<DriverProfilePage> ListAsync(DriverStatus? status, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DriverProfilePage([], 0));
        public Task<bool> IsVehiclePlateInUseAsync(string plate, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static DriverProfile CreateAvailableDriver(Guid driverUserId, string cityCode, VehicleType vehicleType)
    {
        var now = DateTimeOffset.UtcNow;
        var driver = new DriverProfile(driverUserId, now);
        driver.AddVehicle(new Vehicle("Honda", "CB125F", 2025, "Roja", $"T{Guid.NewGuid():N}"[..6], vehicleType, cityCode));
        foreach (var type in new[]
                 {
                     DriverDocumentType.PersonalIdentification,
                     DriverDocumentType.DriverLicense,
                     DriverDocumentType.VehicleRegistration,
                     DriverDocumentType.Insurance
                 })
        {
            var document = new DriverDocument(type, $"test/{type}.pdf", null);
            driver.AddDocument(document);
            document.Review(true, null, Guid.NewGuid(), now);
        }
        driver.Approve(null, now);
        driver.SetAvailability(DriverAvailabilityStatus.Available);
        return driver;
    }

    private sealed class InMemoryPricingRuleRepository : IPricingRuleRepository
    {
        private readonly List<PricingRule> _rules = [];

        public InMemoryPricingRuleRepository(
            bool includeDefaultRule = true,
            int minimumFareCop = 3_500,
            int baseFareCop = 0,
            int farePerKilometerCop = 0,
            int farePerMinuteCop = 0)
        {
            if (includeDefaultRule)
            {
                _rules.Add(new PricingRule(
                    "BUC",
                    RideServiceType.Motorcycle,
                    minimumFareCop,
                    baseFareCop,
                    farePerKilometerCop,
                    farePerMinuteCop,
                    true,
                    DateTimeOffset.UtcNow));
            }
        }

        public void Add(PricingRule pricingRule) => _rules.Add(pricingRule);

        public Task<PricingRule?> GetByIdAsync(Guid pricingRuleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rules.SingleOrDefault(rule => rule.Id == pricingRuleId));

        public Task<PricingRule?> GetByCityAndServiceAsync(
            string cityCode,
            RideServiceType serviceType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_rules.SingleOrDefault(rule => rule.CityCode == cityCode.Trim().ToUpperInvariant() && rule.ServiceType == serviceType));

        public Task<IReadOnlyCollection<PricingRule>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<PricingRule>>(
                _rules.Where(rule => !activeOnly || rule.IsActive).ToArray());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
