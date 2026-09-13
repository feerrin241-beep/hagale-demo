using Hagale.Domain.Safety;

namespace Hagale.Application.Contracts;

public interface IEmergencyContactRepository
{
    Task<EmergencyContact?> GetByIdAsync(Guid emergencyContactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EmergencyContact>> ListActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(EmergencyContact emergencyContact);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
