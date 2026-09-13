using Hagale.Application.Common;
using Hagale.Domain.Safety;

namespace Hagale.Application.Safety;

public sealed record CreateEmergencyServiceChannelCommand(
    string CityCode,
    EmergencyChannelType ChannelType,
    string DisplayName,
    string ContactNumber,
    bool IsActive);

public sealed record UpdateEmergencyServiceChannelCommand(
    string DisplayName,
    string ContactNumber,
    bool IsActive);

public sealed record EmergencyServiceChannelDto(
    Guid Id,
    string CityCode,
    EmergencyChannelType ChannelType,
    string DisplayName,
    string ContactNumber,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc);

public interface IEmergencyServiceChannelService
{
    Task<IReadOnlyCollection<EmergencyServiceChannelDto>> ListForAdministrationAsync(CancellationToken cancellationToken = default);
    Task<ApplicationResult<EmergencyServiceChannelDto>> CreateAsync(CreateEmergencyServiceChannelCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<EmergencyServiceChannelDto>> UpdateAsync(Guid emergencyServiceChannelId, UpdateEmergencyServiceChannelCommand command, CancellationToken cancellationToken = default);
}
