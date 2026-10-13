using Hagale.Application.Common;
using Hagale.Application.Contracts;
using Hagale.Domain.Common;
using Hagale.Domain.Drivers;
using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;

namespace Hagale.Application.Rides;

public sealed class RideRequestService(
    IRideRequestRepository rideRequestRepository,
    IPricingRuleRepository pricingRuleRepository,
    IDriverRepository driverRepository,
    IUserDirectory userDirectory,
    TimeProvider timeProvider,
    IRideRealtimeNotifier? realtimeNotifier = null,
    IRideRatingRepository? rideRatingRepository = null) : IRideRequestService
{
    private readonly IRideRealtimeNotifier realtimeNotifier = realtimeNotifier ?? NullRideRealtimeNotifier.Instance;
    private readonly IRideRatingRepository? ratingRepository = rideRatingRepository;

    public async Task<ApplicationResult<RideRequestDto>> CreateAsync(
        Guid customerUserId,
        CreateRideRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await userDirectory.ExistsAsync(customerUserId, cancellationToken))
        {
            return ApplicationResult<RideRequestDto>.Failure("La cuenta no está disponible.");
        }

        var driverProfile = await driverRepository.GetByUserIdAsync(customerUserId, cancellationToken);
        if (driverProfile?.AvailabilityStatus is DriverAvailabilityStatus.Available or DriverAvailabilityStatus.Busy)
        {
            return ApplicationResult<RideRequestDto>.Failure("No puedes solicitar un servicio mientras estás disponible o en viaje como conductor.");
        }

        var existingRequests = await rideRequestRepository.ListByCustomerUserIdAsync(customerUserId, cancellationToken);
        if (existingRequests.Any(IsOpenForCustomer))
        {
            return ApplicationResult<RideRequestDto>.Failure("Ya tienes una solicitud en curso. Revisa su estado o finalízala antes de solicitar otro recorrido.");
        }

        var pricingRule = await pricingRuleRepository.GetByCityAndServiceAsync(
            command.OperatingCityCode,
            command.ServiceType,
            cancellationToken);
        if (pricingRule is null || !pricingRule.IsActive)
        {
            return ApplicationResult<RideRequestDto>.Failure("No hay una tarifa activa para la ciudad y servicio seleccionados.");
        }

        try
        {
            var rideRequest = new RideRequest(
                customerUserId,
                command.PickupAddress,
                command.DestinationAddress,
                pricingRule.CityCode,
                command.ServiceType,
                command.ProposedPriceCop,
                pricingRule.MinimumFareCop,
                command.PickupLatitude,
                command.PickupLongitude,
                command.DestinationLatitude,
                command.DestinationLongitude,
                command.PaymentMethod,
                command.FareMode,
                timeProvider.GetUtcNow());
            rideRequestRepository.Add(rideRequest);
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(rideRequest, driver: null, dispatchChanged: true, cancellationToken);
            return ApplicationResult<RideRequestDto>.Success(Map(rideRequest));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<RideRequestDto>.Failure(exception.Message);
        }
    }

    public async Task<IReadOnlyCollection<RideRequestDto>> ListMineAsync(
        Guid customerUserId,
        CancellationToken cancellationToken = default)
    {
        var requests = await rideRequestRepository.ListByCustomerUserIdAsync(customerUserId, cancellationToken);
        return requests.Select(Map).ToArray();
    }

    public async Task<CustomerRideTrackingDto?> GetTrackingForCustomerAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.CustomerUserId != customerUserId)
        {
            return null;
        }

        var shareDriverLocation = request.Status is RideRequestStatus.Accepted
            or RideRequestStatus.DriverEnRoute
            or RideRequestStatus.DriverArrived
            or RideRequestStatus.InProgress;
        var driver = shareDriverLocation && request.AssignedDriverProfileId.HasValue
            ? await driverRepository.GetByIdAsync(request.AssignedDriverProfileId.Value, cancellationToken)
            : null;
        var driverSummary = driver is null
            ? null
            : await BuildAssignedDriverSummaryAsync(driver, cancellationToken);

        return new CustomerRideTrackingDto(
            request.Id,
            request.Status,
            request.AcceptedAtUtc,
            request.DriverEnRouteAtUtc,
            request.DriverArrivedAtUtc,
            request.WaitingStartedAtUtc,
            request.WaitingEndedAtUtc,
            request.IncludedWaitingMinutesAtStart,
            request.AdditionalWaitingFarePerMinuteCopAtStart,
            request.AdditionalWaitingMinutes,
            request.WaitingAdditionalChargeCop,
            request.StartedAtUtc,
            driverSummary,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DestinationLatitude,
            request.DestinationLongitude,
            driver?.LastKnownLatitude,
            driver?.LastKnownLongitude,
            driver?.LocationUpdatedAtUtc);
    }

    private async Task<AssignedDriverSummaryDto?> BuildAssignedDriverSummaryAsync(
        DriverProfile driver,
        CancellationToken cancellationToken)
    {
        var user = await userDirectory.GetBasicProfileAsync(driver.UserId, cancellationToken);
        var vehicle = driver.Vehicles.FirstOrDefault(item => item.IsActive)
            ?? driver.Vehicles.FirstOrDefault();
        return user is null || vehicle is null
            ? null
            : new AssignedDriverSummaryDto(
                user.FirstName,
                user.LastName,
                vehicle.Brand,
                vehicle.Model,
                vehicle.Year,
                vehicle.Color,
                vehicle.Plate,
                vehicle.Type);
    }

    public async Task<ApplicationResult<RideRequestDto>> CancelAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancelRideRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var rideRequest = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (rideRequest is null || rideRequest.CustomerUserId != customerUserId)
        {
            return ApplicationResult<RideRequestDto>.Failure("La solicitud no está disponible.");
        }

        var releasesCounterOfferDriver = rideRequest.Status == RideRequestStatus.CounterOfferPending;
        var assignedDriver = rideRequest.AssignedDriverProfileId is not null
            ? await driverRepository.GetByIdAsync(rideRequest.AssignedDriverProfileId.Value, cancellationToken)
            : null;

        try
        {
            rideRequest.Cancel(command.Reason, timeProvider.GetUtcNow());
            if (assignedDriver?.Status == DriverStatus.Approved && releasesCounterOfferDriver)
            {
                assignedDriver.SetAvailability(DriverAvailabilityStatus.Available);
            }
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(rideRequest, assignedDriver, dispatchChanged: true, cancellationToken);
            return ApplicationResult<RideRequestDto>.Success(Map(rideRequest));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<RideRequestDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<RideRequestDto>> AcceptCounterOfferAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.CustomerUserId != customerUserId)
        {
            return ApplicationResult<RideRequestDto>.Failure("La solicitud no está disponible.");
        }

        var driver = request.AssignedDriverProfileId is null
            ? null
            : await driverRepository.GetByIdAsync(request.AssignedDriverProfileId.Value, cancellationToken);
        if (driver is null || driver.Status != DriverStatus.Approved ||
            driver.AvailabilityStatus != DriverAvailabilityStatus.Busy || !CanServe(driver, request))
        {
            return ApplicationResult<RideRequestDto>.Failure("El conductor ya no está disponible para esta contraoferta.");
        }

        try
        {
            request.AcceptCounterOffer(timeProvider.GetUtcNow());
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver, dispatchChanged: true, cancellationToken);
            return ApplicationResult<RideRequestDto>.Success(Map(request));
        }
        catch (ConcurrentUpdateException)
        {
            return ApplicationResult<RideRequestDto>.Failure("La contraoferta fue actualizada desde otra operación. Actualiza la pantalla.");
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<RideRequestDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<RideRequestDto>> RejectCounterOfferAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.CustomerUserId != customerUserId)
        {
            return ApplicationResult<RideRequestDto>.Failure("La solicitud no está disponible.");
        }

        var driver = request.AssignedDriverProfileId is null
            ? null
            : await driverRepository.GetByIdAsync(request.AssignedDriverProfileId.Value, cancellationToken);

        try
        {
            request.RejectCounterOffer();
            if (driver?.Status == DriverStatus.Approved)
            {
                driver.SetAvailability(DriverAvailabilityStatus.Available);
            }
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver, dispatchChanged: true, cancellationToken);
            return ApplicationResult<RideRequestDto>.Success(Map(request));
        }
        catch (ConcurrentUpdateException)
        {
            return ApplicationResult<RideRequestDto>.Failure("La contraoferta fue actualizada desde otra operación. Actualiza la pantalla.");
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<RideRequestDto>.Failure(exception.Message);
        }
    }

    public async Task<CustomerDriverOfferBoardDto?> GetDriverOffersForCustomerAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.CustomerUserId != customerUserId || request.Status != RideRequestStatus.Pending)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (!request.IsOfferWindowOpenAt(now))
        {
            try
            {
                request.RestartOfferCollection(now);
                await rideRequestRepository.SaveChangesAsync(cancellationToken);
                await NotifyRideChangedAsync(request, driver: null, dispatchChanged: true, cancellationToken);
            }
            catch (ConcurrentUpdateException)
            {
                // Otra consulta ya abrió la siguiente ronda; vuelve a leerla.
                request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
                if (request is null || request.CustomerUserId != customerUserId || request.Status != RideRequestStatus.Pending)
                {
                    return null;
                }
            }
            catch (DomainRuleViolationException)
            {
                return null;
            }
        }

        var boardNow = timeProvider.GetUtcNow();
        var availableOffers = request.DriverOffers
            .Where(offer => offer.SearchRound == request.OfferSearchRound && offer.IsPendingAt(boardNow))
            .OrderBy(offer => offer.OfferedPriceCop)
            .ThenBy(offer => offer.OfferedAtUtc)
            .ToArray();
        var customerOffers = new List<CustomerDriverOfferDto>(availableOffers.Length);

        foreach (var offer in availableOffers)
        {
            var driver = await driverRepository.GetByIdAsync(offer.DriverProfileId, cancellationToken);
            if (!CanReceiveRequests(driver) || !CanServe(driver!, request))
            {
                continue;
            }

            var customerOffer = await BuildCustomerDriverOfferAsync(request, offer, driver!, cancellationToken);
            if (customerOffer is not null)
            {
                customerOffers.Add(customerOffer);
                if (customerOffers.Count == 8)
                {
                    break;
                }
            }
        }

        return new CustomerDriverOfferBoardDto(
            request.Id,
            request.GetOfferWindowEndUtc(),
            request.OfferSearchRound,
            request.IsOfferWindowOpenAt(boardNow),
            customerOffers);
    }

    public async Task<ApplicationResult<RideRequestDto>> SelectDriverOfferAsync(
        Guid customerUserId,
        Guid rideRequestId,
        Guid driverOfferId,
        CancellationToken cancellationToken = default)
    {
        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.CustomerUserId != customerUserId)
        {
            return ApplicationResult<RideRequestDto>.Failure("La solicitud no está disponible.");
        }

        var offer = request.DriverOffers.SingleOrDefault(item => item.Id == driverOfferId);
        if (offer is null)
        {
            return ApplicationResult<RideRequestDto>.Failure("La oferta ya no está disponible. Actualiza las propuestas recibidas.");
        }

        var driver = await driverRepository.GetByIdAsync(offer.DriverProfileId, cancellationToken);
        if (!CanReceiveRequests(driver) || !CanServe(driver!, request))
        {
            return ApplicationResult<RideRequestDto>.Failure("El conductor ya no está disponible. Elige otra propuesta.");
        }

        try
        {
            request.SelectDriverOffer(driverOfferId, timeProvider.GetUtcNow());
            driver!.SetAvailability(DriverAvailabilityStatus.Busy);
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver, dispatchChanged: true, cancellationToken);
            return ApplicationResult<RideRequestDto>.Success(Map(request));
        }
        catch (ConcurrentUpdateException)
        {
            return await RecoverConcurrentDriverOfferSelectionAsync(
                customerUserId,
                rideRequestId,
                driverOfferId,
                driver!.Id,
                cancellationToken);
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<RideRequestDto>.Failure(exception.Message);
        }
    }

    private async Task<ApplicationResult<RideRequestDto>> RecoverConcurrentDriverOfferSelectionAsync(
        Guid customerUserId,
        Guid rideRequestId,
        Guid driverOfferId,
        Guid driverProfileId,
        CancellationToken cancellationToken)
    {
        var request = await rideRequestRepository.ReloadByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.CustomerUserId != customerUserId)
        {
            return ApplicationResult<RideRequestDto>.Failure("La solicitud ya no está disponible.");
        }

        if (request.Status == RideRequestStatus.Accepted && request.AssignedDriverProfileId == driverProfileId)
        {
            // A repeated selection after the first one committed is already complete.
            return ApplicationResult<RideRequestDto>.Success(Map(request));
        }

        if (request.Status != RideRequestStatus.Pending)
        {
            return ApplicationResult<RideRequestDto>.Failure(
                "La solicitud ya fue asignada o cerrada. Actualicé el estado del servicio.");
        }

        var now = timeProvider.GetUtcNow();
        var offer = request.DriverOffers.SingleOrDefault(item =>
            item.Id == driverOfferId &&
            item.SearchRound == request.OfferSearchRound &&
            item.IsPendingAt(now));
        if (offer is null || !request.IsOfferWindowOpenAt(now))
        {
            return ApplicationResult<RideRequestDto>.Failure(
                "Esa propuesta ya venció o cambió de ronda. Actualiza la lista y elige una oferta vigente.");
        }

        var driver = await driverRepository.ReloadByIdAsync(driverProfileId, cancellationToken);
        if (!CanReceiveRequests(driver) || !CanServe(driver!, request) ||
            await rideRequestRepository.GetActiveByDriverProfileIdAsync(driverProfileId, cancellationToken) is not null)
        {
            return ApplicationResult<RideRequestDto>.Failure(
                "Ese conductor ya no está disponible. Actualicé las propuestas para que elijas otra.");
        }

        try
        {
            request.SelectDriverOffer(driverOfferId, now);
            driver!.SetAvailability(DriverAvailabilityStatus.Busy);
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver, dispatchChanged: true, cancellationToken);
            return ApplicationResult<RideRequestDto>.Success(Map(request));
        }
        catch (ConcurrentUpdateException)
        {
            return ApplicationResult<RideRequestDto>.Failure(
                "Las propuestas cambiaron otra vez. Actualiza la lista; si ya asignaron el servicio, verás el seguimiento.");
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<RideRequestDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<RideRequestDto>> RestartOfferCollectionAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.CustomerUserId != customerUserId)
        {
            return ApplicationResult<RideRequestDto>.Failure("La solicitud no está disponible.");
        }

        try
        {
            request.RestartOfferCollection(timeProvider.GetUtcNow());
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver: null, dispatchChanged: true, cancellationToken);
            return ApplicationResult<RideRequestDto>.Success(Map(request));
        }
        catch (ConcurrentUpdateException)
        {
            return ApplicationResult<RideRequestDto>.Failure("La búsqueda fue actualizada desde otra operación. Actualiza la pantalla.");
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<RideRequestDto>.Failure(exception.Message);
        }
    }

    public async Task<IReadOnlyCollection<DriverRideRequestDto>> ListAvailableForDriverAsync(
        Guid driverUserId,
        decimal? maximumPickupDistanceKilometers = null,
        CancellationToken cancellationToken = default)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (!CanReceiveRequests(driver))
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();
        var allPendingRequests = await rideRequestRepository.ListPendingAsync(cancellationToken);
        // Un conductor mantiene solo una propuesta viva a la vez. Así no puede
        // ser elegido por dos pasajeros simultáneamente mientras sigue libre.
        if (allPendingRequests.Any(request => request.HasPendingOfferFromDriver(driver!.Id, now)))
        {
            return [];
        }

        var pendingRequests = allPendingRequests
            .Where(request => request.IsOfferWindowOpenAt(now))
            .Where(request => CanServe(driver!, request))
            .Select(request => new
            {
                Request = request,
                PickupDistance = CalculatePickupDistance(driver!, request)
            });

        if (maximumPickupDistanceKilometers is > 0)
        {
            pendingRequests = pendingRequests.Where(item =>
                !item.PickupDistance.HasValue || item.PickupDistance.Value <= maximumPickupDistanceKilometers.Value);
        }

        var activePricingRules = await pricingRuleRepository.ListAsync(activeOnly: true, cancellationToken);
        return pendingRequests
            .OrderBy(item => item.PickupDistance ?? decimal.MaxValue)
            .ThenBy(item => item.Request.RequestedAtUtc)
            .Select(item =>
            {
                var pricingRule = FindPricingRule(activePricingRules, item.Request);
                return MapForDriver(
                    item.Request,
                    item.PickupDistance,
                    CalculateDirectDistanceReferenceFare(pricingRule, item.Request),
                    pricingRule);
            })
            .ToArray();
    }

    public async Task<DriverRideRequestDto?> GetCurrentForDriverAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        var request = await rideRequestRepository.GetActiveByDriverProfileIdAsync(driver.Id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var pricingRule = await pricingRuleRepository.GetByCityAndServiceAsync(
            request.OperatingCityCode,
            request.ServiceType,
            cancellationToken);
        return MapForDriver(
            request,
            CalculatePickupDistance(driver, request),
            CalculateDirectDistanceReferenceFare(pricingRule, request),
            pricingRule);
    }

    public async Task<DriverRideRequestDto?> GetPendingOfferForDriverAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (driver is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var pendingRequestAndOffer = (await rideRequestRepository.ListPendingAsync(cancellationToken))
            .SelectMany(request => request.DriverOffers
                .Where(offer => offer.DriverProfileId == driver.Id && offer.IsPendingAt(now))
                .Select(offer => new { Request = request, Offer = offer }))
            .OrderBy(item => item.Offer.ExpiresAtUtc)
            .FirstOrDefault();
        if (pendingRequestAndOffer is null)
        {
            return null;
        }

        var pricingRule = await pricingRuleRepository.GetByCityAndServiceAsync(
            pendingRequestAndOffer.Request.OperatingCityCode,
            pendingRequestAndOffer.Request.ServiceType,
            cancellationToken);
        return MapForDriver(
            pendingRequestAndOffer.Request,
            CalculatePickupDistance(driver, pendingRequestAndOffer.Request),
            CalculateDirectDistanceReferenceFare(pricingRule, pendingRequestAndOffer.Request),
            pricingRule,
            pendingRequestAndOffer.Offer);
    }

    public async Task<DriverActivitySummaryDto> GetActivitySummaryAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (driver is null)
        {
            return new DriverActivitySummaryDto(0, 0, 0, 0, 0, 0, 0, null, null);
        }

        var completedRequests = await rideRequestRepository.ListCompletedByDriverProfileIdAsync(driver.Id, cancellationToken);
        var cashRequests = completedRequests
            .Where(request => request.PaymentMethod == RidePaymentMethod.Cash)
            .ToArray();
        var nequiRequests = completedRequests
            .Where(request => request.PaymentMethod == RidePaymentMethod.Nequi)
            .ToArray();
        var distances = completedRequests
            .Select(CalculateTripDistance)
            .Where(distance => distance.HasValue)
            .Select(distance => distance!.Value)
            .ToArray();

        return new DriverActivitySummaryDto(
            completedRequests.Count,
            completedRequests.Sum(CalculateCollectedValueCop),
            completedRequests.Sum(request => request.WaitingAdditionalChargeCop ?? 0),
            cashRequests.Length,
            cashRequests.Sum(CalculateCollectedValueCop),
            nequiRequests.Length,
            nequiRequests.Sum(CalculateCollectedValueCop),
            distances.Length == 0
                ? null
                : decimal.Round(distances.Sum(), 1, MidpointRounding.AwayFromZero),
            completedRequests.FirstOrDefault()?.CompletedAtUtc);
    }

    public async Task<IReadOnlyCollection<DriverRideRequestDto>> ListCompletedForDriverAsync(
        Guid driverUserId,
        CancellationToken cancellationToken = default)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (driver is null)
        {
            return [];
        }

        var completedRequests = await rideRequestRepository.ListCompletedByDriverProfileIdAsync(driver.Id, cancellationToken);
        return completedRequests
            .Take(12)
            .Select(request => MapForDriver(request))
            .ToArray();
    }

    public Task<ApplicationResult<DriverRideRequestDto>> AcceptAsync(
        Guid driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) =>
        SubmitDriverOfferAsync(driverUserId, rideRequestId, offeredPriceCop: null, cancellationToken);

    public async Task<ApplicationResult<DriverRideRequestDto>> MakeCounterOfferAsync(
        Guid driverUserId,
        Guid rideRequestId,
        CreateCounterOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await SubmitDriverOfferAsync(driverUserId, rideRequestId, command.PriceCop, cancellationToken);
    }

    private async Task<ApplicationResult<DriverRideRequestDto>> SubmitDriverOfferAsync(
        Guid driverUserId,
        Guid rideRequestId,
        int? offeredPriceCop,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (!CanReceiveRequests(driver))
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("Debes estar aprobado y disponible para enviar una oferta.");
        }

        if (await rideRequestRepository.GetActiveByDriverProfileIdAsync(driver!.Id, cancellationToken) is not null)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("Ya tienes un servicio activo.");
        }

        var now = timeProvider.GetUtcNow();
        var pendingRequests = await rideRequestRepository.ListPendingAsync(cancellationToken);
        if (pendingRequests.Any(request => request.HasPendingOfferFromDriver(driver.Id, now)))
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("Ya enviaste una oferta y estás esperando la elección del pasajero.");
        }

        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || !CanServe(driver, request))
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("La solicitud no está disponible para tu vehículo y ciudad de operación.");
        }

        try
        {
            var pricingRule = await pricingRuleRepository.GetByCityAndServiceAsync(
                request.OperatingCityCode,
                request.ServiceType,
                cancellationToken);
            var fairFare = CalculateDirectDistanceReferenceFare(pricingRule, request);
            var maximumDriverPriceCop = CalculateMaximumDriverOfferFare(fairFare, request);
            var price = offeredPriceCop ?? request.ProposedPriceCop;
            if (offeredPriceCop.HasValue && price > maximumDriverPriceCop)
            {
                return ApplicationResult<DriverRideRequestDto>.Failure(
                    $"Tu oferta no puede superar {maximumDriverPriceCop:N0} COP (máximo 100 % por encima de la tarifa justa)." );
            }

            var offer = request.AddDriverOffer(
                driver.Id,
                price,
                now,
                offeredPriceCop.HasValue ? maximumDriverPriceCop : null);
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver: null, dispatchChanged: true, cancellationToken);
            return ApplicationResult<DriverRideRequestDto>.Success(MapForDriver(
                request,
                CalculatePickupDistance(driver, request),
                CalculateDirectDistanceReferenceFare(pricingRule, request),
                pricingRule,
                offer));
        }
        catch (ConcurrentUpdateException)
        {
            return await RecoverConcurrentDriverOfferAsync(driver, rideRequestId, offeredPriceCop, cancellationToken);
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(exception.Message);
        }
    }

    private async Task<ApplicationResult<DriverRideRequestDto>> RecoverConcurrentDriverOfferAsync(
        DriverProfile driver,
        Guid rideRequestId,
        int? offeredPriceCop,
        CancellationToken cancellationToken)
    {
        var request = await rideRequestRepository.ReloadByIdAsync(rideRequestId, cancellationToken);
        if (request is null || !CanServe(driver, request))
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("La solicitud ya no está disponible para tu vehículo o ciudad.");
        }

        var now = timeProvider.GetUtcNow();
        var existingOffer = request.DriverOffers.FirstOrDefault(offer =>
            offer.DriverProfileId == driver.Id &&
            offer.SearchRound == request.OfferSearchRound &&
            offer.IsPendingAt(now));
        var pricingRule = await pricingRuleRepository.GetByCityAndServiceAsync(
            request.OperatingCityCode,
            request.ServiceType,
            cancellationToken);
        if (existingOffer is not null)
        {
            // A retried tap (or a response lost after persistence) is safe to
            // treat as success; do not create a second offer for this driver.
            return ApplicationResult<DriverRideRequestDto>.Success(MapForDriver(
                request,
                CalculatePickupDistance(driver, request),
                CalculateDirectDistanceReferenceFare(pricingRule, request),
                pricingRule,
                existingOffer));
        }

        if (request.Status != RideRequestStatus.Pending || !request.IsOfferWindowOpenAt(now))
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(
                "La solicitud cambió mientras enviabas la oferta y ya no está abierta. Actualicé el estado; revisa otra solicitud.");
        }

        if (await rideRequestRepository.GetActiveByDriverProfileIdAsync(driver.Id, cancellationToken) is not null)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("Ya tienes un servicio activo.");
        }

        var pendingRequests = await rideRequestRepository.ListPendingAsync(cancellationToken);
        if (pendingRequests.Any(pending =>
                pending.Id != rideRequestId && pending.HasPendingOfferFromDriver(driver.Id, now)))
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(
                "Ya tienes otra oferta pendiente. Espera la decisión del pasajero antes de enviar una nueva.");
        }

        var fairFare = CalculateDirectDistanceReferenceFare(pricingRule, request);
        var maximumDriverPriceCop = CalculateMaximumDriverOfferFare(fairFare, request);
        var price = offeredPriceCop ?? request.ProposedPriceCop;
        if (offeredPriceCop.HasValue && price > maximumDriverPriceCop)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(
                $"Tu oferta no puede superar {maximumDriverPriceCop:N0} COP (máximo 100 % por encima de la tarifa justa).");
        }

        try
        {
            var offer = request.AddDriverOffer(
                driver.Id,
                price,
                now,
                offeredPriceCop.HasValue ? maximumDriverPriceCop : null);
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver: null, dispatchChanged: true, cancellationToken);
            return ApplicationResult<DriverRideRequestDto>.Success(MapForDriver(
                request,
                CalculatePickupDistance(driver, request),
                fairFare,
                pricingRule,
                offer));
        }
        catch (ConcurrentUpdateException)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(
                "La solicitud volvió a cambiar al enviar la oferta. Actualiza la bandeja e inténtalo con una solicitud que siga disponible.");
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(exception.Message);
        }
    }

    public Task<ApplicationResult<DriverRideRequestDto>> MarkDriverEnRouteAsync(
        Guid driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(driverUserId, rideRequestId, (request, changedAtUtc) => request.MarkDriverEnRoute(changedAtUtc), false, cancellationToken);

    public Task<ApplicationResult<DriverRideRequestDto>> MarkDriverArrivedAsync(
        Guid driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) => MarkDriverArrivedWithWaitingPolicyAsync(driverUserId, rideRequestId, cancellationToken);

    private async Task<ApplicationResult<DriverRideRequestDto>> MarkDriverArrivedWithWaitingPolicyAsync(
        Guid driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (driver is null || driver.Status != DriverStatus.Approved)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("El conductor no está disponible para actualizar este servicio.");
        }

        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.AssignedDriverProfileId != driver.Id)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("El servicio no está asignado a este conductor.");
        }

        var pricingRule = await pricingRuleRepository.GetByCityAndServiceAsync(
            request.OperatingCityCode,
            request.ServiceType,
            cancellationToken);
        if (pricingRule is null || !pricingRule.IsActive)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("No hay una regla activa para registrar el tiempo de espera del servicio.");
        }

        try
        {
            request.MarkDriverArrived(
                timeProvider.GetUtcNow(),
                pricingRule.IncludedWaitingMinutes,
                pricingRule.AdditionalWaitingFarePerMinuteCop);
            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver, dispatchChanged: false, cancellationToken);
            return ApplicationResult<DriverRideRequestDto>.Success(MapForDriver(
                request,
                CalculatePickupDistance(driver, request),
                CalculateDirectDistanceReferenceFare(pricingRule, request),
                pricingRule));
        }
        catch (ConcurrentUpdateException)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("El servicio fue actualizado desde otra operación. Actualiza la pantalla.");
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(exception.Message);
        }
    }

    public Task<ApplicationResult<DriverRideRequestDto>> StartAsync(
        Guid driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(driverUserId, rideRequestId, (request, changedAtUtc) => request.Start(changedAtUtc), false, cancellationToken);

    public Task<ApplicationResult<DriverRideRequestDto>> CompleteAsync(
        Guid driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(driverUserId, rideRequestId, (request, changedAtUtc) => request.Complete(changedAtUtc), true, cancellationToken);

    private async Task<ApplicationResult<DriverRideRequestDto>> TransitionAsync(
        Guid driverUserId,
        Guid rideRequestId,
        Action<RideRequest, DateTimeOffset> transition,
        bool makeDriverAvailable,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByUserIdAsync(driverUserId, cancellationToken);
        if (driver is null || driver.Status != DriverStatus.Approved)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("El conductor no está disponible para actualizar este servicio.");
        }

        var request = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (request is null || request.AssignedDriverProfileId != driver.Id)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("El servicio no está asignado a este conductor.");
        }

        try
        {
            transition(request, timeProvider.GetUtcNow());
            if (makeDriverAvailable)
            {
                driver.SetAvailability(DriverAvailabilityStatus.Available);
            }

            await rideRequestRepository.SaveChangesAsync(cancellationToken);
            await NotifyRideChangedAsync(request, driver, dispatchChanged: makeDriverAvailable, cancellationToken);
            return ApplicationResult<DriverRideRequestDto>.Success(MapForDriver(request, CalculatePickupDistance(driver, request)));
        }
        catch (ConcurrentUpdateException)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure("El servicio fue actualizado desde otra operación. Actualiza la pantalla.");
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverRideRequestDto>.Failure(exception.Message);
        }
    }

    private async Task NotifyRideChangedAsync(
        RideRequest request,
        DriverProfile? driver,
        bool dispatchChanged,
        CancellationToken cancellationToken)
    {
        await realtimeNotifier.NotifyRideChangedAsync(
            request.CustomerUserId,
            driver?.UserId,
            request.Id,
            cancellationToken);

        if (dispatchChanged)
        {
            await realtimeNotifier.NotifyDispatchChangedAsync(
                request.OperatingCityCode,
                request.ServiceType,
                cancellationToken);
        }
    }

    private async Task<CustomerDriverOfferDto?> BuildCustomerDriverOfferAsync(
        RideRequest request,
        RideDriverOffer offer,
        DriverProfile driver,
        CancellationToken cancellationToken)
    {
        var vehicle = driver.Vehicles.FirstOrDefault(item => item.IsActive)
            ?? driver.Vehicles.FirstOrDefault();
        if (vehicle is null)
        {
            return null;
        }

        var user = await userDirectory.GetBasicProfileAsync(driver.UserId, cancellationToken);
        var rating = ratingRepository is null
            ? new DriverRatingSummary(null, 0)
            : await ratingRepository.GetDriverSummaryAsync(driver.Id, cancellationToken);
        var pickupDistance = CalculatePickupDistance(driver, request);
        return new CustomerDriverOfferDto(
            offer.Id,
            offer.OfferedPriceCop,
            offer.OfferedAtUtc,
            offer.ExpiresAtUtc,
            user?.FirstName ?? "Conductor",
            user?.LastName ?? "HÁGALE",
            vehicle.Brand,
            vehicle.Model,
            vehicle.Color,
            vehicle.Type,
            pickupDistance,
            CalculateEstimatedPickupMinutes(pickupDistance),
            rating.AverageScore,
            rating.RatingCount);
    }

    private static RideRequestDto Map(RideRequest request) => new(
        request.Id,
        request.PickupAddress,
        request.DestinationAddress,
        request.OperatingCityCode,
        request.ServiceType,
        request.PaymentMethod,
        request.FareMode,
        request.ProposedPriceCop,
        request.MinimumFareCopAtRequest,
        request.Status,
        request.RequestedAtUtc,
        request.CancelledAtUtc,
        request.CancellationReason,
        request.CounterOfferPriceCop,
        request.CounterOfferAtUtc,
        request.AcceptedAtUtc,
        request.DriverEnRouteAtUtc,
        request.DriverArrivedAtUtc,
        request.WaitingStartedAtUtc,
        request.WaitingEndedAtUtc,
        request.IncludedWaitingMinutesAtStart,
        request.AdditionalWaitingFarePerMinuteCopAtStart,
        request.AdditionalWaitingMinutes,
        request.WaitingAdditionalChargeCop,
        request.StartedAtUtc,
        request.CompletedAtUtc,
        request.PickupLatitude,
        request.PickupLongitude,
        request.DestinationLatitude,
        request.DestinationLongitude,
        request.GetOfferWindowEndUtc(),
        request.OfferSearchRound);

    private static DriverRideRequestDto MapForDriver(
        RideRequest request,
        decimal? pickupDistanceKilometers = null,
        int? directDistanceReferenceFareCop = null,
        PricingRule? pricingRule = null,
        RideDriverOffer? pendingDriverOffer = null) => new(
        request.Id,
        request.PickupAddress,
        request.DestinationAddress,
        request.OperatingCityCode,
        request.ServiceType,
        request.PaymentMethod,
        request.FareMode,
        request.ProposedPriceCop,
        request.MinimumFareCopAtRequest,
        directDistanceReferenceFareCop,
        request.Status,
        request.RequestedAtUtc,
        pickupDistanceKilometers,
        CalculateTripDistance(request),
        CalculateTotalDistance(pickupDistanceKilometers, request),
        request.CounterOfferPriceCop,
        request.CounterOfferAtUtc,
        request.AcceptedAtUtc,
        request.DriverEnRouteAtUtc,
        request.DriverArrivedAtUtc,
        request.WaitingStartedAtUtc,
        request.WaitingEndedAtUtc,
        request.IncludedWaitingMinutesAtStart,
        request.AdditionalWaitingFarePerMinuteCopAtStart,
        request.AdditionalWaitingMinutes,
        request.WaitingAdditionalChargeCop,
        request.StartedAtUtc,
        request.CompletedAtUtc,
        request.PickupLatitude,
        request.PickupLongitude,
        request.DestinationLatitude,
        request.DestinationLongitude,
        pricingRule?.FairOfferMinimumPercent ?? PricingRule.DefaultFairOfferMinimumPercent,
        pricingRule?.FavorableOfferMinimumPercent ?? PricingRule.DefaultFavorableOfferMinimumPercent,
        pendingDriverOffer?.Id,
        pendingDriverOffer?.OfferedPriceCop,
        pendingDriverOffer?.ExpiresAtUtc);

    private static decimal? CalculatePickupDistance(DriverProfile driver, RideRequest request) =>
        driver.LastKnownLatitude.HasValue && driver.LastKnownLongitude.HasValue &&
        request.PickupLatitude.HasValue && request.PickupLongitude.HasValue
            ? GeoCoordinates.CalculateDirectDistanceKilometers(
                driver.LastKnownLatitude.Value,
                driver.LastKnownLongitude.Value,
                request.PickupLatitude.Value,
                request.PickupLongitude.Value)
            : null;

    private static int? CalculateEstimatedPickupMinutes(decimal? pickupDistanceKilometers)
    {
        if (!pickupDistanceKilometers.HasValue)
        {
            return null;
        }

        // Referencia urbana conservadora: ~21 km/h. No se presenta como una
        // navegación GPS exacta, sino como la llegada aproximada del conductor.
        return Math.Max(2, (int)decimal.Ceiling(pickupDistanceKilometers.Value / 0.35m));
    }

    private static bool IsOpenForCustomer(RideRequest request) =>
        request.Status is RideRequestStatus.Pending
            or RideRequestStatus.CounterOfferPending
            or RideRequestStatus.Accepted
            or RideRequestStatus.DriverEnRoute
            or RideRequestStatus.DriverArrived
            or RideRequestStatus.InProgress;

    private static decimal? CalculateTripDistance(RideRequest request) =>
        request.PickupLatitude.HasValue && request.PickupLongitude.HasValue &&
        request.DestinationLatitude.HasValue && request.DestinationLongitude.HasValue
            ? GeoCoordinates.CalculateDirectDistanceKilometers(
                request.PickupLatitude.Value,
                request.PickupLongitude.Value,
                request.DestinationLatitude.Value,
                request.DestinationLongitude.Value)
            : null;

    private static int CalculateCollectedValueCop(RideRequest request) =>
        request.ProposedPriceCop + (request.WaitingAdditionalChargeCop ?? 0);

    private static decimal? CalculateTotalDistance(decimal? pickupDistance, RideRequest request)
    {
        var tripDistance = CalculateTripDistance(request);
        return pickupDistance.HasValue && tripDistance.HasValue
            ? decimal.Round(pickupDistance.Value + tripDistance.Value, 1, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int? FindDirectDistanceReferenceFare(
        IReadOnlyCollection<PricingRule> activePricingRules,
        RideRequest request)
    {
        var pricingRule = FindPricingRule(activePricingRules, request);
        return CalculateDirectDistanceReferenceFare(pricingRule, request);
    }

    private static PricingRule? FindPricingRule(
        IReadOnlyCollection<PricingRule> pricingRules,
        RideRequest request) =>
        pricingRules.FirstOrDefault(rule =>
            rule.CityCode.Equals(request.OperatingCityCode, StringComparison.OrdinalIgnoreCase) &&
            rule.ServiceType == request.ServiceType);

    private static int? CalculateDirectDistanceReferenceFare(PricingRule? pricingRule, RideRequest request)
    {
        var tripDistance = CalculateTripDistance(request);
        return pricingRule is null || !pricingRule.IsActive || !tripDistance.HasValue
            ? null
            : pricingRule.CalculateRecommendedFareCop(tripDistance.Value, estimatedDurationMinutes: 0);
    }

    private static int CalculateMaximumDriverOfferFare(int? directDistanceFareCop, RideRequest request)
    {
        var fairFareCop = directDistanceFareCop
            ?? Math.Max(request.ProposedPriceCop, request.MinimumFareCopAtRequest);
        return (int)Math.Min(int.MaxValue, (long)fairFareCop * 2);
    }

    private static bool CanReceiveRequests(DriverProfile? driver) =>
        driver is not null &&
        driver.Status == DriverStatus.Approved &&
        driver.AvailabilityStatus == DriverAvailabilityStatus.Available;

    private static bool CanServe(DriverProfile driver, RideRequest request) =>
        driver.Vehicles.Any(vehicle =>
            vehicle.IsActive &&
            AreCompatibleCityCodes(vehicle.OperatingCityCode, request.OperatingCityCode) &&
            IsVehicleCompatibleWithService(vehicle.Type, request.ServiceType));

    // The first development records used both "BUC" and "BUCARAMANGA".
    // A future City entity will replace this bridge with immutable city identifiers.
    private static bool AreCompatibleCityCodes(string driverCityCode, string requestCityCode)
    {
        var driverCode = NormalizeCityCodeForComparison(driverCityCode);
        var requestCode = NormalizeCityCodeForComparison(requestCityCode);
        return driverCode == requestCode ||
               driverCode.StartsWith(requestCode, StringComparison.Ordinal) ||
               requestCode.StartsWith(driverCode, StringComparison.Ordinal);
    }

    private static string NormalizeCityCodeForComparison(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static bool IsVehicleCompatibleWithService(VehicleType vehicleType, RideServiceType serviceType) =>
        serviceType switch
        {
            RideServiceType.Motorcycle => vehicleType is VehicleType.Motorcycle or VehicleType.MotorcyclePremium,
            RideServiceType.MotorcyclePremium => vehicleType == VehicleType.MotorcyclePremium,
            _ => false
        };
}
