using Hagale.Application.Common;
using Hagale.Domain.Drivers;

namespace Hagale.Application.Drivers;

public interface IDriverApplicationService
{
    Task<ApplicationResult<DriverProfileDto>> ApplyAsync(Guid userId, CreateDriverApplicationCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverApplicationPageDto>> ListForReviewAsync(DriverStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> RegisterVehicleAsync(Guid userId, RegisterVehicleCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> UpdateVehicleAsync(Guid userId, Guid vehicleId, UpdateVehicleCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> RegisterDocumentAsync(Guid userId, RegisterDriverDocumentCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> ChangeAvailabilityAsync(Guid userId, ChangeAvailabilityCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> UpdateLocationAsync(Guid userId, UpdateDriverLocationCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverDocumentFileDto>> GetDocumentForReviewAsync(Guid administratorUserId, Guid driverProfileId, Guid documentId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> StartReviewAsync(Guid administratorUserId, Guid driverProfileId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> ReviewDocumentAsync(Guid administratorUserId, Guid driverProfileId, Guid documentId, ReviewDecision decision, CancellationToken cancellationToken = default);
    Task<ApplicationResult<DriverProfileDto>> ReviewApplicationAsync(Guid administratorUserId, Guid driverProfileId, ReviewDecision decision, CancellationToken cancellationToken = default);
}
