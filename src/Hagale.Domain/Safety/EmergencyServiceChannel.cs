using Hagale.Domain.Common;
using System.Text.RegularExpressions;

namespace Hagale.Domain.Safety;

public sealed class EmergencyServiceChannel
{
    private static readonly Regex ContactNumberPattern = new(
        @"^\+?[0-9][0-9 -]{1,18}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private EmergencyServiceChannel()
    {
    }

    public EmergencyServiceChannel(
        string cityCode,
        EmergencyChannelType channelType,
        string displayName,
        string contactNumber,
        bool isActive,
        DateTimeOffset updatedAtUtc)
    {
        Id = Guid.NewGuid();
        CityCode = NormalizeCityCode(cityCode);
        ChannelType = channelType;
        DisplayName = NormalizeDisplayName(displayName);
        ContactNumber = NormalizeContactNumber(contactNumber);
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string CityCode { get; private set; } = null!;
    public EmergencyChannelType ChannelType { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string ContactNumber { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    public void Update(string displayName, string contactNumber, bool isActive, DateTimeOffset updatedAtUtc)
    {
        DisplayName = NormalizeDisplayName(displayName);
        ContactNumber = NormalizeContactNumber(contactNumber);
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string NormalizeCityCode(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20 || normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new DomainRuleViolationException("El código de ciudad no es válido.");
        }

        return normalized;
    }

    private static string NormalizeDisplayName(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length is < 2 or > 100)
        {
            throw new DomainRuleViolationException("El nombre del canal debe tener entre 2 y 100 caracteres.");
        }

        return normalized;
    }

    private static string NormalizeContactNumber(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || !ContactNumberPattern.IsMatch(normalized))
        {
            throw new DomainRuleViolationException("El número de contacto no es válido.");
        }

        return normalized;
    }
}
