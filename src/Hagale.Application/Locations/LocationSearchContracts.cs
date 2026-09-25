namespace Hagale.Application.Locations;

public sealed record LocationSearchResult(
    decimal Latitude,
    decimal Longitude,
    string DisplayName);
