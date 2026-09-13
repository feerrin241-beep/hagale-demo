using Hagale.Application.Contracts;
using Hagale.Domain.Safety;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class EmergencyContactRepository(HagaleDbContext database) : IEmergencyContactRepository
{
    public void Add(EmergencyContact emergencyContact) => database.EmergencyContacts.Add(emergencyContact);

    public Task<EmergencyContact?> GetByIdAsync(Guid emergencyContactId, CancellationToken cancellationToken = default) =>
        database.EmergencyContacts.SingleOrDefaultAsync(contact => contact.Id == emergencyContactId, cancellationToken);

    public async Task<IReadOnlyCollection<EmergencyContact>> ListActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await database.EmergencyContacts
            .AsNoTracking()
            .Where(contact => contact.UserId == userId && contact.ArchivedAtUtc == null)
            .OrderBy(contact => contact.Name)
            .ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}
