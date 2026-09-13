using Hagale.Domain.Common;

namespace Hagale.Domain.Rides;

public sealed class RideRequest
{
    private RideRequest()
    {
    }

    public RideRequest(
        Guid customerUserId,
        string pickupAddress,
        string destinationAddress,
        string cityCode,
        RideServiceType serviceType,
        int proposedPriceCop,
        int minimumFareCopAtRequest,
        DateTimeOffset requestedAtUtc)
        : this(
            customerUserId,
            pickupAddress,
            destinationAddress,
            cityCode,
            serviceType,
            proposedPriceCop,
            minimumFareCopAtRequest,
            null,
            null,
            null,
            null,
            RidePaymentMethod.Cash,
            RideFareMode.PassengerOffer,
            requestedAtUtc)
    {
    }

    public RideRequest(
        Guid customerUserId,
        string pickupAddress,
        string destinationAddress,
        string cityCode,
        RideServiceType serviceType,
        int proposedPriceCop,
        int minimumFareCopAtRequest,
        decimal? pickupLatitude,
        decimal? pickupLongitude,
        decimal? destinationLatitude,
        decimal? destinationLongitude,
        DateTimeOffset requestedAtUtc)
        : this(
            customerUserId,
            pickupAddress,
            destinationAddress,
            cityCode,
            serviceType,
            proposedPriceCop,
            minimumFareCopAtRequest,
            pickupLatitude,
            pickupLongitude,
            destinationLatitude,
            destinationLongitude,
            RidePaymentMethod.Cash,
            RideFareMode.PassengerOffer,
            requestedAtUtc)
    {
    }

    public RideRequest(
        Guid customerUserId,
        string pickupAddress,
        string destinationAddress,
        string cityCode,
        RideServiceType serviceType,
        int proposedPriceCop,
        int minimumFareCopAtRequest,
        decimal? pickupLatitude,
        decimal? pickupLongitude,
        decimal? destinationLatitude,
        decimal? destinationLongitude,
        RidePaymentMethod paymentMethod,
        RideFareMode fareMode,
        DateTimeOffset requestedAtUtc)
    {
        if (customerUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La solicitud debe estar asociada a una cuenta válida.");
        }

        Id = Guid.NewGuid();
        CustomerUserId = customerUserId;
        PickupAddress = NormalizeLocation(pickupAddress, "origen");
        DestinationAddress = NormalizeLocation(destinationAddress, "destino");
        if (PickupAddress.Equals(DestinationAddress, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleViolationException("El origen y el destino deben ser diferentes.");
        }

        OperatingCityCode = NormalizeCityCode(cityCode);
        ServiceType = serviceType;
        if (!Enum.IsDefined(typeof(RidePaymentMethod), paymentMethod))
        {
            throw new DomainRuleViolationException("El método de pago no es válido.");
        }

        if (!Enum.IsDefined(typeof(RideFareMode), fareMode))
        {
            throw new DomainRuleViolationException("El modo de tarifa no es válido.");
        }

        PaymentMethod = paymentMethod;
        FareMode = fareMode;
        if (minimumFareCopAtRequest <= 0)
        {
            throw new DomainRuleViolationException("La tarifa mínima de referencia debe ser mayor que cero.");
        }

        if (proposedPriceCop < minimumFareCopAtRequest)
        {
            throw new DomainRuleViolationException("La oferta no puede ser inferior a la tarifa mínima vigente.");
        }

        ProposedPriceCop = proposedPriceCop;
        MinimumFareCopAtRequest = minimumFareCopAtRequest;
        GeoCoordinates.EnsureOptionalPair(pickupLatitude, pickupLongitude, "recogida");
        GeoCoordinates.EnsureOptionalPair(destinationLatitude, destinationLongitude, "destino");
        PickupLatitude = pickupLatitude;
        PickupLongitude = pickupLongitude;
        DestinationLatitude = destinationLatitude;
        DestinationLongitude = destinationLongitude;
        RequestedAtUtc = requestedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CustomerUserId { get; private set; }
    public string PickupAddress { get; private set; } = null!;
    public string DestinationAddress { get; private set; } = null!;
    public string OperatingCityCode { get; private set; } = null!;
    public RideServiceType ServiceType { get; private set; }
    public RidePaymentMethod PaymentMethod { get; private set; } = RidePaymentMethod.Cash;
    public RideFareMode FareMode { get; private set; } = RideFareMode.PassengerOffer;
    public int ProposedPriceCop { get; private set; }
    public int MinimumFareCopAtRequest { get; private set; }
    public decimal? PickupLatitude { get; private set; }
    public decimal? PickupLongitude { get; private set; }
    public decimal? DestinationLatitude { get; private set; }
    public decimal? DestinationLongitude { get; private set; }
    public RideRequestStatus Status { get; private set; } = RideRequestStatus.Pending;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? AssignedDriverProfileId { get; private set; }
    public int? CounterOfferPriceCop { get; private set; }
    public DateTimeOffset? CounterOfferAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? DriverEnRouteAtUtc { get; private set; }
    public DateTimeOffset? DriverArrivedAtUtc { get; private set; }
    public DateTimeOffset? WaitingStartedAtUtc { get; private set; }
    public DateTimeOffset? WaitingEndedAtUtc { get; private set; }
    public int? IncludedWaitingMinutesAtStart { get; private set; }
    public int? AdditionalWaitingFarePerMinuteCopAtStart { get; private set; }
    public int? AdditionalWaitingMinutes { get; private set; }
    public int? WaitingAdditionalChargeCop { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    public void Accept(Guid driverProfileId, DateTimeOffset acceptedAtUtc)
    {
        if (driverProfileId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La aceptación debe identificar a un conductor válido.");
        }

        if (Status != RideRequestStatus.Pending)
        {
            throw new DomainRuleViolationException("Esta solicitud ya no está disponible para aceptación.");
        }

        AssignedDriverProfileId = driverProfileId;
        AcceptedAtUtc = acceptedAtUtc;
        Status = RideRequestStatus.Accepted;
    }

    public void MakeCounterOffer(Guid driverProfileId, int priceCop, DateTimeOffset offeredAtUtc)
    {
        if (driverProfileId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La contraoferta debe identificar a un conductor válido.");
        }

        if (Status != RideRequestStatus.Pending)
        {
            throw new DomainRuleViolationException("Esta solicitud ya no está disponible para una contraoferta.");
        }

        if (priceCop < MinimumFareCopAtRequest)
        {
            throw new DomainRuleViolationException("La contraoferta no puede ser inferior a la tarifa mínima vigente.");
        }

        AssignedDriverProfileId = driverProfileId;
        CounterOfferPriceCop = priceCop;
        CounterOfferAtUtc = offeredAtUtc;
        Status = RideRequestStatus.CounterOfferPending;
    }

    public void AcceptCounterOffer(DateTimeOffset acceptedAtUtc)
    {
        EnsureStatus(RideRequestStatus.CounterOfferPending, "No hay una contraoferta pendiente para aceptar.");
        if (AssignedDriverProfileId is null || CounterOfferPriceCop is null)
        {
            throw new DomainRuleViolationException("La contraoferta no contiene información válida.");
        }

        ProposedPriceCop = CounterOfferPriceCop.Value;
        AcceptedAtUtc = acceptedAtUtc;
        Status = RideRequestStatus.Accepted;
    }

    public void RejectCounterOffer()
    {
        EnsureStatus(RideRequestStatus.CounterOfferPending, "No hay una contraoferta pendiente para rechazar.");
        ResetCounterOffer();
        Status = RideRequestStatus.Pending;
    }

    public void MarkDriverEnRoute(DateTimeOffset changedAtUtc)
    {
        EnsureStatus(RideRequestStatus.Accepted, "Solo una solicitud aceptada puede marcarse como conductor en camino.");
        DriverEnRouteAtUtc = changedAtUtc;
        Status = RideRequestStatus.DriverEnRoute;
    }

    public void MarkDriverArrived(
        DateTimeOffset changedAtUtc,
        int? includedWaitingMinutes = null,
        int? additionalWaitingFarePerMinuteCop = null)
    {
        EnsureStatus(RideRequestStatus.DriverEnRoute, "Solo un conductor en camino puede marcar que llegó a la recogida.");
        if (includedWaitingMinutes.HasValue != additionalWaitingFarePerMinuteCop.HasValue)
        {
            throw new DomainRuleViolationException("La configuración de espera debe estar completa.");
        }

        if (includedWaitingMinutes is < 0 or > 1_440)
        {
            throw new DomainRuleViolationException("Los minutos de espera incluidos deben estar entre 0 y 1.440.");
        }

        if (additionalWaitingFarePerMinuteCop < 0)
        {
            throw new DomainRuleViolationException("El valor por minuto adicional de espera no puede ser negativo.");
        }

        DriverArrivedAtUtc = changedAtUtc;
        WaitingStartedAtUtc = includedWaitingMinutes.HasValue ? changedAtUtc : null;
        WaitingEndedAtUtc = null;
        IncludedWaitingMinutesAtStart = includedWaitingMinutes;
        AdditionalWaitingFarePerMinuteCopAtStart = additionalWaitingFarePerMinuteCop;
        AdditionalWaitingMinutes = null;
        WaitingAdditionalChargeCop = null;
        Status = RideRequestStatus.DriverArrived;
    }

    public void Start(DateTimeOffset changedAtUtc)
    {
        EnsureStatus(RideRequestStatus.DriverArrived, "El viaje solo puede iniciarse cuando el conductor haya llegado a la recogida.");
        CloseWaiting(changedAtUtc);
        StartedAtUtc = changedAtUtc;
        Status = RideRequestStatus.InProgress;
    }

    public void Complete(DateTimeOffset changedAtUtc)
    {
        EnsureStatus(RideRequestStatus.InProgress, "Solo un viaje iniciado puede finalizarse.");
        CompletedAtUtc = changedAtUtc;
        Status = RideRequestStatus.Completed;
    }

    public void Cancel(string? reason, DateTimeOffset cancelledAtUtc)
    {
        if (Status is not (RideRequestStatus.Pending or RideRequestStatus.CounterOfferPending))
        {
            throw new DomainRuleViolationException("Solo una solicitud pendiente o con contraoferta puede cancelarse.");
        }

        Status = RideRequestStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancellationReason = NormalizeOptionalReason(reason);
    }

    private static string NormalizeLocation(string value, string label)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length is < 5 or > 250)
        {
            throw new DomainRuleViolationException($"El {label} debe tener entre 5 y 250 caracteres.");
        }

        return normalized;
    }

    private void EnsureStatus(RideRequestStatus expectedStatus, string errorMessage)
    {
        if (Status != expectedStatus)
        {
            throw new DomainRuleViolationException(errorMessage);
        }
    }

    private void CloseWaiting(DateTimeOffset endedAtUtc)
    {
        if (!WaitingStartedAtUtc.HasValue)
        {
            return;
        }

        if (endedAtUtc < WaitingStartedAtUtc.Value)
        {
            throw new DomainRuleViolationException("La espera no puede finalizar antes de comenzar.");
        }

        var elapsedMinutes = (endedAtUtc - WaitingStartedAtUtc.Value).TotalMinutes;
        if (elapsedMinutes > int.MaxValue)
        {
            throw new DomainRuleViolationException("El tiempo de espera excede el límite permitido.");
        }

        var elapsedWholeMinutes = elapsedMinutes <= 0 ? 0 : (int)Math.Ceiling(elapsedMinutes);
        var includedMinutes = IncludedWaitingMinutesAtStart ?? 0;
        var additionalMinutes = Math.Max(0, elapsedWholeMinutes - includedMinutes);
        var ratePerMinute = AdditionalWaitingFarePerMinuteCopAtStart ?? 0;
        int additionalCharge;
        try
        {
            additionalCharge = checked(additionalMinutes * ratePerMinute);
        }
        catch (OverflowException)
        {
            throw new DomainRuleViolationException("El valor adicional de espera excede el límite permitido.");
        }

        WaitingEndedAtUtc = endedAtUtc;
        AdditionalWaitingMinutes = additionalMinutes;
        WaitingAdditionalChargeCop = additionalCharge;
    }

    private void ResetCounterOffer()
    {
        AssignedDriverProfileId = null;
        CounterOfferPriceCop = null;
        CounterOfferAtUtc = null;
    }

    private static string NormalizeCityCode(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20 || normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new DomainRuleViolationException("La ciudad de operación no es válida.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalReason(string? value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 500)
        {
            throw new DomainRuleViolationException("La razón de cancelación no puede exceder 500 caracteres.");
        }

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
