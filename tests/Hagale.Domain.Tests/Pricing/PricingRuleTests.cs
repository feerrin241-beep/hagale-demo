using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;

namespace Hagale.Domain.Tests.Pricing;

public sealed class PricingRuleTests
{
    [Fact]
    public void Recommended_fare_never_falls_below_configured_minimum()
    {
        var rule = new PricingRule("BUC", RideServiceType.Motorcycle, 3_500, 0, 0, 0, true, DateTimeOffset.UtcNow);

        var fare = rule.CalculateRecommendedFareCop(0, 0);

        Assert.Equal(3_500, fare);
    }

    [Fact]
    public void Recommended_fare_uses_configured_base_distance_and_time_components()
    {
        var rule = new PricingRule("BUC", RideServiceType.Motorcycle, 3_500, 2_000, 700, 300, true, DateTimeOffset.UtcNow);

        var fare = rule.CalculateRecommendedFareCop(3.2m, 5);

        Assert.Equal(6_300, fare);
    }
}
