using Hagale.Domain.Common;

namespace Hagale.Domain.Drivers;

public sealed class Vehicle
{
    private Vehicle()
    {
    }

    public Vehicle(
        string brand,
        string model,
        int year,
        string color,
        string plate,
        VehicleType type,
        string operatingCityCode)
    {
        Brand = RequireText(brand, nameof(brand), 100);
        Model = RequireText(model, nameof(model), 100);
        Year = RequireYear(year);
        Color = RequireText(color, nameof(color), 50);
        Plate = NormalizePlate(plate);
        Type = type;
        OperatingCityCode = RequireText(operatingCityCode, nameof(operatingCityCode), 20).ToUpperInvariant();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DriverProfileId { get; private set; }
    public string Brand { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public int Year { get; private set; }
    public string Color { get; private set; } = null!;
    public string Plate { get; private set; } = null!;
    public VehicleType Type { get; private set; }
    public string OperatingCityCode { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    internal void AssignToDriver(Guid driverProfileId)
    {
        if (driverProfileId == Guid.Empty)
        {
            throw new DomainRuleViolationException("El vehículo debe estar asociado a un conductor válido.");
        }

        if (DriverProfileId != Guid.Empty && DriverProfileId != driverProfileId)
        {
            throw new DomainRuleViolationException("Un vehículo no puede reasignarse sin un proceso administrativo.");
        }

        DriverProfileId = driverProfileId;
    }

    public void Deactivate() => IsActive = false;

    public void UpdateDetails(
        string brand,
        string model,
        int year,
        string color,
        string plate,
        VehicleType type,
        string operatingCityCode)
    {
        Brand = RequireText(brand, nameof(brand), 100);
        Model = RequireText(model, nameof(model), 100);
        Year = RequireYear(year);
        Color = RequireText(color, nameof(color), 50);
        Plate = NormalizePlate(plate);
        Type = type;
        OperatingCityCode = RequireText(operatingCityCode, nameof(operatingCityCode), 20).ToUpperInvariant();
    }

    private static string NormalizePlate(string value) => RequireText(value, nameof(value), 10)
        .Replace(" ", string.Empty, StringComparison.Ordinal)
        .ToUpperInvariant();

    private static string RequireText(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new DomainRuleViolationException($"{parameterName} es obligatorio y no puede exceder {maximumLength} caracteres.");
        }

        return normalized;
    }

    private static int RequireYear(int year)
    {
        var maximumYear = DateTime.UtcNow.Year + 1;
        if (year < 1900 || year > maximumYear)
        {
            throw new DomainRuleViolationException("El año del vehículo no es válido.");
        }

        return year;
    }
}
