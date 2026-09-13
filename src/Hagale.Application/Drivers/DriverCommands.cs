using Hagale.Domain.Drivers;

namespace Hagale.Application.Drivers;

public sealed record CreateDriverApplicationCommand;

public sealed record RegisterVehicleCommand(
    string Brand,
    string Model,
    int Year,
    string Color,
    string Plate,
    VehicleType Type,
    string OperatingCityCode);

public sealed record UpdateVehicleCommand(
    string Brand,
    string Model,
    int Year,
    string Color,
    string Plate,
    VehicleType Type,
    string OperatingCityCode);

public sealed record RegisterDriverDocumentCommand(
    DriverDocumentType Type,
    string StorageObjectKey,
    DateOnly? ExpiresOn);

public sealed record ReviewDecision(bool Approve, string? Notes);

public sealed record ChangeAvailabilityCommand(DriverAvailabilityStatus AvailabilityStatus);

public sealed record UpdateDriverLocationCommand(decimal Latitude, decimal Longitude);
