using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;

namespace Hagale.Application.Contracts;

public interface IPricingRuleRepository
{
    Task<PricingRule?> GetByIdAsync(Guid pricingRuleId, CancellationToken cancellationToken = default);
    Task<PricingRule?> GetByCityAndServiceAsync(string cityCode, RideServiceType serviceType, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PricingRule>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default);
    void Add(PricingRule pricingRule);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
