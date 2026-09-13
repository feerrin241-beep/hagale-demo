using Hagale.Application.Contracts;
using Hagale.Application.Common;
using Hagale.Application.Drivers;
using Hagale.Domain.Drivers;

namespace Hagale.Application.Tests.Drivers;

public sealed class DriverApplicationServiceTests
{
    [Fact]
    public async Task Application_is_approved_only_after_vehicle_and_document_review()
    {
        var repository = new InMemoryDriverRepository();
        var userId = Guid.NewGuid();
        var service = new DriverApplicationService(repository, new ExistingUserDirectory(userId), TimeProvider.System);

        var application = await service.ApplyAsync(userId, new CreateDriverApplicationCommand());
        var applicationId = application.Value!.Id;
        await service.RegisterVehicleAsync(userId, new RegisterVehicleCommand("Honda", "CB125F", 2025, "Roja", "XYZ12A", VehicleType.Motorcycle, "BUC"));

        var beforeReview = await service.ReviewApplicationAsync(Guid.NewGuid(), applicationId, new ReviewDecision(true, null));
        var documentReview = await RegisterAndApproveRequiredDocumentsAsync(service, userId, applicationId);
        var approval = await service.ReviewApplicationAsync(Guid.NewGuid(), applicationId, new ReviewDecision(true, "Verificado."));

        Assert.False(beforeReview.IsSuccess);
        Assert.All(documentReview, result => Assert.True(result.IsSuccess));
        Assert.True(approval.IsSuccess);
        Assert.Equal(DriverStatus.Approved, approval.Value!.Status);
    }

    [Fact]
    public async Task Approved_driver_location_is_exposed_in_the_profile_dto()
    {
        var repository = new InMemoryDriverRepository();
        var userId = Guid.NewGuid();
        var service = new DriverApplicationService(repository, new ExistingUserDirectory(userId), TimeProvider.System);

        var application = await service.ApplyAsync(userId, new CreateDriverApplicationCommand());
        await service.RegisterVehicleAsync(userId, new RegisterVehicleCommand("Honda", "CB125F", 2025, "Roja", "XYZ12A", VehicleType.Motorcycle, "BUC"));
        await RegisterAndApproveRequiredDocumentsAsync(service, userId, application.Value!.Id);
        await service.ReviewApplicationAsync(Guid.NewGuid(), application.Value!.Id, new ReviewDecision(true, "Verificado."));
        await service.ChangeAvailabilityAsync(userId, new ChangeAvailabilityCommand(DriverAvailabilityStatus.Available));

        var updated = await service.UpdateLocationAsync(userId, new UpdateDriverLocationCommand(7.1193m, -73.1227m));

        Assert.True(updated.IsSuccess);
        Assert.Equal(7.1193m, updated.Value!.LastKnownLatitude);
        Assert.Equal(-73.1227m, updated.Value!.LastKnownLongitude);
        Assert.NotNull(updated.Value.LocationUpdatedAtUtc);
    }

    [Fact]
    public async Task Administrator_can_prepare_private_document_for_review()
    {
        var repository = new InMemoryDriverRepository();
        var userId = Guid.NewGuid();
        var service = new DriverApplicationService(repository, new ExistingUserDirectory(userId), TimeProvider.System);

        var application = await service.ApplyAsync(userId, new CreateDriverApplicationCommand());
        var document = await service.RegisterDocumentAsync(userId, new RegisterDriverDocumentCommand(DriverDocumentType.Insurance, "private/owner/soat.png", null));
        var documentId = document.Value!.Documents.Single().Id;

        var result = await service.GetDocumentForReviewAsync(Guid.NewGuid(), application.Value!.Id, documentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(documentId, result.Value!.Id);
        Assert.Equal(DriverDocumentType.Insurance, result.Value.Type);
        Assert.Equal("private/owner/soat.png", result.Value.StorageObjectKey);
        Assert.Equal($"Insurance-{documentId:N}.png", result.Value.DownloadFileName);
        Assert.DoesNotContain("/", result.Value.DownloadFileName);
        Assert.DoesNotContain("\\", result.Value.DownloadFileName);
    }

    [Fact]
    public async Task Private_document_review_requires_administrator_and_matching_document()
    {
        var repository = new InMemoryDriverRepository();
        var userId = Guid.NewGuid();
        var service = new DriverApplicationService(repository, new ExistingUserDirectory(userId), TimeProvider.System);

        var application = await service.ApplyAsync(userId, new CreateDriverApplicationCommand());
        var document = await service.RegisterDocumentAsync(userId, new RegisterDriverDocumentCommand(DriverDocumentType.DriverLicense, "private/license.pdf", null));
        var documentId = document.Value!.Documents.Single().Id;

        var withoutAdministrator = await service.GetDocumentForReviewAsync(Guid.Empty, application.Value!.Id, documentId);
        var wrongDocument = await service.GetDocumentForReviewAsync(Guid.NewGuid(), application.Value!.Id, Guid.NewGuid());

        Assert.False(withoutAdministrator.IsSuccess);
        Assert.Contains("administrador", withoutAdministrator.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(wrongDocument.IsSuccess);
        Assert.Contains("no pertenece", wrongDocument.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Application_and_review_changes_notify_the_applicant_after_being_saved()
    {
        var repository = new InMemoryDriverRepository();
        var userId = Guid.NewGuid();
        var notifier = new RecordingDriverApplicationRealtimeNotifier();
        var service = new DriverApplicationService(
            repository,
            new ExistingUserDirectory(userId),
            TimeProvider.System,
            driverApplicationRealtimeNotifier: notifier);

        var application = await service.ApplyAsync(userId, new CreateDriverApplicationCommand());

        Assert.True(application.IsSuccess);
        Assert.Equal([userId], notifier.ApplicantUserIds);

        notifier.Clear();
        await service.RegisterVehicleAsync(userId, new RegisterVehicleCommand("Honda", "CB125F", 2025, "Roja", "XYZ12A", VehicleType.Motorcycle, "BUC"));
        await RegisterAndApproveRequiredDocumentsAsync(service, userId, application.Value!.Id);
        var approved = await service.ReviewApplicationAsync(Guid.NewGuid(), application.Value!.Id, new ReviewDecision(true, "Verificado."));

        Assert.True(approved.IsSuccess);
        Assert.Equal(10, notifier.ApplicantUserIds.Count);
        Assert.All(notifier.ApplicantUserIds, notifiedUserId => Assert.Equal(userId, notifiedUserId));
    }

    private static async Task<IReadOnlyCollection<ApplicationResult<DriverProfileDto>>> RegisterAndApproveRequiredDocumentsAsync(
        DriverApplicationService service,
        Guid userId,
        Guid applicationId)
    {
        var results = new List<ApplicationResult<DriverProfileDto>>();
        foreach (var type in new[]
                 {
                     DriverDocumentType.PersonalIdentification,
                     DriverDocumentType.DriverLicense,
                     DriverDocumentType.VehicleRegistration,
                     DriverDocumentType.Insurance
                 })
        {
            var document = await service.RegisterDocumentAsync(userId, new RegisterDriverDocumentCommand(type, $"documents/{type}.pdf", null));
            results.Add(await service.ReviewDocumentAsync(Guid.NewGuid(), applicationId, document.Value!.Documents.Single(item => item.Type == type).Id, new ReviewDecision(true, null)));
        }

        return results;
    }

    private sealed class ExistingUserDirectory(Guid existingUserId) : IUserDirectory
    {
        public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == existingUserId);

        public Task<bool> EnsureRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == existingUserId && roleName == "Driver");
    }

    private sealed class RecordingDriverApplicationRealtimeNotifier : IDriverApplicationRealtimeNotifier
    {
        public List<Guid> ApplicantUserIds { get; } = [];

        public Task NotifyChangedAsync(Guid applicantUserId, CancellationToken cancellationToken = default)
        {
            ApplicantUserIds.Add(applicantUserId);
            return Task.CompletedTask;
        }

        public void Clear() => ApplicantUserIds.Clear();
    }

    private sealed class InMemoryDriverRepository : IDriverRepository
    {
        private readonly List<DriverProfile> _drivers = [];

        public void Add(DriverProfile driverProfile) => _drivers.Add(driverProfile);

        public void AddVehicle(Vehicle vehicle)
        {
        }

        public void AddDocument(DriverDocument document)
        {
        }

        public Task<DriverProfile?> GetByIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drivers.SingleOrDefault(item => item.Id == driverProfileId));

        public Task<DriverProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drivers.SingleOrDefault(item => item.UserId == userId));

        public Task<DriverProfilePage> ListAsync(
            DriverStatus? status,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            var matches = _drivers
                .Where(driver => status is null || driver.Status == status)
                .Skip(skip)
                .Take(take)
                .ToArray();
            var total = _drivers.Count(driver => status is null || driver.Status == status);
            return Task.FromResult(new DriverProfilePage(matches, total));
        }

        public Task<bool> IsVehiclePlateInUseAsync(string plate, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drivers.SelectMany(item => item.Vehicles).Any(item => item.Plate == plate));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
