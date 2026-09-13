using Hagale.Application.Contracts;
using Hagale.Application.Safety;
using Hagale.Domain.Safety;

namespace Hagale.Application.Tests.Safety;

public sealed class EmergencyContactServiceTests
{
    [Fact]
    public async Task Account_can_create_and_archive_its_own_contact()
    {
        var accountId = Guid.NewGuid();
        var repository = new InMemoryEmergencyContactRepository();
        var service = new EmergencyContactService(repository, new ExistingUserDirectory(accountId), TimeProvider.System);

        var created = await service.CreateAsync(
            accountId,
            new CreateEmergencyContactCommand("Ana Pérez", "+573001234567", "Hermana"));
        var archived = await service.ArchiveAsync(accountId, created.Value!.Id);

        Assert.True(created.IsSuccess);
        Assert.True(archived.IsSuccess);
        Assert.Empty(await service.ListMineAsync(accountId));
    }

    [Fact]
    public async Task Account_cannot_archive_someone_elses_contact()
    {
        var accountId = Guid.NewGuid();
        var contactOwnedByAnotherAccount = new EmergencyContact(
            Guid.NewGuid(),
            "Ana Pérez",
            "+573001234567",
            "Hermana",
            DateTimeOffset.UtcNow);
        var repository = new InMemoryEmergencyContactRepository();
        repository.Add(contactOwnedByAnotherAccount);
        var service = new EmergencyContactService(repository, new ExistingUserDirectory(accountId), TimeProvider.System);

        var result = await service.ArchiveAsync(accountId, contactOwnedByAnotherAccount.Id);

        Assert.False(result.IsSuccess);
        Assert.True(contactOwnedByAnotherAccount.IsActive);
    }

    private sealed class ExistingUserDirectory(Guid existingUserId) : IUserDirectory
    {
        public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == existingUserId);

        public Task<bool> EnsureRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository
    {
        private readonly List<EmergencyContact> _contacts = [];

        public void Add(EmergencyContact emergencyContact) => _contacts.Add(emergencyContact);

        public Task<EmergencyContact?> GetByIdAsync(Guid emergencyContactId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_contacts.SingleOrDefault(contact => contact.Id == emergencyContactId));

        public Task<IReadOnlyCollection<EmergencyContact>> ListActiveByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EmergencyContact>>(
                _contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
