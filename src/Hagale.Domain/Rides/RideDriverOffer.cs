using Hagale.Domain.Common;

namespace Hagale.Domain.Rides;

/// <summary>
/// Oferta privada de un conductor durante una ronda de despacho. Los datos de
/// contacto no se guardan aquí ni se exponen antes de la selección.
/// </summary>
public sealed class RideDriverOffer
{
    private RideDriverOffer()
    {
    }

    internal RideDriverOffer(
        Guid rideRequestId,
        Guid driverProfileId,
        int offeredPriceCop,
        DateTimeOffset offeredAtUtc,
        DateTimeOffset expiresAtUtc,
        int searchRound,
        bool hasResponded = true)
    {
        if (rideRequestId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La oferta debe pertenecer a una solicitud válida.");
        }

        if (driverProfileId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La oferta debe identificar a un conductor válido.");
        }

        if (offeredPriceCop <= 0)
        {
            throw new DomainRuleViolationException("El valor de la oferta debe ser mayor que cero.");
        }

        if (expiresAtUtc <= offeredAtUtc)
        {
            throw new DomainRuleViolationException("La vigencia de la oferta no es válida.");
        }

        if (searchRound < 1)
        {
            throw new DomainRuleViolationException("La ronda de despacho no es válida.");
        }

        Id = Guid.NewGuid();
        RideRequestId = rideRequestId;
        DriverProfileId = driverProfileId;
        OfferedPriceCop = offeredPriceCop;
        OfferedAtUtc = offeredAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        SearchRound = searchRound;
        HasResponded = hasResponded;
    }

    public Guid Id { get; private set; }
    public Guid RideRequestId { get; private set; }
    public Guid DriverProfileId { get; private set; }
    public int OfferedPriceCop { get; private set; }
    public DateTimeOffset OfferedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public int SearchRound { get; private set; }
    public bool HasResponded { get; private set; } = true;
    public RideDriverOfferStatus Status { get; private set; } = RideDriverOfferStatus.Pending;
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public bool IsPendingAt(DateTimeOffset atUtc) =>
        Status == RideDriverOfferStatus.Pending && atUtc < ExpiresAtUtc;

    public bool IsPersonalDispatchTurnAt(DateTimeOffset atUtc) =>
        !HasResponded && IsPendingAt(atUtc);

    internal void RecordDispatchResponse(int offeredPriceCop, DateTimeOffset respondedAtUtc)
    {
        if (!IsPersonalDispatchTurnAt(respondedAtUtc))
        {
            throw new DomainRuleViolationException("El tiempo para responder este aviso terminó. La solicitud pasó al siguiente conductor.");
        }

        if (offeredPriceCop <= 0)
        {
            throw new DomainRuleViolationException("El valor de la oferta debe ser mayor que cero.");
        }

        OfferedPriceCop = offeredPriceCop;
        OfferedAtUtc = respondedAtUtc;
        ExpiresAtUtc = respondedAtUtc.AddSeconds(RideRequest.OfferCollectionWindowSeconds);
        HasResponded = true;
    }

    internal void Select(DateTimeOffset resolvedAtUtc) => Resolve(RideDriverOfferStatus.Selected, resolvedAtUtc);

    internal void Decline(DateTimeOffset resolvedAtUtc) => Resolve(RideDriverOfferStatus.NotSelected, resolvedAtUtc);

    internal void Expire(DateTimeOffset resolvedAtUtc) => Resolve(RideDriverOfferStatus.Expired, resolvedAtUtc);

    internal void Withdraw(DateTimeOffset resolvedAtUtc) => Resolve(RideDriverOfferStatus.Withdrawn, resolvedAtUtc);

    private void Resolve(RideDriverOfferStatus status, DateTimeOffset resolvedAtUtc)
    {
        if (Status != RideDriverOfferStatus.Pending)
        {
            return;
        }

        Status = status;
        ResolvedAtUtc = resolvedAtUtc;
    }
}
