using Microsoft.AspNetCore.SignalR;
using POS.Api.Hubs;
using POS.Application.Services;

namespace POS.Api.Services;

public class NotificationService(IHubContext<NotificationHub> hub) : INotificationService
{
    public Task EnviarNotificacionSucursalAsync(int sucursalId, NotificacionDto dto) =>
        hub.Clients.Group($"sucursal-{sucursalId}").SendAsync("Notificacion", dto);
}
