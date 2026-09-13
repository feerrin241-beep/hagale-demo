namespace Hagale.Domain.Common;

/// <summary>
/// Validates geographic coordinates and calculates an approximate straight-line distance.
/// Route distance and ETA deliberately belong to a maps provider, not to this domain helper.
/// </summary>
public static class GeoCoordinates
{
    private const double EarthRadiusKilometers = 6_371.0088;

    public static void EnsureOptionalPair(decimal? latitude, decimal? longitude, string label)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new DomainRuleViolationException($"La ubicación de {label} debe incluir latitud y longitud.");
        }

        if (!latitude.HasValue)
        {
            return;
        }

        EnsureValid(latitude.Value, longitude!.Value, label);
    }

    public static void EnsureValid(decimal latitude, decimal longitude, string label)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainRuleViolationException($"La ubicación de {label} no es válida.");
        }
    }

    public static decimal CalculateDirectDistanceKilometers(
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude)
    {
        var latitudeDelta = ToRadians((double)(destinationLatitude - originLatitude));
        var longitudeDelta = ToRadians((double)(destinationLongitude - originLongitude));
        var originLatitudeRadians = ToRadians((double)originLatitude);
        var destinationLatitudeRadians = ToRadians((double)destinationLatitude);

        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                        Math.Cos(originLatitudeRadians) * Math.Cos(destinationLatitudeRadians) *
                        Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        var centralAngle = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));

        return Math.Round((decimal)(EarthRadiusKilometers * centralAngle), 1, MidpointRounding.AwayFromZero);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
