using Hagale.Application.Common;
using Hagale.Application.Contracts;
using Hagale.Domain.Common;
using Hagale.Domain.Pricing;

namespace Hagale.Application.Pricing;

public sealed class PricingService(
    IPricingRuleRepository pricingRuleRepository,
    TimeProvider timeProvider) : IPricingService
{
    public async Task<IReadOnlyCollection<PricingRuleDto>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        (await pricingRuleRepository.ListAsync(activeOnly: true, cancellationToken))
        .Select(Map)
        .ToArray();

    public async Task<IReadOnlyCollection<PricingRuleDto>> ListForAdministrationAsync(CancellationToken cancellationToken = default) =>
        (await pricingRuleRepository.ListAsync(activeOnly: false, cancellationToken))
        .Select(Map)
        .ToArray();

    public async Task<ApplicationResult<PricingRuleDto>> CreateRuleAsync(
        CreatePricingRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            var pricingRule = new PricingRule(
                command.CityCode,
                command.ServiceType,
                command.MinimumFareCop,
                command.BaseFareCop,
                command.FarePerKilometerCop,
                command.FarePerMinuteCop,
                command.IsActive,
                timeProvider.GetUtcNow(),
                command.IncludedWaitingMinutes,
                command.AdditionalWaitingFarePerMinuteCop,
                command.FairOfferMinimumPercent,
                command.FavorableOfferMinimumPercent);
            var existing = await pricingRuleRepository.GetByCityAndServiceAsync(pricingRule.CityCode, pricingRule.ServiceType, cancellationToken);
            if (existing is not null)
            {
                return ApplicationResult<PricingRuleDto>.Failure("Ya existe una regla para esa ciudad y servicio.");
            }

            pricingRuleRepository.Add(pricingRule);
            await pricingRuleRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<PricingRuleDto>.Success(Map(pricingRule));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<PricingRuleDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<PricingRuleDto>> UpdateRuleAsync(
        Guid pricingRuleId,
        UpdatePricingRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pricingRule = await pricingRuleRepository.GetByIdAsync(pricingRuleId, cancellationToken);
        if (pricingRule is null)
        {
            return ApplicationResult<PricingRuleDto>.Failure("La regla de tarifa no existe.");
        }

        try
        {
            pricingRule.Update(
                command.MinimumFareCop,
                command.BaseFareCop,
                command.FarePerKilometerCop,
                command.FarePerMinuteCop,
                command.IsActive,
                timeProvider.GetUtcNow(),
                command.IncludedWaitingMinutes,
                command.AdditionalWaitingFarePerMinuteCop,
                command.FairOfferMinimumPercent,
                command.FavorableOfferMinimumPercent);
            await pricingRuleRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<PricingRuleDto>.Success(Map(pricingRule));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<PricingRuleDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<PricingQuoteDto>> QuoteAsync(
        PricingQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pricingRule = await pricingRuleRepository.GetByCityAndServiceAsync(request.CityCode, request.ServiceType, cancellationToken);
        if (pricingRule is null || !pricingRule.IsActive)
        {
            return ApplicationResult<PricingQuoteDto>.Failure("No hay una tarifa activa para la ciudad y servicio seleccionados.");
        }

        try
        {
            return ApplicationResult<PricingQuoteDto>.Success(new PricingQuoteDto(
                pricingRule.CityCode,
                pricingRule.ServiceType,
                request.EstimatedDistanceKilometers,
                request.EstimatedDurationMinutes,
                pricingRule.MinimumFareCop,
                pricingRule.CalculateRecommendedFareCop(request.EstimatedDistanceKilometers, request.EstimatedDurationMinutes)));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<PricingQuoteDto>.Failure(exception.Message);
        }
    }

    private static PricingRuleDto Map(PricingRule pricingRule) => new(
        pricingRule.Id,
        pricingRule.CityCode,
        pricingRule.ServiceType,
        pricingRule.MinimumFareCop,
        pricingRule.BaseFareCop,
        pricingRule.FarePerKilometerCop,
        pricingRule.FarePerMinuteCop,
        pricingRule.IncludedWaitingMinutes,
        pricingRule.AdditionalWaitingFarePerMinuteCop,
        pricingRule.IsActive,
        pricingRule.UpdatedAtUtc,
        pricingRule.FairOfferMinimumPercent,
        pricingRule.FavorableOfferMinimumPercent);
}
