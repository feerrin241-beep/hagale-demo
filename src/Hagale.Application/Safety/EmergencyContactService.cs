using Hagale.Application.Common;
using Hagale.Application.Contracts;
using Hagale.Domain.Common;
using Hagale.Domain.Safety;

namespace Hagale.Application.Safety;

public sealed class EmergencyContactService(
    IEmergencyContactRepository emergencyContactRepository,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IEmergencyContactService
{
    public async Task<IReadOnlyCollection<EmergencyContactDto>> ListMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        (await emergencyContactRepository.ListActiveByUserIdAsync(userId, cancellationToken))
        .Select(Map)
        .ToArray();

    public async Task<ApplicationResult<EmergencyContactDto>> CreateAsync(
        Guid userId,
        CreateEmergencyContactCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!await userDirectory.ExistsAsync(userId, cancellationToken))
        {
            return ApplicationResult<EmergencyContactDto>.Failure("La cuenta no está disponible.");
        }

        try
        {
            var emergencyContact = new EmergencyContact(
                userId,
                command.Name,
                command.PhoneNumber,
                command.Relationship,
                timeProvider.GetUtcNow());
            emergencyContactRepository.Add(emergencyContact);
            await emergencyContactRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<EmergencyContactDto>.Success(Map(emergencyContact));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<EmergencyContactDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<EmergencyContactDto>> UpdateAsync(
        Guid userId,
        Guid emergencyContactId,
        UpdateEmergencyContactCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var contact = await GetOwnedActiveContactAsync(userId, emergencyContactId, cancellationToken);
        if (contact is null)
        {
            return ApplicationResult<EmergencyContactDto>.Failure("El contacto no está disponible.");
        }

        try
        {
            contact.Update(command.Name, command.PhoneNumber, command.Relationship);
            await emergencyContactRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<EmergencyContactDto>.Success(Map(contact));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<EmergencyContactDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<bool>> ArchiveAsync(
        Guid userId,
        Guid emergencyContactId,
        CancellationToken cancellationToken = default)
    {
        var contact = await GetOwnedActiveContactAsync(userId, emergencyContactId, cancellationToken);
        if (contact is null)
        {
            return ApplicationResult<bool>.Failure("El contacto no está disponible.");
        }

        try
        {
            contact.Archive(timeProvider.GetUtcNow());
            await emergencyContactRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<bool>.Success(true);
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<bool>.Failure(exception.Message);
        }
    }

    private async Task<EmergencyContact?> GetOwnedActiveContactAsync(
        Guid userId,
        Guid emergencyContactId,
        CancellationToken cancellationToken)
    {
        var contact = await emergencyContactRepository.GetByIdAsync(emergencyContactId, cancellationToken);
        return contact is not null && contact.UserId == userId && contact.IsActive ? contact : null;
    }

    private static EmergencyContactDto Map(EmergencyContact contact) => new(
        contact.Id,
        contact.Name,
        contact.PhoneNumber,
        contact.Relationship,
        contact.CreatedAtUtc);
}
