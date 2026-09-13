using System.Security.Claims;
using Hagale.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hagale.Api.Realtime;

// El hub no recibe comandos de negocio. Solo autentica la conexión y la
// asocia a una cuenta para que el servidor pueda enviar avisos privados.
[Authorize]
public sealed class RideEventsHub : Hub
{
    public const string HubPath = "/hubs/rides";
    public const string RideChangedEvent = "rideChanged";
    public const string DispatchChangedEvent = "dispatchChanged";
    public const string DriverLocationChangedEvent = "driverLocationChanged";
    public const string DriverApplicationChangedEvent = "driverApplicationChanged";

    public override async Task OnConnectedAsync()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdValue, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        if (Context.User?.IsInRole(HagaleRoles.Driver) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DriversGroup);
        }

        if (Context.User?.IsInRole(HagaleRoles.Administrator) == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdministratorsGroup);
        }

        await base.OnConnectedAsync();
    }

    public static string UserGroup(Guid userId) => $"user:{userId:N}";
    public const string DriversGroup = "drivers";
    public const string AdministratorsGroup = "administrators";
}
