using Hagale.Domain.Drivers;

namespace Hagale.Application.Drivers;

public sealed record DriverProfileDto(
    Guid Id,
    Guid UserId,
    DriverStatus Status,
    DriverAvailabilityStatus AvailabilityStatus,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    string? AdministrativeNotes,
    decimal? LastKnownLatitude,
    decimal? LastKnownLongitude,
    DateTimeOffset? LocationUpdatedAtUtc,
    IReadOnlyCollection<VehicleDto> Vehicles,
    IReadOnlyCollection<DriverDocumentDto> Documents);

public sealed record VehicleDto(
    Guid Id,
    string Brand,
    string Model,
    int Year,
    string Color,
    string Plate,
    VehicleType Type,
    string OperatingCityCode,
    bool IsActive);

public sealed record DriverDocumentDto(
    Guid Id,
    DriverDocumentType Type,
    DateOnly? ExpiresOn,
    DocumentReviewStatus ReviewStatus,
    string? ReviewNotes,
    DateTimeOffset? ReviewedAtUtc);

public sealed record DriverDocumentFileDto(
    Guid Id,
    DriverDocumentType Type,
    string StorageObjectKey,
    string DownloadFileName);

public sealed record DriverApplicationPageDto(
    IReadOnlyCollection<DriverProfileDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
