using Hagale.Domain.Common;

namespace Hagale.Domain.Drivers;

public sealed class DriverProfile
{
    private readonly List<Vehicle> _vehicles = [];
    private readonly List<DriverDocument> _documents = [];

    private DriverProfile()
    {
    }

    public DriverProfile(Guid userId, DateTimeOffset appliedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleViolationException("El perfil de conductor debe estar asociado a un usuario válido.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        AppliedAtUtc = appliedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DriverStatus Status { get; private set; } = DriverStatus.Pending;
    public DriverAvailabilityStatus AvailabilityStatus { get; private set; } = DriverAvailabilityStatus.Offline;
    public DateTimeOffset AppliedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? StatusChangedAtUtc { get; private set; }
    public string? AdministrativeNotes { get; private set; }
    public decimal? LastKnownLatitude { get; private set; }
    public decimal? LastKnownLongitude { get; private set; }
    public DateTimeOffset? LocationUpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public IReadOnlyCollection<Vehicle> Vehicles => _vehicles.AsReadOnly();
    public IReadOnlyCollection<DriverDocument> Documents => _documents.AsReadOnly();

    public void StartReview(DateTimeOffset changedAtUtc)
    {
        if (Status != DriverStatus.Pending)
        {
            throw new DomainRuleViolationException("Solo una solicitud pendiente puede pasar a revisión.");
        }

        ChangeStatus(DriverStatus.UnderReview, changedAtUtc, null);
    }

    public void Approve(string? notes, DateTimeOffset changedAtUtc)
    {
        if (Status is not (DriverStatus.Pending or DriverStatus.UnderReview))
        {
            throw new DomainRuleViolationException("Solo una solicitud pendiente o en revisión puede aprobarse.");
        }

        if (!HasMinimumApprovalEvidence())
        {
            throw new DomainRuleViolationException("No se puede aprobar: requiere vehículo activo y documentos aprobados.");
        }

        Status = DriverStatus.Approved;
        AvailabilityStatus = DriverAvailabilityStatus.Offline;
        ApprovedAtUtc = changedAtUtc;
        StatusChangedAtUtc = changedAtUtc;
        AdministrativeNotes = NormalizeNotes(notes);
    }

    public void Reject(string notes, DateTimeOffset changedAtUtc)
    {
        if (Status is not (DriverStatus.Pending or DriverStatus.UnderReview))
        {
            throw new DomainRuleViolationException("Solo una solicitud pendiente o en revisión puede rechazarse.");
        }

        ChangeStatus(DriverStatus.Rejected, changedAtUtc, RequireNotes(notes));
    }

    public void Suspend(string notes, DateTimeOffset changedAtUtc)
    {
        if (Status != DriverStatus.Approved)
        {
            throw new DomainRuleViolationException("Solo un conductor aprobado puede suspenderse.");
        }

        ChangeStatus(DriverStatus.Suspended, changedAtUtc, RequireNotes(notes));
        AvailabilityStatus = DriverAvailabilityStatus.Offline;
    }

    public void Reactivate(string? notes, DateTimeOffset changedAtUtc)
    {
        if (Status is not (DriverStatus.Suspended or DriverStatus.Inactive))
        {
            throw new DomainRuleViolationException("Solo un conductor suspendido o inactivo puede reactivarse.");
        }

        ChangeStatus(DriverStatus.Approved, changedAtUtc, notes);
        AvailabilityStatus = DriverAvailabilityStatus.Offline;
    }

    public void Deactivate(string notes, DateTimeOffset changedAtUtc)
    {
        if (Status == DriverStatus.Inactive)
        {
            throw new DomainRuleViolationException("El conductor ya está inactivo.");
        }

        ChangeStatus(DriverStatus.Inactive, changedAtUtc, RequireNotes(notes));
        AvailabilityStatus = DriverAvailabilityStatus.Offline;
    }

    public void SetAvailability(DriverAvailabilityStatus availabilityStatus)
    {
        if (Status != DriverStatus.Approved)
        {
            throw new DomainRuleViolationException("Solo un conductor aprobado puede cambiar su disponibilidad.");
        }

        if (availabilityStatus == DriverAvailabilityStatus.Available && !_vehicles.Any(vehicle => vehicle.IsActive))
        {
            throw new DomainRuleViolationException("Debes tener una motocicleta activa para estar disponible.");
        }

        AvailabilityStatus = availabilityStatus;
        if (availabilityStatus == DriverAvailabilityStatus.Offline)
        {
            ClearCurrentLocation();
        }
    }

    /// <summary>
    /// Stores only the driver's latest location while the driver is actively serving.
    /// Historical location tracking is intentionally not performed here.
    /// </summary>
    public void UpdateCurrentLocation(decimal latitude, decimal longitude, DateTimeOffset updatedAtUtc)
    {
        if (Status != DriverStatus.Approved || AvailabilityStatus is DriverAvailabilityStatus.Offline)
        {
            throw new DomainRuleViolationException("Activa tu disponibilidad antes de actualizar la ubicación de despacho.");
        }

        GeoCoordinates.EnsureValid(latitude, longitude, "despacho");
        LastKnownLatitude = latitude;
        LastKnownLongitude = longitude;
        LocationUpdatedAtUtc = updatedAtUtc;
    }

    public void AddVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        if (_vehicles.Any(existing => existing.Plate.Equals(vehicle.Plate, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainRuleViolationException("No se puede registrar dos veces la misma placa para el conductor.");
        }

        vehicle.AssignToDriver(Id);
        _vehicles.Add(vehicle);
    }

    public void AddDocument(DriverDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.AssignToDriver(Id);
        _documents.Add(document);
    }

    public bool HasMinimumApprovalEvidence() =>
        _vehicles.Any(vehicle => vehicle.IsActive) &&
        HasApprovedRequiredDocuments();

    public IReadOnlyCollection<DriverDocumentType> GetMissingRequiredDocumentTypes()
    {
        var approvedTypes = _documents
            .Where(document => document.ReviewStatus == DocumentReviewStatus.Approved)
            .Select(document => document.Type)
            .ToHashSet();

        return GetRequiredDocumentTypes()
            .Where(type => !approvedTypes.Contains(type))
            .ToArray();
    }

    private void ChangeStatus(DriverStatus status, DateTimeOffset changedAtUtc, string? notes)
    {
        Status = status;
        StatusChangedAtUtc = changedAtUtc;
        AdministrativeNotes = NormalizeNotes(notes);
    }

    private void ClearCurrentLocation()
    {
        LastKnownLatitude = null;
        LastKnownLongitude = null;
        LocationUpdatedAtUtc = null;
    }

    private bool HasApprovedRequiredDocuments() => GetMissingRequiredDocumentTypes().Count == 0;

    private IReadOnlyCollection<DriverDocumentType> GetRequiredDocumentTypes()
    {
        var required = new List<DriverDocumentType>
        {
            DriverDocumentType.PersonalIdentification,
            DriverDocumentType.DriverLicense,
            DriverDocumentType.VehicleRegistration,
            DriverDocumentType.Insurance
        };

        if (_vehicles.Any(vehicle => vehicle.IsActive && vehicle.Type == VehicleType.Motorcycle && RequiresRoadworthiness(vehicle.Year)))
        {
            required.Add(DriverDocumentType.Roadworthiness);
        }

        return required;
    }

    private static bool RequiresRoadworthiness(int vehicleYear)
    {
        var currentYear = DateTime.UtcNow.Year;
        return currentYear - vehicleYear >= 2;
    }

    private static string RequireNotes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleViolationException("Debe registrar una razón administrativa.");
        }

        return NormalizeNotes(value)!;
    }

    private static string? NormalizeNotes(string? value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 1_000)
        {
            throw new DomainRuleViolationException("Las observaciones no pueden exceder 1000 caracteres.");
        }

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
