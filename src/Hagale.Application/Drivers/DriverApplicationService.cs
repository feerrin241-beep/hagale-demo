using Hagale.Application.Common;
using Hagale.Application.Contracts;
using Hagale.Application.Authentication;
using Hagale.Application.Rides;
using Hagale.Domain.Common;
using Hagale.Domain.Drivers;
using Hagale.Domain.Rides;

namespace Hagale.Application.Drivers;

public sealed class DriverApplicationService(
    IDriverRepository driverRepository,
    IUserDirectory userDirectory,
    TimeProvider timeProvider,
    IRideRequestRepository? rideRequestRepository = null,
    IRideRealtimeNotifier? realtimeNotifier = null,
    IDriverApplicationRealtimeNotifier? driverApplicationRealtimeNotifier = null) : IDriverApplicationService
{
    private readonly IDriverApplicationRealtimeNotifier applicationRealtimeNotifier =
        driverApplicationRealtimeNotifier ?? NullDriverApplicationRealtimeNotifier.Instance;

    public async Task<ApplicationResult<DriverProfileDto>> ApplyAsync(
        Guid userId,
        CreateDriverApplicationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await userDirectory.ExistsAsync(userId, cancellationToken))
        {
            return ApplicationResult<DriverProfileDto>.Failure("El usuario no existe.");
        }

        if (await driverRepository.GetByUserIdAsync(userId, cancellationToken) is not null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("El usuario ya tiene una solicitud de conductor.");
        }

        var driver = new DriverProfile(userId, timeProvider.GetUtcNow());
        driverRepository.Add(driver);
        await driverRepository.SaveChangesAsync(cancellationToken);
        await NotifyApplicationChangedAsync(driver, cancellationToken);

        return ApplicationResult<DriverProfileDto>.Success(Map(driver));
    }

    public async Task<ApplicationResult<DriverProfileDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        return driver is null
            ? ApplicationResult<DriverProfileDto>.Failure("El usuario no tiene una solicitud de conductor.")
            : ApplicationResult<DriverProfileDto>.Success(Map(driver));
    }

    public async Task<ApplicationResult<DriverApplicationPageDto>> ListForReviewAsync(
        DriverStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return ApplicationResult<DriverApplicationPageDto>.Failure("La paginación debe usar una página desde 1 y un tamaño entre 1 y 100.");
        }

        var result = await driverRepository.ListAsync(status, (page - 1) * pageSize, pageSize, cancellationToken);
        return ApplicationResult<DriverApplicationPageDto>.Success(new DriverApplicationPageDto(
            result.Items.Select(Map).ToArray(),
            page,
            pageSize,
            result.TotalCount));
    }

    public async Task<ApplicationResult<DriverProfileDto>> RegisterVehicleAsync(
        Guid userId,
        RegisterVehicleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("El usuario no tiene una solicitud de conductor.");
        }

        try
        {
            var vehicle = new Vehicle(command.Brand, command.Model, command.Year, command.Color, command.Plate, command.Type, command.OperatingCityCode);
            if (await driverRepository.IsVehiclePlateInUseAsync(vehicle.Plate, cancellationToken))
            {
                return ApplicationResult<DriverProfileDto>.Failure("La placa ya está registrada en HÁGALE.");
            }

            driver.AddVehicle(vehicle);
            driverRepository.AddVehicle(vehicle);
            await driverRepository.SaveChangesAsync(cancellationToken);
            await NotifyApplicationChangedAsync(driver, cancellationToken);
            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<DriverProfileDto>> RegisterDocumentAsync(
        Guid userId,
        RegisterDriverDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("El usuario no tiene una solicitud de conductor.");
        }

        try
        {
            var document = new DriverDocument(command.Type, command.StorageObjectKey, command.ExpiresOn);
            driver.AddDocument(document);
            driverRepository.AddDocument(document);
            await driverRepository.SaveChangesAsync(cancellationToken);
            await NotifyApplicationChangedAsync(driver, cancellationToken);
            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<DriverProfileDto>> UpdateVehicleAsync(
        Guid userId,
        Guid vehicleId,
        UpdateVehicleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        var vehicle = driver?.Vehicles.SingleOrDefault(item => item.Id == vehicleId);
        if (driver is null || vehicle is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("La motocicleta no pertenece a tu solicitud.");
        }

        try
        {
            var normalizedPlate = command.Plate.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            if (!vehicle.Plate.Equals(normalizedPlate, StringComparison.OrdinalIgnoreCase) && await driverRepository.IsVehiclePlateInUseAsync(normalizedPlate, cancellationToken))
            {
                return ApplicationResult<DriverProfileDto>.Failure("La placa ya está registrada en HÁGALE.");
            }

            vehicle.UpdateDetails(command.Brand, command.Model, command.Year, command.Color, command.Plate, command.Type, command.OperatingCityCode);
            await driverRepository.SaveChangesAsync(cancellationToken);
            await NotifyApplicationChangedAsync(driver, cancellationToken);
            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<DriverProfileDto>> ChangeAvailabilityAsync(
        Guid userId,
        ChangeAvailabilityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AvailabilityStatus == DriverAvailabilityStatus.Busy)
        {
            return ApplicationResult<DriverProfileDto>.Failure("El estado ocupado solo se actualiza por la operación de un viaje.");
        }

        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("El usuario no tiene una solicitud de conductor.");
        }

        try
        {
            driver.SetAvailability(command.AvailabilityStatus);
            await driverRepository.SaveChangesAsync(cancellationToken);
            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<DriverProfileDto>> UpdateLocationAsync(
        Guid userId,
        UpdateDriverLocationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("El usuario no tiene una solicitud de conductor.");
        }

        try
        {
            driver.UpdateCurrentLocation(command.Latitude, command.Longitude, timeProvider.GetUtcNow());
            await driverRepository.SaveChangesAsync(cancellationToken);

            // Solo se avisa al pasajero del recorrido activo. La ubicación no
            // se publica a conductores, administradores ni a otros clientes.
            if (rideRequestRepository is not null && realtimeNotifier is not null)
            {
                var activeRide = await rideRequestRepository.GetActiveByDriverProfileIdAsync(driver.Id, cancellationToken);
                if (activeRide?.Status is RideRequestStatus.Accepted
                    or RideRequestStatus.DriverEnRoute
                    or RideRequestStatus.DriverArrived
                    or RideRequestStatus.InProgress)
                {
                    await realtimeNotifier.NotifyDriverLocationChangedAsync(
                        activeRide.CustomerUserId,
                        activeRide.Id,
                        cancellationToken);
                }
            }

            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<DriverProfileDto>> StartReviewAsync(
        Guid administratorUserId,
        Guid driverProfileId,
        CancellationToken cancellationToken = default)
    {
        if (administratorUserId == Guid.Empty)
        {
            return ApplicationResult<DriverProfileDto>.Failure("La revisión debe identificar al administrador responsable.");
        }

        var driver = await driverRepository.GetByIdAsync(driverProfileId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("La solicitud de conductor no existe.");
        }

        try
        {
            driver.StartReview(timeProvider.GetUtcNow());
            await driverRepository.SaveChangesAsync(cancellationToken);
            await NotifyApplicationChangedAsync(driver, cancellationToken);
            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<DriverDocumentFileDto>> GetDocumentForReviewAsync(
        Guid administratorUserId,
        Guid driverProfileId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (administratorUserId == Guid.Empty)
        {
            return ApplicationResult<DriverDocumentFileDto>.Failure("La consulta debe identificar al administrador responsable.");
        }

        var driver = await driverRepository.GetByIdAsync(driverProfileId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverDocumentFileDto>.Failure("La solicitud de conductor no existe.");
        }

        var document = driver.Documents.SingleOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            return ApplicationResult<DriverDocumentFileDto>.Failure("El documento no pertenece a esta solicitud.");
        }

        var extension = Path.GetExtension(document.StorageObjectKey);
        var safeFileName = $"{document.Type}-{document.Id:N}{extension}";
        return ApplicationResult<DriverDocumentFileDto>.Success(new DriverDocumentFileDto(
            document.Id,
            document.Type,
            document.StorageObjectKey,
            safeFileName));
    }

    public async Task<ApplicationResult<DriverProfileDto>> ReviewDocumentAsync(
        Guid administratorUserId,
        Guid driverProfileId,
        Guid documentId,
        ReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var driver = await driverRepository.GetByIdAsync(driverProfileId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("La solicitud de conductor no existe.");
        }

        var document = driver.Documents.SingleOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("El documento no pertenece a esta solicitud.");
        }

        try
        {
            document.Review(decision.Approve, decision.Notes, administratorUserId, timeProvider.GetUtcNow());
            await driverRepository.SaveChangesAsync(cancellationToken);
            await NotifyApplicationChangedAsync(driver, cancellationToken);
            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<ApplicationResult<DriverProfileDto>> ReviewApplicationAsync(
        Guid administratorUserId,
        Guid driverProfileId,
        ReviewDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (administratorUserId == Guid.Empty)
        {
            return ApplicationResult<DriverProfileDto>.Failure("La revisión debe identificar al administrador responsable.");
        }

        var driver = await driverRepository.GetByIdAsync(driverProfileId, cancellationToken);
        if (driver is null)
        {
            return ApplicationResult<DriverProfileDto>.Failure("La solicitud de conductor no existe.");
        }

        try
        {
            if (decision.Approve)
            {
                if (!driver.HasMinimumApprovalEvidence())
                {
                    return ApplicationResult<DriverProfileDto>.Failure(BuildMissingApprovalEvidenceMessage(driver));
                }

                driver.Approve(decision.Notes, timeProvider.GetUtcNow());
                if (!await userDirectory.EnsureRoleAsync(driver.UserId, HagaleRoles.Driver, cancellationToken))
                {
                    return ApplicationResult<DriverProfileDto>.Failure("No se pudo habilitar el rol de conductor para el usuario.");
                }
            }
            else
            {
                driver.Reject(decision.Notes ?? string.Empty, timeProvider.GetUtcNow());
            }

            await driverRepository.SaveChangesAsync(cancellationToken);
            await NotifyApplicationChangedAsync(driver, cancellationToken);
            return ApplicationResult<DriverProfileDto>.Success(Map(driver));
        }
        catch (DomainRuleViolationException exception)
        {
            return ApplicationResult<DriverProfileDto>.Failure(exception.Message);
        }
    }

    private Task NotifyApplicationChangedAsync(DriverProfile driver, CancellationToken cancellationToken) =>
        applicationRealtimeNotifier.NotifyChangedAsync(driver.UserId, cancellationToken);

    private static string BuildMissingApprovalEvidenceMessage(DriverProfile driver)
    {
        if (!driver.Vehicles.Any(vehicle => vehicle.IsActive))
        {
            return "No se puede aprobar: requiere una motocicleta activa.";
        }

        var missingDocuments = driver.GetMissingRequiredDocumentTypes();
        return missingDocuments.Count == 0
            ? "No se puede aprobar: requiere vehículo activo y documentos aprobados."
            : $"No se puede aprobar: faltan documentos obligatorios aprobados ({string.Join(", ", missingDocuments)}).";
    }

    private static DriverProfileDto Map(DriverProfile driver) => new(
        driver.Id,
        driver.UserId,
        driver.Status,
        driver.AvailabilityStatus,
        driver.AppliedAtUtc,
        driver.ApprovedAtUtc,
        driver.AdministrativeNotes,
        driver.LastKnownLatitude,
        driver.LastKnownLongitude,
        driver.LocationUpdatedAtUtc,
        driver.Vehicles.Select(vehicle => new VehicleDto(
            vehicle.Id,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Year,
            vehicle.Color,
            vehicle.Plate,
            vehicle.Type,
            vehicle.OperatingCityCode,
            vehicle.IsActive)).ToArray(),
        driver.Documents.Select(document => new DriverDocumentDto(
            document.Id,
            document.Type,
            document.ExpiresOn,
            document.ReviewStatus,
            document.ReviewNotes,
            document.ReviewedAtUtc)).ToArray());
}
