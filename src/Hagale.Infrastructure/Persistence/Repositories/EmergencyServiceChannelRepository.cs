using Hagale.Application.Contracts;
using Hagale.Domain.Safety;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class EmergencyServiceChannelRepository(HagaleDbContext database) : IEmergencyServiceChannelRepository
{
    public void Add(EmergencyServiceChannel emergencyServiceChannel) => database.EmergencyServiceChannels.Add(emergencyServiceChannel);

    public Task<EmergencyServiceChannel?> GetByIdAsync(Guid emergencyServiceChannelId, CancellationToken cancellationToken = default) =>
        database.EmergencyServiceChannels.SingleOrDefaultAsync(channel => channel.Id == emergencyServiceChannelId, cancellationToken);

    public Task<EmergencyServiceChannel?> GetByCityAndTypeAsync(
        string cityCode,
        EmergencyChannelType channelType,
        CancellationToken cancellationToken = default)
    {
        var normalizedCityCode = cityCode?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalizedCityCode)
            ? Task.FromResult<EmergencyServiceChannel?>(null)
            : database.EmergencyServiceChannels.SingleOrDefaultAsync(
                channel => channel.CityCode == normalizedCityCode && channel.ChannelType == channelType,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmergencyServiceChannel>> ListAsync(CancellationToken cancellationToken = default) =>
        await database.EmergencyServiceChannels
            .AsNoTracking()
            .OrderBy(channel => channel.CityCode)
            .ThenBy(channel => channel.ChannelType)
            .ToArrayAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);
}
