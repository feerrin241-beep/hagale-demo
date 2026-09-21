using System.Globalization;
using System.Text.Json;
using Hagale.Application.Common;
using Hagale.Application.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hagale.Infrastructure.Routing;

/// <summary>
/// Server-side adapter for an OSRM-compatible route server. Keeping this call
/// on the API means a later provider key never has to be shipped to phones.
/// </summary>
public sealed class OsrmRoadRoutingService(
    HttpClient httpClient,
    IOptions<RoadRoutingOptions> options,
    IMemoryCache cache,
    ILogger<OsrmRoadRoutingService> logger) : IRoadRoutingService
{
    private const string ProviderName = "OpenStreetMap / OSRM";

    public async Task<ApplicationResult<RoadRouteDto>> EstimateAsync(
        RouteEstimateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidCoordinate(request.OriginLatitude, request.OriginLongitude) ||
            !IsValidCoordinate(request.DestinationLatitude, request.DestinationLongitude))
        {
            return ApplicationResult<RoadRouteDto>.Failure("Las coordenadas de origen y destino no son válidas.");
        }

        var configured = options.Value;
        if (!Uri.TryCreate(configured.OsrmBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("https" or "http"))
        {
            return ApplicationResult<RoadRouteDto>.Failure("El proveedor de rutas reales aún no está configurado.");
        }

        var cacheKey = BuildCacheKey(request);
        if (cache.TryGetValue(cacheKey, out RoadRouteDto? cached) && cached is not null)
        {
            return ApplicationResult<RoadRouteDto>.Success(cached);
        }

        var origin = FormatCoordinate(request.OriginLongitude, request.OriginLatitude);
        var destination = FormatCoordinate(request.DestinationLongitude, request.DestinationLatitude);
        var path = $"route/v1/driving/{origin};{destination}?overview=full&geometries=geojson&steps=true";
        var requestUri = new Uri(baseUri, path);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(configured.TimeoutSeconds, 3, 30)));
            using var outboundRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
            outboundRequest.Headers.UserAgent.ParseAdd("HAGALE/0.1 (+https://hagale-demo.onrender.com)");
            outboundRequest.Headers.Accept.ParseAdd("application/json");
            using var response = await httpClient.SendAsync(outboundRequest, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Routing provider returned HTTP {StatusCode}.", (int)response.StatusCode);
                return ApplicationResult<RoadRouteDto>.Failure("No fue posible calcular la ruta por calles ahora. Intenta de nuevo en unos segundos.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryMapRoute(document.RootElement, out var route))
            {
                return ApplicationResult<RoadRouteDto>.Failure("No se encontró una ruta transitable entre los dos puntos seleccionados.");
            }

            var duration = TimeSpan.FromMinutes(Math.Clamp(configured.CacheMinutes, 1, 30));
            cache.Set(cacheKey, route, duration);
            return ApplicationResult<RoadRouteDto>.Success(route);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("The routing provider timed out.");
            return ApplicationResult<RoadRouteDto>.Failure("La ruta real tardó demasiado. Revisa la conexión e intenta de nuevo.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The routing provider could not be reached.");
            return ApplicationResult<RoadRouteDto>.Failure("No se pudo conectar con el servicio de rutas reales.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The routing provider returned an invalid response.");
            return ApplicationResult<RoadRouteDto>.Failure("El servicio de rutas devolvió una respuesta no válida.");
        }
    }

    private static bool TryMapRoute(JsonElement root, out RoadRouteDto route)
    {
        route = null!;
        if (!root.TryGetProperty("code", out var code) ||
            !string.Equals(code.GetString(), "Ok", StringComparison.OrdinalIgnoreCase) ||
            !root.TryGetProperty("routes", out var routes) ||
            routes.ValueKind != JsonValueKind.Array || routes.GetArrayLength() == 0)
        {
            return false;
        }

        var primary = routes[0];
        if (!TryGetNumber(primary, "distance", out var distanceMeters) ||
            !TryGetNumber(primary, "duration", out var durationSeconds) ||
            !primary.TryGetProperty("geometry", out var geometry) ||
            !geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var points = new List<RouteGeometryPoint>();
        foreach (var coordinate in coordinates.EnumerateArray())
        {
            if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2 ||
                !TryGetArrayNumber(coordinate, 0, out var longitude) ||
                !TryGetArrayNumber(coordinate, 1, out var latitude) ||
                !IsValidCoordinate(latitude, longitude))
            {
                continue;
            }

            points.Add(new RouteGeometryPoint(
                decimal.Round(latitude, 6, MidpointRounding.AwayFromZero),
                decimal.Round(longitude, 6, MidpointRounding.AwayFromZero)));
        }

        if (points.Count < 2)
        {
            return false;
        }

        route = new RoadRouteDto(
            decimal.Round(distanceMeters / 1000m, 2, MidpointRounding.AwayFromZero),
            Math.Max(1, (int)Math.Ceiling(durationSeconds / 60m)),
            points,
            MapSteps(primary),
            ProviderName);
        return true;
    }

    private static IReadOnlyCollection<RouteStepDto> MapSteps(JsonElement route)
    {
        if (!route.TryGetProperty("legs", out var legs) || legs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<RouteStepDto>();
        foreach (var leg in legs.EnumerateArray())
        {
            if (!leg.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var step in steps.EnumerateArray())
            {
                if (!TryGetNumber(step, "distance", out var distanceMeters) ||
                    !TryGetNumber(step, "duration", out var durationSeconds))
                {
                    continue;
                }

                result.Add(new RouteStepDto(
                    DescribeStep(step),
                    decimal.Round(distanceMeters / 1000m, 2, MidpointRounding.AwayFromZero),
                    Math.Max(0, (int)Math.Round(durationSeconds, MidpointRounding.AwayFromZero))));
            }
        }

        return result;
    }

    private static string DescribeStep(JsonElement step)
    {
        var streetName = step.TryGetProperty("name", out var name) ? name.GetString()?.Trim() : null;
        var type = step.TryGetProperty("maneuver", out var maneuver) && maneuver.TryGetProperty("type", out var typeElement)
            ? typeElement.GetString()?.Trim().ToLowerInvariant()
            : null;
        var modifier = step.TryGetProperty("maneuver", out maneuver) && maneuver.TryGetProperty("modifier", out var modifierElement)
            ? modifierElement.GetString()?.Trim().ToLowerInvariant()
            : null;
        var suffix = string.IsNullOrWhiteSpace(streetName) ? string.Empty : $" por {streetName}";

        return type switch
        {
            "depart" => $"Comienza{suffix}",
            "arrive" => "Llegaste al destino",
            "roundabout" or "rotary" => $"Toma la glorieta{suffix}",
            "merge" => $"Incorpórate{suffix}",
            "turn" when modifier?.Contains("left", StringComparison.Ordinal) == true => $"Gira a la izquierda{suffix}",
            "turn" when modifier?.Contains("right", StringComparison.Ordinal) == true => $"Gira a la derecha{suffix}",
            "turn" when modifier?.Contains("uturn", StringComparison.Ordinal) == true => "Haz retorno",
            "turn" => $"Continúa{suffix}",
            "new name" => $"Continúa{suffix}",
            _ => string.IsNullOrWhiteSpace(streetName) ? "Continúa por la vía" : $"Continúa por {streetName}"
        };
    }

    private static bool TryGetNumber(JsonElement element, string propertyName, out decimal value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) && property.TryGetDecimal(out value);
    }

    private static bool TryGetArrayNumber(JsonElement array, int index, out decimal value)
    {
        if (index < 0 || index >= array.GetArrayLength())
        {
            value = 0;
            return false;
        }

        value = 0;
        return array[index].TryGetDecimal(out value);
    }

    private static bool IsValidCoordinate(decimal latitude, decimal longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static string FormatCoordinate(decimal longitude, decimal latitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{longitude:F6},{latitude:F6}");

    private static string BuildCacheKey(RouteEstimateRequest request) => string.Create(
        CultureInfo.InvariantCulture,
        $"road-route:{request.OriginLatitude:F5},{request.OriginLongitude:F5}:{request.DestinationLatitude:F5},{request.DestinationLongitude:F5}");
}
