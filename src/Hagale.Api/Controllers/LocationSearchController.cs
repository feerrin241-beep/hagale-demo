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

        var uri = $"{NominatimEndpoint}?format=jsonv2&limit=3&countrycodes=co&addressdetails=0&q={Uri.EscapeDataString(normalizedQuery + ", " + cityHint)}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var outboundRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        outboundRequest.Headers.UserAgent.ParseAdd("HAGALE/0.1 (+https://hagale-demo.onrender.com)");
        outboundRequest.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await httpClient.SendAsync(outboundRequest, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Detail = "El buscador de direcciones no está disponible. Puedes elegir el punto en el mapa." });
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeout.Token);
            var results = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray()
                    .Select(place =>
                    {
                        var lat = place.TryGetProperty("lat", out var latElement) ? latElement.GetString() : null;
                        var lon = place.TryGetProperty("lon", out var lonElement) ? lonElement.GetString() : null;
                        var displayName = place.TryGetProperty("display_name", out var displayElement) ? displayElement.GetString() : null;
                        return (lat, lon, displayName);
                    })
                    .Where(place => decimal.TryParse(place.lat, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                        && decimal.TryParse(place.lon, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    .Select(place => new LocationSearchResult(
                        decimal.Parse(place.lat!, CultureInfo.InvariantCulture),
                        decimal.Parse(place.lon!, CultureInfo.InvariantCulture),
                        place.displayName?.Trim() ?? normalizedQuery))
                    .ToArray()
                : Array.Empty<LocationSearchResult>();
            cache.Set(cacheKey, results, TimeSpan.FromMinutes(10));
            return Ok(results);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Detail = "La búsqueda tardó demasiado. Puedes elegir el punto en el mapa." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Detail = "No se pudo conectar al buscador. Puedes elegir el punto en el mapa." });
        }
    }

}

