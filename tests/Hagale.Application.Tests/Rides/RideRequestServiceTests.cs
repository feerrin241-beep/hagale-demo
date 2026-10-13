using Hagale.Application.Contracts;
using Hagale.Application.Common;
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
    public async Task Available_approved_driver_can_send_an_offer_and_customer_can_select_it()
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

        var offered = await service.AcceptAsync(driverUserId, request.Id);

        Assert.True(offered.IsSuccess);
        Assert.Equal(RideRequestStatus.Pending, request.Status);
        Assert.Equal(DriverAvailabilityStatus.Available, driver.AvailabilityStatus);
        Assert.NotNull(offered.Value!.PendingDriverOfferId);
        Assert.Equal(3_500, offered.Value.PendingDriverOfferPriceCop);
        Assert.Empty(await service.ListAvailableForDriverAsync(driverUserId));

        var board = await service.GetDriverOffersForCustomerAsync(customerUserId, request.Id);
        var offer = Assert.Single(board!.Offers);
        Assert.Equal(3_500, offer.OfferedPriceCop);

        var selected = await service.SelectDriverOfferAsync(customerUserId, request.Id, offer.Id);

        Assert.True(selected.IsSuccess);
        Assert.Equal(RideRequestStatus.Accepted, request.Status);
        Assert.Equal(DriverAvailabilityStatus.Busy, driver.AvailabilityStatus);
    }

    [Fact]
    public async Task Driver_offer_reloads_and_retries_when_another_driver_updates_the_request()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var request = new RideRequest(
            customerUserId,
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            8_000,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(request);
        rideRepository.BeforeNextSaveFailure = currentRequest =>
        {
            var offersField = typeof(RideRequest).GetField(
                "_driverOffers",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var offers = (List<RideDriverOffer>)offersField.GetValue(currentRequest)!;
            offers.RemoveAll(offer => offer.DriverProfileId == driver.Id);
            currentRequest.AddDriverOffer(Guid.NewGuid(), 8_500, DateTimeOffset.UtcNow);
        };
        var drivers = new InMemoryDriverRepository();
        drivers.Add(driver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        var offered = await service.AcceptAsync(driverUserId, request.Id);

        Assert.True(offered.IsSuccess);
        Assert.Equal(2, request.DriverOffers.Count);
        Assert.Contains(request.DriverOffers, offer => offer.DriverProfileId == driver.Id && offer.OfferedPriceCop == 8_000);
    }

    [Fact]
    public async Task Counter_offer_preserves_selected_price_and_reaches_customer_after_repeated_concurrent_updates()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var request = new RideRequest(
            customerUserId,
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            5_700,
            3_500,
            DateTimeOffset.UtcNow);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(request);
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(driver);

        var simulatedConcurrentUpdatesRemaining = 2;
        void SimulateConcurrentUpdate(RideRequest currentRequest)
        {
            var offersField = typeof(RideRequest).GetField(
                "_driverOffers",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var offers = (List<RideDriverOffer>)offersField.GetValue(currentRequest)!;
            offers.RemoveAll(offer => offer.DriverProfileId == driver.Id);
            currentRequest.AddDriverOffer(Guid.NewGuid(), 8_500 + simulatedConcurrentUpdatesRemaining * 500, DateTimeOffset.UtcNow);
            simulatedConcurrentUpdatesRemaining--;
            if (simulatedConcurrentUpdatesRemaining > 0)
            {
                rides.BeforeNextSaveFailure = SimulateConcurrentUpdate;
            }
        }
        rides.BeforeNextSaveFailure = SimulateConcurrentUpdate;

        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(customerUserId, driverUserId),
            TimeProvider.System);

        var submitted = await service.MakeCounterOfferAsync(
            driverUserId,
            request.Id,
            new CreateCounterOfferCommand(6_200));
        var customerBoard = await service.GetDriverOffersForCustomerAsync(customerUserId, request.Id);

        Assert.True(submitted.IsSuccess, submitted.Error);
        Assert.Equal(6_200, submitted.Value!.PendingDriverOfferPriceCop);
        var customerOffer = Assert.Single(customerBoard!.Offers);
        Assert.Equal(6_200, customerOffer.OfferedPriceCop);
    }

    [Fact]
    public async Task Customer_keeps_a_recent_driver_offer_when_the_next_search_round_starts()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var requestedAt = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new AdjustableTimeProvider(requestedAt.AddSeconds(55));
        var request = new RideRequest(
            customerUserId,
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            5_700,
            3_500,
            requestedAt);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(request);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle));
        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(customerUserId, driverUserId),
            timeProvider);

        var submitted = await service.MakeCounterOfferAsync(
            driverUserId,
            request.Id,
            new CreateCounterOfferCommand(6_200));
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var customerBoard = await service.GetDriverOffersForCustomerAsync(customerUserId, request.Id);

        Assert.True(submitted.IsSuccess, submitted.Error);
        Assert.Equal(2, customerBoard!.SearchRound);
        var customerOffer = Assert.Single(customerBoard.Offers);
        Assert.Equal(6_200, customerOffer.OfferedPriceCop);
    }

    [Fact]
    public async Task Customer_selection_is_confirmed_if_the_same_offer_was_committed_during_a_retry()
    {
        var customerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var request = new RideRequest(
            customerUserId,
            "Calle 72 # 10-07, Bucaramanga",
            "Aeropuerto Palonegro, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            8_000,
            3_500,
            DateTimeOffset.UtcNow);
        var rides = new InMemoryRideRequestRepository();
        rides.Add(request);
        var drivers = new InMemoryDriverRepository();
        drivers.Add(driver);
        var service = new RideRequestService(
            rides,
            new InMemoryPricingRuleRepository(),
            drivers,
            new ExistingUserDirectory(customerUserId, driverUserId),
            TimeProvider.System);
        Assert.True((await service.AcceptAsync(driverUserId, request.Id)).IsSuccess);
        var board = await service.GetDriverOffersForCustomerAsync(customerUserId, request.Id);
        var offerId = Assert.Single(board!.Offers).Id;
        rides.BeforeNextSaveFailure = _ => { };

        var selected = await service.SelectDriverOfferAsync(customerUserId, request.Id, offerId);

        Assert.True(selected.IsSuccess);
        Assert.Equal(RideRequestStatus.Accepted, selected.Value!.Status);
        Assert.Equal(driver.Id, request.AssignedDriverProfileId);
    }

    [Fact]
    public async Task Selecting_and_advancing_a_service_notifies_only_its_participants()
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

        var offered = await service.AcceptAsync(driverUserId, request.Id);

        Assert.True(offered.IsSuccess);
        var offerNotice = Assert.Single(notifier.RideChanges);
        Assert.Equal(customerUserId, offerNotice.CustomerUserId);
        Assert.Null(offerNotice.DriverUserId);
        Assert.Equal(request.Id, offerNotice.RideRequestId);
        Assert.Single(notifier.DispatchChanges);

        notifier.Clear();
        var board = await service.GetDriverOffersForCustomerAsync(customerUserId, request.Id);
        var selected = await service.SelectDriverOfferAsync(customerUserId, request.Id, Assert.Single(board!.Offers).Id);

        Assert.True(selected.IsSuccess);
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

        var accepted = await SendOfferAndSelectAsync(service, customerUserId, driverUserId, request.Id);
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

        await SendOfferAndSelectAsync(service, request.CustomerUserId, driverUserId, request.Id);
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

        await SendOfferAndSelectAsync(service, completedRequest.CustomerUserId, driverUserId, completedRequest.Id);
        await service.MarkDriverEnRouteAsync(driverUserId, completedRequest.Id);
        await service.MarkDriverArrivedAsync(driverUserId, completedRequest.Id);
        await service.StartAsync(driverUserId, completedRequest.Id);
        await service.CompleteAsync(driverUserId, completedRequest.Id);
        await SendOfferAndSelectAsync(service, nequiCompletedRequest.CustomerUserId, driverUserId, nequiCompletedRequest.Id);
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
    public async Task Customer_can_compare_multiple_driver_offers_and_select_the_preferred_driver()
    {
        var customerUserId = Guid.NewGuid();
        var firstDriverUserId = Guid.NewGuid();
        var preferredDriverUserId = Guid.NewGuid();
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
        var firstDriver = CreateAvailableDriver(firstDriverUserId, "BUC", VehicleType.Motorcycle);
        var preferredDriver = CreateAvailableDriver(preferredDriverUserId, "BUC", VehicleType.Motorcycle);
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(firstDriver);
        driverRepository.Add(preferredDriver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        var firstOffer = await service.MakeCounterOfferAsync(firstDriverUserId, request.Id, new CreateCounterOfferCommand(4_200));
        var preferredOffer = await service.MakeCounterOfferAsync(preferredDriverUserId, request.Id, new CreateCounterOfferCommand(4_700));
        var board = await service.GetDriverOffersForCustomerAsync(customerUserId, request.Id);

        Assert.True(firstOffer.IsSuccess);
        Assert.True(preferredOffer.IsSuccess);
        Assert.Equal(RideRequestStatus.Pending, request.Status);
        Assert.Equal(DriverAvailabilityStatus.Available, firstDriver.AvailabilityStatus);
        Assert.Equal(DriverAvailabilityStatus.Available, preferredDriver.AvailabilityStatus);
        Assert.Equal([4_200, 4_700], board!.Offers.Select(offer => offer.OfferedPriceCop));

        var selectedOffer = Assert.Single(board.Offers, offer => offer.OfferedPriceCop == 4_700);
        var accepted = await service.SelectDriverOfferAsync(customerUserId, request.Id, selectedOffer.Id);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(RideRequestStatus.Accepted, request.Status);
        Assert.Equal(4_700, request.ProposedPriceCop);
        Assert.Equal(DriverAvailabilityStatus.Available, firstDriver.AvailabilityStatus);
        Assert.Equal(DriverAvailabilityStatus.Busy, preferredDriver.AvailabilityStatus);
    }

    [Fact]
    public async Task Driver_cannot_send_a_second_live_offer_while_waiting_for_customer_selection()
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
        var anotherRequest = new RideRequest(
            Guid.NewGuid(),
            "Carrera 33 # 48-16, Bucaramanga",
            "Calle 56 # 27-45, Bucaramanga",
            "BUC",
            RideServiceType.Motorcycle,
            3_500,
            3_500,
            DateTimeOffset.UtcNow);
        var rideRepository = new InMemoryRideRequestRepository();
        rideRepository.Add(request);
        rideRepository.Add(anotherRequest);
        var driver = CreateAvailableDriver(driverUserId, "BUC", VehicleType.Motorcycle);
        var driverRepository = new InMemoryDriverRepository();
        driverRepository.Add(driver);
        var service = new RideRequestService(
            rideRepository,
            new InMemoryPricingRuleRepository(),
            driverRepository,
            new ExistingUserDirectory(customerUserId),
            TimeProvider.System);

        var firstOffer = await service.MakeCounterOfferAsync(driverUserId, request.Id, new CreateCounterOfferCommand(4_200));
        var secondOffer = await service.MakeCounterOfferAsync(driverUserId, anotherRequest.Id, new CreateCounterOfferCommand(4_200));
        var pendingOffer = await service.GetPendingOfferForDriverAsync(driverUserId);

        Assert.True(firstOffer.IsSuccess);
        Assert.False(secondOffer.IsSuccess);
        Assert.Contains("esperando la elección", secondOffer.Error!);
        Assert.Equal(RideRequestStatus.Pending, request.Status);
        Assert.Equal(DriverAvailabilityStatus.Available, driver.AvailabilityStatus);
        Assert.NotNull(pendingOffer);
        Assert.Equal(request.Id, pendingOffer!.Id);
        Assert.Equal(4_200, pendingOffer.PendingDriverOfferPriceCop);
    }

    private static async Task<ApplicationResult<RideRequestDto>> SendOfferAndSelectAsync(
        RideRequestService service,
        Guid customerUserId,
        Guid driverUserId,
        Guid rideRequestId)
    {
        var offered = await service.AcceptAsync(driverUserId, rideRequestId);
        Assert.True(offered.IsSuccess);

        var board = await service.GetDriverOffersForCustomerAsync(customerUserId, rideRequestId);
        var offer = Assert.Single(board!.Offers);
        var selected = await service.SelectDriverOfferAsync(customerUserId, rideRequestId, offer.Id);
        Assert.True(selected.IsSuccess);
        return selected;
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

    private sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset currentUtcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => currentUtcNow;

        public void Advance(TimeSpan duration) => currentUtcNow += duration;
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

        public Action<RideRequest>? BeforeNextSaveFailure { get; set; }

        public void Add(RideRequest rideRequest) => _requests.Add(rideRequest);

        public Task<RideRequest?> GetByIdAsync(Guid rideRequestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.SingleOrDefault(request => request.Id == rideRequestId));

        public Task<RideRequest?> ReloadByIdAsync(Guid rideRequestId, CancellationToken cancellationToken = default) =>
            GetByIdAsync(rideRequestId, cancellationToken);

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

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (BeforeNextSaveFailure is { } simulateConcurrentUpdate)
            {
                BeforeNextSaveFailure = null;
                simulateConcurrentUpdate(_requests[^1]);
                throw new Hagale.Application.Common.ConcurrentUpdateException("Simulated concurrent update.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryDriverRepository : IDriverRepository
    {
        private readonly List<DriverProfile> _drivers = [];

        public void Add(DriverProfile driverProfile) => _drivers.Add(driverProfile);
        public void AddVehicle(Vehicle vehicle) { }
        public void AddDocument(DriverDocument document) { }
        public Task<DriverProfile?> GetByIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drivers.SingleOrDefault(driver => driver.Id == driverProfileId));
        public Task<DriverProfile?> ReloadByIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
            GetByIdAsync(driverProfileId, cancellationToken);
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
