using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace POS.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public Task JoinSucursal(int sucursalId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"sucursal-{sucursalId}");

    public Task LeaveSucursal(int sucursalId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"sucursal-{sucursalId}");
}
