using Hagale.Application.Contracts;
using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class PricingRuleRepository(HagaleDbContext database) : IPricingRuleRepository
{
    public void Add(PricingRule pricingRule) => database.PricingRules.Add(pricingRule);

    public Task<PricingRule?> GetByIdAsync(Guid pricingRuleId, CancellationToken cancellationToken = default) =>
        database.PricingRules.SingleOrDefaultAsync(rule => rule.Id == pricingRuleId, cancellationToken);

    public Task<PricingRule?> GetByCityAndServiceAsync(
        string cityCode,
        RideServiceType serviceType,
        CancellationToken cancellationToken = default)
    {
        var normalizedCityCode = cityCode?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalizedCityCode)
            ? Task.FromResult<PricingRule?>(null)
            : database.PricingRules.SingleOrDefaultAsync(
                rule => rule.CityCode == normalizedCityCode && rule.ServiceType == serviceType,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<PricingRule>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = database.PricingRules.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(rule => rule.IsActive);
        }

        return await query
            .OrderBy(rule => rule.CityCode)
            .ThenBy(rule => rule.ServiceType)
            .ToArrayAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}
