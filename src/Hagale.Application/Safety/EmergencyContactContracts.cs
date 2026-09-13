using Hagale.Application.Common;

namespace Hagale.Application.Safety;

public sealed record CreateEmergencyContactCommand(string Name, string PhoneNumber, string? Relationship);
public sealed record UpdateEmergencyContactCommand(string Name, string PhoneNumber, string? Relationship);

public sealed record EmergencyContactDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    string? Relationship,
    DateTimeOffset CreatedAtUtc);

public interface IEmergencyContactService
{
    Task<IReadOnlyCollection<EmergencyContactDto>> ListMineAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<EmergencyContactDto>> CreateAsync(Guid userId, CreateEmergencyContactCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<EmergencyContactDto>> UpdateAsync(Guid userId, Guid emergencyContactId, UpdateEmergencyContactCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<bool>> ArchiveAsync(Guid userId, Guid emergencyContactId, CancellationToken cancellationToken = default);
}
