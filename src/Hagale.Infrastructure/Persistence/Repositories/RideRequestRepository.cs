using Hagale.Application.Contracts;
using Hagale.Application.Common;
using Hagale.Domain.Rides;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class RideRequestRepository(
    HagaleDbContext database,
    ILogger<RideRequestRepository> logger) : IRideRequestRepository
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

                return new RideRequestWriteLock(database, logger, null, null);
            }

            return new RideRequestWriteLock(database, logger, request, transaction);
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
            LogConcurrencyFailure(exception, rideRequestId: null);
            throw new ConcurrentUpdateException("La solicitud fue actualizada por otra operación.", exception);
        }
    }

    private void LogConcurrencyFailure(DbUpdateConcurrencyException exception, Guid? rideRequestId)
    {
        var entries = string.Join(
            ", ",
            exception.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}"));
        logger.LogError(
            exception,
            "EF concurrency conflict while saving ride data. RequestId={RequestId}; Entries={Entries}; Provider={Provider}",
            rideRequestId,
            entries,
            database.Database.ProviderName);
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
        ILogger<RideRequestRepository> logger,
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
                var entries = string.Join(
                    ", ",
                    exception.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}"));
                logger.LogError(
                    exception,
                    "EF concurrency conflict while saving locked ride request. RequestId={RequestId}; Entries={Entries}; Provider={Provider}",
                    Request?.Id,
                    entries,
                    database.Database.ProviderName);
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

        public async Task SaveDriverOfferAsync(RideDriverOffer offer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(completed, this);
            ArgumentNullException.ThrowIfNull(offer);
            try
            {
                if (database.Database.IsNpgsql())
                {
                    // Offer submission only inserts a child row. Updating the
                    // tracked aggregate here unnecessarily writes RideRequests
                    // and its RowVersion, which can conflict with unrelated
                    // customer refresh/round operations. The request-level
                    // advisory lock and transaction still serialize all writes.
                    await database.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO "RideDriverOffers"
                            ("Id", "RideRequestId", "DriverProfileId", "OfferedPriceCop", "OfferedAtUtc", "ExpiresAtUtc", "SearchRound", "Status", "ResolvedAtUtc")
                        VALUES
                            ({offer.Id}, {offer.RideRequestId}, {offer.DriverProfileId}, {offer.OfferedPriceCop}, {offer.OfferedAtUtc}, {offer.ExpiresAtUtc}, {offer.SearchRound}, {offer.Status.ToString()}, {offer.ResolvedAtUtc})
                        """,
                        cancellationToken);

                    // Keep the in-memory aggregate for the response/notifier,
                    // but prevent a later SaveChanges in this scope from
                    // attempting to insert the same offer a second time.
                    var trackedOffer = database.Entry(offer);
                    if (trackedOffer.State != EntityState.Detached)
                    {
                        trackedOffer.State = EntityState.Detached;
                    }
                }
                else
                {
                    await database.RideDriverOffers.AddAsync(offer, cancellationToken);
                    await database.SaveChangesAsync(cancellationToken);
                }

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                completed = true;
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                logger.LogWarning(
                    exception,
                    "Duplicate driver offer prevented by database constraint. RequestId={RequestId}; DriverProfileId={DriverProfileId}; SearchRound={SearchRound}",
                    offer.RideRequestId,
                    offer.DriverProfileId,
                    offer.SearchRound);
                await RollbackAndDisposeAsync();
                throw new ConcurrentUpdateException("La oferta ya fue registrada por otra solicitud.", exception);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                var entries = string.Join(
                    ", ",
                    exception.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}"));
                logger.LogError(
                    exception,
                    "EF concurrency conflict while saving driver offer. RequestId={RequestId}; DriverProfileId={DriverProfileId}; Entries={Entries}; Provider={Provider}",
                    offer.RideRequestId,
                    offer.DriverProfileId,
                    entries,
                    database.Database.ProviderName);
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
