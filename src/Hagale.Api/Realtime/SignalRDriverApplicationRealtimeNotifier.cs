using Hagale.Application.Drivers;
using Microsoft.AspNetCore.SignalR;

namespace Hagale.Api.Realtime;

// Solo emite una señal a la persona titular y al equipo administrativo. Los
// clientes no reciben identificaciones, documentos ni resultados de revisión
// dentro del evento; consultan después la información que ya pueden ver.
public sealed class SignalRDriverApplicationRealtimeNotifier(
    IHubContext<RideEventsHub> hubContext,
    ILogger<SignalRDriverApplicationRealtimeNotifier> logger) : IDriverApplicationRealtimeNotifier
{
    public async Task NotifyChangedAsync(
        Guid applicantUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.Groups([
                    RideEventsHub.UserGroup(applicantUserId),
                    RideEventsHub.AdministratorsGroup
                ])
                .SendAsync(RideEventsHub.DriverApplicationChangedEvent, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // La modificación ya quedó persistida. La pantalla puede volver a
            // consultarla manualmente si una conexión temporal no la recibe.
            logger.LogWarning(exception, "No se pudo emitir el cambio de habilitación del conductor.");
        }
    }
}
