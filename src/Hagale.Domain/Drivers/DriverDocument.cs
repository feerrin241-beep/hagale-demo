using Hagale.Domain.Common;

namespace Hagale.Domain.Drivers;

public sealed class DriverDocument
{
    private DriverDocument()
    {
    }

    public DriverDocument(DriverDocumentType type, string storageObjectKey, DateOnly? expiresOn)
    {
        Type = type;
        StorageObjectKey = RequireStorageObjectKey(storageObjectKey);
        ExpiresOn = expiresOn;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DriverProfileId { get; private set; }
    public DriverDocumentType Type { get; private set; }
    public string StorageObjectKey { get; private set; } = null!;
    public DateOnly? ExpiresOn { get; private set; }
    public DocumentReviewStatus ReviewStatus { get; private set; } = DocumentReviewStatus.Pending;
    public string? ReviewNotes { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }

    internal void AssignToDriver(Guid driverProfileId)
    {
        if (driverProfileId == Guid.Empty)
        {
            throw new DomainRuleViolationException("El documento debe estar asociado a un conductor válido.");
        }

        DriverProfileId = driverProfileId;
    }

    public void Review(bool approved, string? notes, Guid reviewedByUserId, DateTimeOffset reviewedAtUtc)
    {
        if (ReviewStatus != DocumentReviewStatus.Pending)
        {
            throw new DomainRuleViolationException("Un documento ya revisado no puede revisarse otra vez.");
        }

        if (reviewedByUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException("La revisión debe identificar al administrador responsable.");
        }

        if (!approved && string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainRuleViolationException("Un documento rechazado debe incluir una razón.");
        }

        ReviewStatus = approved ? DocumentReviewStatus.Approved : DocumentReviewStatus.Rejected;
        ReviewNotes = NormalizeNotes(notes);
        ReviewedByUserId = reviewedByUserId;
        ReviewedAtUtc = reviewedAtUtc;
    }

    private static string RequireStorageObjectKey(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 500 || normalized.Contains("://", StringComparison.Ordinal))
        {
            throw new DomainRuleViolationException("La referencia interna del documento no es válida.");
        }

        return normalized;
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
