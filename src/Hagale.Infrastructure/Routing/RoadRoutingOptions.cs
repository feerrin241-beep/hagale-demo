namespace Hagale.Infrastructure.Routing;

public sealed class RoadRoutingOptions
{
    public const string SectionName = "Routing";

    /// <summary>
    /// The public OSRM endpoint is suitable for the controlled HÁGALE demo.
    /// Production can point this setting at a managed or self-hosted instance
    /// without changing mobile code.
    /// </summary>
    public string? OsrmBaseUrl { get; init; }

    public int TimeoutSeconds { get; init; } = 8;

    public int CacheMinutes { get; init; } = 5;
}
