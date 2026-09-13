using Hagale.Domain.Drivers;

namespace Hagale.Application.Contracts;

public interface IDriverRepository
{
    Task<DriverProfile?> GetByIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default);
    Task<DriverProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DriverProfilePage> ListAsync(DriverStatus? status, int skip, int take, CancellationToken cancellationToken = default);
    Task<bool> IsVehiclePlateInUseAsync(string plate, CancellationToken cancellationToken = default);
    void Add(DriverProfile driverProfile);
    void AddVehicle(Vehicle vehicle);
    void AddDocument(DriverDocument document);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record DriverProfilePage(IReadOnlyCollection<DriverProfile> Items, int TotalCount);
