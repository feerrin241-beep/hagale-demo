using Hagale.Application.Rides;
using Hagale.Domain.Rides;
using Microsoft.AspNetCore.SignalR;

namespace Hagale.Api.Realtime;

// SignalR entrega únicamente identificadores y tipos de cambio. Cada cliente
// vuelve a consultar su endpoint autorizado, evitando exponer datos de viaje
// a conexiones que no correspondan a esa cuenta.
public sealed class SignalRRideRealtimeNotifier(
    IHubContext<RideEventsHub> hubContext,
    ILogger<SignalRRideRealtimeNotifier> logger) : IRideRealtimeNotifier
{
    public async Task NotifyRideChangedAsync(
        Guid customerUserId,
        Guid? driverUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var recipientGroups = new List<string> { RideEventsHub.UserGroup(customerUserId) };
        if (driverUserId.HasValue)
        {
            recipientGroups.Add(RideEventsHub.UserGroup(driverUserId.Value));
        }

        await PublishSafelyAsync(
            RideEventsHub.RideChangedEvent,
            rideRequestId,
            recipientGroups,
            cancellationToken);
    }

    public Task NotifyDispatchChangedAsync(
        string operatingCityCode,
        RideServiceType serviceType,
        CancellationToken cancellationToken = default) =>
        // El aviso no contiene solicitud, ubicación, ciudad ni tipo de servicio.
        // Cada conductor autorizado actualiza su propia bandeja filtrada por el API.
        PublishSafelyAsync(
            RideEventsHub.DispatchChangedEvent,
            new { },
            [RideEventsHub.DriversGroup],
            cancellationToken);

    public Task NotifyDriverLocationChangedAsync(
        Guid customerUserId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default) =>
        PublishSafelyAsync(
            RideEventsHub.DriverLocationChangedEvent,
            rideRequestId,
            [RideEventsHub.UserGroup(customerUserId)],
            cancellationToken);

    private async Task PublishSafelyAsync(
        string eventName,
        object payload,
        IReadOnlyCollection<string> groups,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.Groups(groups).SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // El cambio ya fue guardado en base de datos; el polling existente
            // permite que el cliente se recupere de una falla transitoria.
            logger.LogWarning(exception, "No se pudo emitir el evento en tiempo real {EventName}.", eventName);
        }
    }
}
