using Hagale.Domain.Safety;

namespace Hagale.Application.Contracts;

public interface IEmergencyServiceChannelRepository
{
    Task<EmergencyServiceChannel?> GetByIdAsync(Guid emergencyServiceChannelId, CancellationToken cancellationToken = default);
    Task<EmergencyServiceChannel?> GetByCityAndTypeAsync(string cityCode, EmergencyChannelType channelType, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EmergencyServiceChannel>> ListAsync(CancellationToken cancellationToken = default);
    void Add(EmergencyServiceChannel emergencyServiceChannel);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
