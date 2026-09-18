using Hagale.Domain.Common;
using Hagale.Domain.Rides;

namespace Hagale.Domain.Pricing;

public sealed class PricingRule
{
    public const int DefaultIncludedWaitingMinutes = 5;
    public const int DefaultAdditionalWaitingFarePerMinuteCop = 1_000;
    public const int DefaultFairOfferMinimumPercent = 90;
    public const int DefaultFavorableOfferMinimumPercent = 105;

    private PricingRule()
    {
    }

    public PricingRule(
        string cityCode,
        RideServiceType serviceType,
        int minimumFareCop,
        int baseFareCop,
        int farePerKilometerCop,
        int farePerMinuteCop,
        bool isActive,
        DateTimeOffset updatedAtUtc,
        int includedWaitingMinutes = DefaultIncludedWaitingMinutes,
        int additionalWaitingFarePerMinuteCop = DefaultAdditionalWaitingFarePerMinuteCop,
        int fairOfferMinimumPercent = DefaultFairOfferMinimumPercent,
        int favorableOfferMinimumPercent = DefaultFavorableOfferMinimumPercent)
    {
        Id = Guid.NewGuid();
        Apply(
            cityCode,
            serviceType,
            minimumFareCop,
            baseFareCop,
            farePerKilometerCop,
            farePerMinuteCop,
            isActive,
            updatedAtUtc,
            includedWaitingMinutes,
            additionalWaitingFarePerMinuteCop,
            fairOfferMinimumPercent,
            favorableOfferMinimumPercent);
    }

    public Guid Id { get; private set; }
    public string CityCode { get; private set; } = null!;
    public RideServiceType ServiceType { get; private set; }
    public int MinimumFareCop { get; private set; }
    public int BaseFareCop { get; private set; }
    public int FarePerKilometerCop { get; private set; }
    public int FarePerMinuteCop { get; private set; }
    public int IncludedWaitingMinutes { get; private set; }
    public int AdditionalWaitingFarePerMinuteCop { get; private set; }
    public int FairOfferMinimumPercent { get; private set; }
    public int FavorableOfferMinimumPercent { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    public void Update(
        int minimumFareCop,
        int baseFareCop,
        int farePerKilometerCop,
        int farePerMinuteCop,
        bool isActive,
        DateTimeOffset updatedAtUtc,
        int includedWaitingMinutes = DefaultIncludedWaitingMinutes,
        int additionalWaitingFarePerMinuteCop = DefaultAdditionalWaitingFarePerMinuteCop,
        int fairOfferMinimumPercent = DefaultFairOfferMinimumPercent,
        int favorableOfferMinimumPercent = DefaultFavorableOfferMinimumPercent) =>
        Apply(
            CityCode,
            ServiceType,
            minimumFareCop,
            baseFareCop,
            farePerKilometerCop,
            farePerMinuteCop,
            isActive,
            updatedAtUtc,
            includedWaitingMinutes,
            additionalWaitingFarePerMinuteCop,
            fairOfferMinimumPercent,
            favorableOfferMinimumPercent);

    public int CalculateRecommendedFareCop(decimal estimatedDistanceKilometers, int estimatedDurationMinutes)
    {
        if (estimatedDistanceKilometers < 0 || estimatedDurationMinutes < 0)
        {
            throw new DomainRuleViolationException("La distancia y el tiempo estimados no pueden ser negativos.");
        }

        var distanceComponent = decimal.Ceiling(estimatedDistanceKilometers) * FarePerKilometerCop;
        var timeComponent = (decimal)estimatedDurationMinutes * FarePerMinuteCop;
        var calculatedFare = (decimal)BaseFareCop + distanceComponent + timeComponent;
        if (calculatedFare > int.MaxValue)
        {
            throw new DomainRuleViolationException("La tarifa calculada excede el límite permitido.");
        }

        return Math.Max(MinimumFareCop, (int)calculatedFare);
    }

    private void Apply(
        string cityCode,
        RideServiceType serviceType,
        int minimumFareCop,
        int baseFareCop,
        int farePerKilometerCop,
        int farePerMinuteCop,
        bool isActive,
        DateTimeOffset updatedAtUtc,
        int includedWaitingMinutes,
        int additionalWaitingFarePerMinuteCop,
        int fairOfferMinimumPercent,
        int favorableOfferMinimumPercent)
    {
        CityCode = NormalizeCityCode(cityCode);
        ServiceType = serviceType;
        if (minimumFareCop <= 0)
        {
            throw new DomainRuleViolationException("La tarifa mínima debe ser mayor que cero.");
        }

        if (baseFareCop < 0 || farePerKilometerCop < 0 || farePerMinuteCop < 0)
        {
            throw new DomainRuleViolationException("Los componentes de tarifa no pueden ser negativos.");
        }

        if (includedWaitingMinutes is < 0 or > 1_440)
        {
            throw new DomainRuleViolationException("Los minutos de espera incluidos deben estar entre 0 y 1.440.");
        }

        if (additionalWaitingFarePerMinuteCop < 0)
        {
            throw new DomainRuleViolationException("El valor por minuto adicional de espera no puede ser negativo.");
        }

        if (fairOfferMinimumPercent is < 1 or > 1_000 || favorableOfferMinimumPercent is < 1 or > 1_000)
        {
            throw new DomainRuleViolationException("Los rangos de clasificación deben estar entre 1 y 1.000 por ciento.");
        }

        if (favorableOfferMinimumPercent < fairOfferMinimumPercent)
        {
            throw new DomainRuleViolationException("El rango favorable no puede ser menor que el rango justo.");
        }

        MinimumFareCop = minimumFareCop;
        BaseFareCop = baseFareCop;
        FarePerKilometerCop = farePerKilometerCop;
        FarePerMinuteCop = farePerMinuteCop;
        IncludedWaitingMinutes = includedWaitingMinutes;
        AdditionalWaitingFarePerMinuteCop = additionalWaitingFarePerMinuteCop;
        FairOfferMinimumPercent = fairOfferMinimumPercent;
        FavorableOfferMinimumPercent = favorableOfferMinimumPercent;
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string NormalizeCityCode(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20 || normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new DomainRuleViolationException("El código de ciudad no es válido.");
        }

        return normalized;
    }
}
