using Hagale.Application.Common;
using Hagale.Application.Contracts;
using Hagale.Domain.Common;
using Hagale.Domain.Safety;

namespace Hagale.Application.Safety;

public sealed class EmergencyServiceChannelService(
    IEmergencyServiceChannelRepository emergencyServiceChannelRepository,
    TimeProvider timeProvider) : IEmergencyServiceChannelService
{
    public async Task<IReadOnlyCollection<EmergencyServiceChannelDto>> ListForAdministrationAsync(CancellationToken cancellationToken = default) =>
        (await emergencyServiceChannelRepository.ListAsync(cancellationToken)).Select(Map).ToArray();

    public async Task<ApplicationResult<EmergencyServiceChannelDto>> CreateAsync(
        CreateEmergencyServiceChannelCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            var channel = new EmergencyServiceChannel(
                command.CityCode,
                command.ChannelType,
                command.DisplayName,
                command.ContactNumber,
                command.IsActive,
                timeProvider.GetUtcNow());
            var existing = await emergencyServiceChannelRepository.GetByCityAndTypeAsync(
                channel.CityCode,
                channel.ChannelType,
                cancellationToken);
            if (existing is not null)
            {
                return ApplicationResult<EmergencyServiceChannelDto>.Failure("Ya existe un canal de ese tipo para la ciudad.");
            }

            emergencyServiceChannelRepository.Add(channel);
            await emergencyServiceChannelRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<EmergencyServiceChannelDto>.Success(Map(channel));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<EmergencyServiceChannelDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<EmergencyServiceChannelDto>> UpdateAsync(
        Guid emergencyServiceChannelId,
        UpdateEmergencyServiceChannelCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var channel = await emergencyServiceChannelRepository.GetByIdAsync(emergencyServiceChannelId, cancellationToken);
        if (channel is null)
        {
            return ApplicationResult<EmergencyServiceChannelDto>.Failure("El canal de emergencia no existe.");
        }

        try
        {
            channel.Update(command.DisplayName, command.ContactNumber, command.IsActive, timeProvider.GetUtcNow());
            await emergencyServiceChannelRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<EmergencyServiceChannelDto>.Success(Map(channel));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<EmergencyServiceChannelDto>.Failure(exception.Message);
        }
    }

    private static EmergencyServiceChannelDto Map(EmergencyServiceChannel channel) => new(
        channel.Id,
        channel.CityCode,
        channel.ChannelType,
        channel.DisplayName,
        channel.ContactNumber,
        channel.IsActive,
        channel.UpdatedAtUtc);
}
