using Hagale.Application.Contracts;
using Hagale.Application.Safety;
using Hagale.Domain.Safety;

namespace Hagale.Application.Tests.Safety;

public sealed class EmergencyServiceChannelServiceTests
{
    [Fact]
    public async Task Administrator_configuration_cannot_duplicate_a_city_and_channel_type()
    {
        var repository = new InMemoryEmergencyServiceChannelRepository();
        var service = new EmergencyServiceChannelService(repository, TimeProvider.System);
        var command = new CreateEmergencyServiceChannelCommand(
            "BUC",
            EmergencyChannelType.GeneralEmergency,
            "Atención municipal",
            "123",
            true);

        var first = await service.CreateAsync(command);
        var duplicate = await service.CreateAsync(command with { DisplayName = "Otro nombre" });

        Assert.True(first.IsSuccess);
        Assert.False(duplicate.IsSuccess);
    }

    private sealed class InMemoryEmergencyServiceChannelRepository : IEmergencyServiceChannelRepository
    {
        private readonly List<EmergencyServiceChannel> _channels = [];

        public void Add(EmergencyServiceChannel emergencyServiceChannel) => _channels.Add(emergencyServiceChannel);

        public Task<EmergencyServiceChannel?> GetByIdAsync(Guid emergencyServiceChannelId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_channels.SingleOrDefault(channel => channel.Id == emergencyServiceChannelId));

        public Task<EmergencyServiceChannel?> GetByCityAndTypeAsync(
            string cityCode,
            EmergencyChannelType channelType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_channels.SingleOrDefault(channel =>
                channel.CityCode == cityCode.Trim().ToUpperInvariant() && channel.ChannelType == channelType));

        public Task<IReadOnlyCollection<EmergencyServiceChannel>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EmergencyServiceChannel>>(_channels.ToArray());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
