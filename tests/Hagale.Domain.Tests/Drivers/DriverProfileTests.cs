using Hagale.Domain.Common;
using Hagale.Domain.Drivers;

namespace Hagale.Domain.Tests.Drivers;

public sealed class DriverProfileTests
{
    [Fact]
    public void Approved_driver_must_start_offline()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);
        PrepareForApproval(driver);

        driver.Approve("Documentación verificada.", DateTimeOffset.UtcNow);

        Assert.Equal(DriverStatus.Approved, driver.Status);
        Assert.Equal(DriverAvailabilityStatus.Offline, driver.AvailabilityStatus);
        Assert.NotNull(driver.ApprovedAtUtc);
    }

    [Fact]
    public void Suspension_requires_an_administrative_reason()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);
        PrepareForApproval(driver);
        driver.Approve(null, DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleViolationException>(() => driver.Suspend("", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Approval_evidence_requires_an_active_vehicle_and_approved_documents()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var vehicle = new Vehicle("Yamaha", "FZ", 2024, "Negra", "ABC12D", VehicleType.Motorcycle, "BUC");

        driver.AddVehicle(vehicle);
        AddApprovedDocument(driver, DriverDocumentType.PersonalIdentification);
        AddApprovedDocument(driver, DriverDocumentType.DriverLicense);
        AddApprovedDocument(driver, DriverDocumentType.VehicleRegistration);
        AddApprovedDocument(driver, DriverDocumentType.Insurance);
        AddApprovedDocument(driver, DriverDocumentType.Roadworthiness);

        Assert.True(driver.HasMinimumApprovalEvidence());
    }

    [Fact]
    public void Approval_evidence_lists_missing_required_documents()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);
        driver.AddVehicle(new Vehicle("Yamaha", "FZ", 2026, "Negra", "ABC12D", VehicleType.Motorcycle, "BUC"));
        AddApprovedDocument(driver, DriverDocumentType.DriverLicense);

        var missingDocuments = driver.GetMissingRequiredDocumentTypes();

        Assert.Contains(DriverDocumentType.PersonalIdentification, missingDocuments);
        Assert.Contains(DriverDocumentType.VehicleRegistration, missingDocuments);
        Assert.Contains(DriverDocumentType.Insurance, missingDocuments);
        Assert.DoesNotContain(DriverDocumentType.DriverLicense, missingDocuments);
        Assert.DoesNotContain(DriverDocumentType.Roadworthiness, missingDocuments);
        Assert.False(driver.HasMinimumApprovalEvidence());
    }

    [Fact]
    public void Older_motorcycle_requires_roadworthiness_before_approval()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);
        driver.AddVehicle(new Vehicle("Yamaha", "FZ", 2020, "Negra", "ABC12D", VehicleType.Motorcycle, "BUC"));
        AddApprovedDocument(driver, DriverDocumentType.PersonalIdentification);
        AddApprovedDocument(driver, DriverDocumentType.DriverLicense);
        AddApprovedDocument(driver, DriverDocumentType.VehicleRegistration);
        AddApprovedDocument(driver, DriverDocumentType.Insurance);

        var missingDocuments = driver.GetMissingRequiredDocumentTypes();

        Assert.Contains(DriverDocumentType.Roadworthiness, missingDocuments);
        Assert.False(driver.HasMinimumApprovalEvidence());
    }

    [Fact]
    public void Driver_cannot_be_approved_without_required_evidence()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleViolationException>(() => driver.Approve(null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Approved_driver_with_active_vehicle_can_become_available()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);
        PrepareForApproval(driver);
        driver.Approve(null, DateTimeOffset.UtcNow);

        driver.SetAvailability(DriverAvailabilityStatus.Available);

        Assert.Equal(DriverAvailabilityStatus.Available, driver.AvailabilityStatus);
    }

    [Fact]
    public void Available_driver_can_update_dispatch_location_and_disconnect_clears_it()
    {
        var driver = new DriverProfile(Guid.NewGuid(), DateTimeOffset.UtcNow);
        PrepareForApproval(driver);
        driver.Approve(null, DateTimeOffset.UtcNow);
        driver.SetAvailability(DriverAvailabilityStatus.Available);

        driver.UpdateCurrentLocation(7.1193m, -73.1227m, DateTimeOffset.UtcNow);

        Assert.Equal(7.1193m, driver.LastKnownLatitude);
        Assert.Equal(-73.1227m, driver.LastKnownLongitude);
        Assert.NotNull(driver.LocationUpdatedAtUtc);

        driver.SetAvailability(DriverAvailabilityStatus.Offline);

        Assert.Null(driver.LastKnownLatitude);
        Assert.Null(driver.LastKnownLongitude);
        Assert.Null(driver.LocationUpdatedAtUtc);
    }

    private static void PrepareForApproval(DriverProfile driver)
    {
        driver.AddVehicle(new Vehicle("Yamaha", "FZ", 2024, "Negra", "ABC12D", VehicleType.Motorcycle, "BUC"));
        AddApprovedDocument(driver, DriverDocumentType.PersonalIdentification);
        AddApprovedDocument(driver, DriverDocumentType.DriverLicense);
        AddApprovedDocument(driver, DriverDocumentType.VehicleRegistration);
        AddApprovedDocument(driver, DriverDocumentType.Insurance);
        AddApprovedDocument(driver, DriverDocumentType.Roadworthiness);
    }

    private static void AddApprovedDocument(DriverProfile driver, DriverDocumentType type)
    {
        var document = new DriverDocument(type, $"driver-documents/{type}.pdf", null);
        driver.AddDocument(document);
        document.Review(true, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
    }
}
