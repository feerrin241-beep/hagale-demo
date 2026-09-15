using Hagale.Domain.Rides;

namespace Hagale.Application.Rides;

// Puerto de salida para avisos efímeros. La regla de negocio no depende de
// SignalR ni de una tecnología concreta de transporte.
public interface IRideRealtimeNotifier
{
    Task NotifyRideChangedAsync(
        Guid customerUserId,
        Guid? driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default);

    Task NotifyDispatchChangedAsync(
        string operatingCityCode,
        RideServiceType serviceType,
        CancellationToken cancellationToken = default);

    Task NotifyDriverLocationChangedAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default);

    Task NotifyRideChatMessageAsync(
        Guid customerUserId,
        Guid? driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default);
}

// Las pruebas y otros hosts pueden usar el servicio sin infraestructura de
// tiempo real. Las operaciones persistidas nunca dependen de este aviso.
public sealed class NullRideRealtimeNotifier : IRideRealtimeNotifier
{
    public static NullRideRealtimeNotifier Instance { get; } = new();

    private NullRideRealtimeNotifier()
    {
    }

    public Task NotifyRideChangedAsync(
        Guid customerUserId,
        Guid? driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyDispatchChangedAsync(
        string operatingCityCode,
        RideServiceType serviceType,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyDriverLocationChangedAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyRideChatMessageAsync(
        Guid customerUserId,
        Guid? driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
