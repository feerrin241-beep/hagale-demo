using Hagale.Application.Contracts;
using Hagale.Application.Common;
using Hagale.Domain.Rides;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class RideRequestRepository(HagaleDbContext database) : IRideRequestRepository
{
    public void Add(RideRequest rideRequest) => database.RideRequests.Add(rideRequest);

    public Task<RideRequest?> GetByIdAsync(Guid rideRequestId, CancellationToken cancellationToken = default) =>
        database.RideRequests
            .Include(request => request.DriverOffers)
            .SingleOrDefaultAsync(request => request.Id == rideRequestId, cancellationToken);

    public async Task<RideRequest?> ReloadByIdAsync(Guid rideRequestId, CancellationToken cancellationToken = default)
    {
        // After a concurrency exception, discard the stale aggregate and any
        // uncommitted offer entities before reading the current database state.
        foreach (var offerEntry in database.ChangeTracker.Entries<RideDriverOffer>()
                     .Where(entry => entry.Property(offer => offer.RideRequestId).CurrentValue == rideRequestId)
                     .ToArray())
        {
            offerEntry.State = EntityState.Detached;
        }

        foreach (var requestEntry in database.ChangeTracker.Entries<RideRequest>()
                     .Where(entry => entry.Entity.Id == rideRequestId)
                     .ToArray())
        {
            requestEntry.State = EntityState.Detached;
        }

        return await GetByIdAsync(rideRequestId, cancellationToken);
    }

    public async Task<IRideRequestWriteLock> LockForUpdateAsync(
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        DetachAggregate(rideRequestId);

        IDbContextTransaction? transaction = null;
        try
        {
            if (database.Database.IsRelational())
            {
                var isolation = database.Database.IsNpgsql() || database.Database.IsSqlServer()
                    ? IsolationLevel.ReadCommitted
                    : IsolationLevel.Serializable;
                transaction = await database.Database.BeginTransactionAsync(isolation, cancellationToken);

                if (database.Database.IsNpgsql())
                {
                    // Serialize every writer for this ride across all app instances.
                    // Unlike an in-process semaphore, a transaction advisory lock
                    // is shared by every Render instance connected to this database.
                    var lockKey = BitConverter.ToInt64(rideRequestId.ToByteArray(), 0);
                    await database.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock({lockKey})",
                        cancellationToken);

                    await database.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM \"RideRequests\" WHERE \"Id\" = {rideRequestId} FOR UPDATE",
                        cancellationToken);
                }
                else if (database.Database.IsSqlServer())
                {
                    await database.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM [RideRequests] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {rideRequestId}",
                        cancellationToken);
                }
            }

            var request = await GetByIdAsync(rideRequestId, cancellationToken);
            if (request is null)
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }

                return new RideRequestWriteLock(database, null, null);
            }

            return new RideRequestWriteLock(database, request, transaction);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            throw;
        }
    }

    public async Task<IReadOnlyCollection<RideRequest>> ListByCustomerUserIdAsync(
        Guid customerUserId,
        CancellationToken cancellationToken = default) =>
        await database.RideRequests
            .AsNoTracking()
            .Where(request => request.CustomerUserId == customerUserId)
            .OrderByDescending(request => request.RequestedAtUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RideRequest>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        await database.RideRequests
            .AsNoTracking()
            .Include(request => request.DriverOffers)
            .Where(request => request.Status == RideRequestStatus.Pending)
            .OrderBy(request => request.RequestedAtUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RideRequest>> ListCompletedByDriverProfileIdAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken = default) =>
        await database.RideRequests
            .AsNoTracking()
            .Where(request => request.AssignedDriverProfileId == driverProfileId && request.Status == RideRequestStatus.Completed)
            .OrderByDescending(request => request.CompletedAtUtc)
            .ToArrayAsync(cancellationToken);

    public Task<RideRequest?> GetActiveByDriverProfileIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
        database.RideRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request => request.AssignedDriverProfileId == driverProfileId &&
                    (request.Status == RideRequestStatus.CounterOfferPending ||
                     request.Status == RideRequestStatus.Accepted ||
                     request.Status == RideRequestStatus.DriverEnRoute ||
                     request.Status == RideRequestStatus.DriverArrived ||
                     request.Status == RideRequestStatus.InProgress),
                cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrentUpdateException("La solicitud fue actualizada por otra operación.", exception);
        }
    }

    private void DetachAggregate(Guid rideRequestId)
    {
        foreach (var offerEntry in database.ChangeTracker.Entries<RideDriverOffer>()
                     .Where(entry => entry.Property(offer => offer.RideRequestId).CurrentValue == rideRequestId)
                     .ToArray())
        {
            offerEntry.State = EntityState.Detached;
        }

        foreach (var requestEntry in database.ChangeTracker.Entries<RideRequest>()
                     .Where(entry => entry.Entity.Id == rideRequestId)
                     .ToArray())
        {
            requestEntry.State = EntityState.Detached;
        }
    }

    private sealed class RideRequestWriteLock(
        HagaleDbContext database,
        RideRequest? request,
        IDbContextTransaction? transaction) : IRideRequestWriteLock
    {
        private bool completed;

        public RideRequest? Request { get; } = request;

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(completed, this);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                completed = true;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await RollbackAndDisposeAsync();
                throw new ConcurrentUpdateException("La solicitud fue actualizada por otra operación.", exception);
            }
            finally
            {
                if (completed && transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (completed)
            {
                return;
            }

            await RollbackAndDisposeAsync();
        }

        private async Task RollbackAndDisposeAsync()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            if (transaction is null)
            {
                return;
            }

            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
