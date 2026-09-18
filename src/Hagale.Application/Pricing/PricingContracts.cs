using Hagale.Application.Common;
using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;

namespace Hagale.Application.Pricing;

public sealed record CreatePricingRuleCommand(
    string CityCode,
    RideServiceType ServiceType,
    int MinimumFareCop,
    int BaseFareCop,
    int FarePerKilometerCop,
    int FarePerMinuteCop,
    bool IsActive,
    int IncludedWaitingMinutes = PricingRule.DefaultIncludedWaitingMinutes,
    int AdditionalWaitingFarePerMinuteCop = PricingRule.DefaultAdditionalWaitingFarePerMinuteCop,
    int FairOfferMinimumPercent = PricingRule.DefaultFairOfferMinimumPercent,
    int FavorableOfferMinimumPercent = PricingRule.DefaultFavorableOfferMinimumPercent);

public sealed record UpdatePricingRuleCommand(
    int MinimumFareCop,
    int BaseFareCop,
    int FarePerKilometerCop,
    int FarePerMinuteCop,
    bool IsActive,
    int IncludedWaitingMinutes = PricingRule.DefaultIncludedWaitingMinutes,
    int AdditionalWaitingFarePerMinuteCop = PricingRule.DefaultAdditionalWaitingFarePerMinuteCop,
    int FairOfferMinimumPercent = PricingRule.DefaultFairOfferMinimumPercent,
    int FavorableOfferMinimumPercent = PricingRule.DefaultFavorableOfferMinimumPercent);

public sealed record PricingQuoteRequest(
    string CityCode,
    RideServiceType ServiceType,
    decimal EstimatedDistanceKilometers,
    int EstimatedDurationMinutes);

public sealed record PricingRuleDto(
    Guid Id,
    string CityCode,
    RideServiceType ServiceType,
    int MinimumFareCop,
    int BaseFareCop,
    int FarePerKilometerCop,
    int FarePerMinuteCop,
    int IncludedWaitingMinutes,
    int AdditionalWaitingFarePerMinuteCop,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc,
    int FairOfferMinimumPercent = PricingRule.DefaultFairOfferMinimumPercent,
    int FavorableOfferMinimumPercent = PricingRule.DefaultFavorableOfferMinimumPercent);

public sealed record PricingQuoteDto(
    string CityCode,
    RideServiceType ServiceType,
    decimal EstimatedDistanceKilometers,
    int EstimatedDurationMinutes,
    int MinimumFareCop,
    int RecommendedFareCop);

public interface IPricingService
{
    Task<IReadOnlyCollection<PricingRuleDto>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PricingRuleDto>> ListForAdministrationAsync(CancellationToken cancellationToken = default);
    Task<ApplicationResult<PricingRuleDto>> CreateRuleAsync(CreatePricingRuleCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PricingRuleDto>> UpdateRuleAsync(Guid pricingRuleId, UpdatePricingRuleCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<PricingQuoteDto>> QuoteAsync(PricingQuoteRequest request, CancellationToken cancellationToken = default);
}
