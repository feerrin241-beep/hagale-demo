using Hagale.Domain.Common;
using System.Text.RegularExpressions;

namespace Hagale.Domain.Safety;

public sealed class EmergencyContact
{
    private static readonly Regex E164PhoneNumber = new(
        @"^\+[1-9]\d{7,14}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private EmergencyContact()
    {
    }

    public EmergencyContact(
        Guid userId,
        string name,
        string phoneNumber,
        string? relationship,
        DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleViolationException("El contacto debe asociarse a una cuenta válida.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Name = NormalizeRequired(name, "nombre", 100);
        PhoneNumber = NormalizePhoneNumber(phoneNumber);
        Relationship = NormalizeOptional(relationship, "relación", 100);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string? Relationship { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public bool IsActive => ArchivedAtUtc is null;

    public void Update(string name, string phoneNumber, string? relationship)
    {
        if (!IsActive)
        {
            throw new DomainRuleViolationException("Un contacto desactivado no puede modificarse.");
        }

        Name = NormalizeRequired(name, "nombre", 100);
        PhoneNumber = NormalizePhoneNumber(phoneNumber);
        Relationship = NormalizeOptional(relationship, "relación", 100);
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        if (!IsActive)
        {
            throw new DomainRuleViolationException("El contacto ya está desactivado.");
        }

        ArchivedAtUtc = archivedAtUtc;
    }

    private static string NormalizeRequired(string value, string label, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 2 || normalized.Length > maximumLength)
        {
            throw new DomainRuleViolationException($"El {label} debe tener entre 2 y {maximumLength} caracteres.");
        }

        return normalized;
    }

    private static string NormalizePhoneNumber(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || !E164PhoneNumber.IsMatch(normalized))
        {
            throw new DomainRuleViolationException("El teléfono debe usar formato internacional E.164.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, string label, int maximumLength)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new DomainRuleViolationException($"La {label} no puede exceder {maximumLength} caracteres.");
        }

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
