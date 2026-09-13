using Hagale.Application.Contracts;
using Hagale.Domain.Drivers;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class DriverRepository(HagaleDbContext database) : IDriverRepository
{
    public void Add(DriverProfile driverProfile) => database.DriverProfiles.Add(driverProfile);

    public void AddVehicle(Vehicle vehicle) => database.Vehicles.Add(vehicle);

    public void AddDocument(DriverDocument document) => database.DriverDocuments.Add(document);

    public Task<DriverProfile?> GetByIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
        QueryProfiles().SingleOrDefaultAsync(driver => driver.Id == driverProfileId, cancellationToken);

    public Task<DriverProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        QueryProfiles().SingleOrDefaultAsync(driver => driver.UserId == userId, cancellationToken);

    public async Task<DriverProfilePage> ListAsync(
        DriverStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = database.DriverProfiles.AsQueryable();
        if (status is not null)
        {
            query = query.Where(driver => driver.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await QueryProfiles(asNoTracking: true)
            .Where(driver => status == null || driver.Status == status)
            .OrderByDescending(driver => driver.AppliedAtUtc)
            .ThenBy(driver => driver.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return new DriverProfilePage(items, totalCount);
    }

    public Task<bool> IsVehiclePlateInUseAsync(string plate, CancellationToken cancellationToken = default) =>
        database.Vehicles.AnyAsync(vehicle => vehicle.Plate == plate, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => database.SaveChangesAsync(cancellationToken);

    private IQueryable<DriverProfile> QueryProfiles(bool asNoTracking = false)
    {
        IQueryable<DriverProfile> query = database.DriverProfiles
            .Include(driver => driver.Vehicles)
            .Include(driver => driver.Documents)
            .AsSplitQuery();

        return asNoTracking ? query.AsNoTracking() : query;
    }
}
