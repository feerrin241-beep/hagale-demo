using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Hagale.Application.Authentication;
using Hagale.Application.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Hagale.Api.Controllers;

[ApiController]
[Authorize(Roles = HagaleRoles.Customer + "," + HagaleRoles.Driver + "," + HagaleRoles.Administrator)]
[Route("api/v1/locations")]
public sealed class LocationSearchController(HttpClient httpClient, IMemoryCache cache) : ControllerBase
{
    private const string PhotonEndpoint = "https://photon.komoot.io/api/";
    private const string NominatimEndpoint = "https://nominatim.openstreetmap.org/search";

    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyCollection<LocationSearchResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<LocationSearchResult>>> Search(
        [FromQuery, Required, StringLength(180, MinimumLength = 4)] string q,
        [FromQuery, StringLength(20)] string? cityCode,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = q.Trim();
        if (normalizedQuery.Length < 4)
        {
            return BadRequest(new ProblemDetails { Detail = "Escribe una dirección más completa." });
        }

        var cityHint = cityCode?.Trim().ToUpperInvariant() switch
        {
            "BUC" or "BUCARAMANGA" => "Bucaramanga, Santander, Colombia",
            "BOG" or "BOGOTA" => "Bogotá, Colombia",
            "MDE" or "MEDELLIN" => "Medellín, Colombia",
            "CAL" or "CALI" => "Cali, Colombia",
            _ => "Colombia"
        };
        var cacheKey = $"location-search:{normalizedQuery.ToUpperInvariant()}:{cityHint}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyCollection<LocationSearchResult>? cached) && cached is not null)
        {
            return Ok(cached);
        }

        var searchText = normalizedQuery + ", " + cityHint;
        var providers = new[]
        {
            (Uri: $"{PhotonEndpoint}?lang=es&limit=3&q={Uri.EscapeDataString(searchText)}", Parser: "photon"),
            (Uri: $"{NominatimEndpoint}?format=jsonv2&limit=3&countrycodes=co&addressdetails=0&q={Uri.EscapeDataString(searchText)}", Parser: "nominatim")
        };

        foreach (var provider in providers)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            using var outboundRequest = new HttpRequestMessage(HttpMethod.Get, provider.Uri);
            outboundRequest.Headers.UserAgent.ParseAdd("HAGALE/0.1 (+https://hagale-demo.onrender.com)");
            outboundRequest.Headers.Accept.ParseAdd("application/json");

            try
            {
                using var response = await httpClient.SendAsync(outboundRequest, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeout.Token);
                var results = provider.Parser == "photon"
                    ? ParsePhotonResults(document.RootElement, normalizedQuery)
                    : ParseNominatimResults(document.RootElement, normalizedQuery);
                if (results.Length == 0)
                {
                    continue;
                }

                cache.Set(cacheKey, results, TimeSpan.FromMinutes(10));
                return Ok(results);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Try the next provider before returning a user-facing error.
            }
            catch (HttpRequestException)
            {
                // Try the next provider before returning a user-facing error.
            }
            catch (JsonException)
            {
                // Try the next provider before returning a user-facing error.
            }
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Detail = "No se pudo ubicar la dirección ahora. Puedes elegir el punto en el mapa." });
    }

    private static LocationSearchResult[] ParsePhotonResults(JsonElement root, string fallbackName)
    {
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return features.EnumerateArray()
            .Select(feature =>
            {
                var coordinates = feature.TryGetProperty("geometry", out var geometry)
                    && geometry.TryGetProperty("coordinates", out var values)
                    && values.ValueKind == JsonValueKind.Array
                    ? values
                    : default;
                var longitude = coordinates.ValueKind == JsonValueKind.Array && coordinates.GetArrayLength() > 0
                    ? coordinates[0].GetDecimal()
                    : 0m;
                var latitude = coordinates.ValueKind == JsonValueKind.Array && coordinates.GetArrayLength() > 1
                    ? coordinates[1].GetDecimal()
                    : 0m;
                var properties = feature.TryGetProperty("properties", out var propertyElement) ? propertyElement : default;
                var displayName = BuildPhotonDisplayName(properties, fallbackName);
                return new LocationSearchResult(latitude, longitude, displayName);
            })
            .Where(result => IsValidCoordinate(result.Latitude, result.Longitude))
            .ToArray();
    }

    private static LocationSearchResult[] ParseNominatimResults(JsonElement root, string fallbackName)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return root.EnumerateArray()
            .Select(place =>
            {
                var latitude = place.TryGetProperty("lat", out var lat) ? lat.GetString() : null;
                var longitude = place.TryGetProperty("lon", out var lon) ? lon.GetString() : null;
                var displayName = place.TryGetProperty("display_name", out var display) ? display.GetString() : null;
                return decimal.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLatitude)
                    && decimal.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLongitude)
                    ? new LocationSearchResult(parsedLatitude, parsedLongitude, displayName?.Trim() ?? fallbackName)
                    : null;
            })
            .Where(result => result is not null)
            .Cast<LocationSearchResult>()
            .Where(result => IsValidCoordinate(result.Latitude, result.Longitude))
            .ToArray();
    }

    private static string BuildPhotonDisplayName(JsonElement properties, string fallbackName)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return fallbackName;
        }

        var parts = new[] { "name", "street", "housenumber", "district", "city", "state" }
            .Select(key => properties.TryGetProperty(key, out var value) ? value.GetString()?.Trim() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length > 0 ? string.Join(", ", parts) : fallbackName;
    }

    private static bool IsValidCoordinate(decimal latitude, decimal longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
}
