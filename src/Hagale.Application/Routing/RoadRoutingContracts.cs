using Hagale.Application.Common;

namespace Hagale.Application.Routing;

/// <summary>
/// Coordinate pair in WGS84. Latitude and longitude are deliberately kept as
/// decimals so the same values can be shared with the ride-request model.
/// </summary>
public sealed record RoutePoint(decimal Latitude, decimal Longitude);

public sealed record RouteEstimateRequest(
    decimal OriginLatitude,
    decimal OriginLongitude,
    decimal DestinationLatitude,
    decimal DestinationLongitude);

public sealed record RouteGeometryPoint(decimal Latitude, decimal Longitude);

public sealed record RouteStepDto(
    string Instruction,
    decimal DistanceKilometers,
    int DurationSeconds);

/// <summary>
/// A road route returned by the configured routing provider. Duration is an
/// estimate based on the road network; it is not a real-time traffic promise.
/// </summary>
public sealed record RoadRouteDto(
    decimal DistanceKilometers,
    int EstimatedDurationMinutes,
    IReadOnlyCollection<RouteGeometryPoint> Geometry,
    IReadOnlyCollection<RouteStepDto> Steps,
    string Provider);

public interface IRoadRoutingService
{
    Task<ApplicationResult<RoadRouteDto>> EstimateAsync(
        RouteEstimateRequest request,
        CancellationToken cancellationToken = default);
}
