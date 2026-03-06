namespace POS.Application.Services;

public record NotificacionDto(
    string Tipo,
    string Titulo,
    string Mensaje,
    string Nivel,
    DateTime Timestamp,
    object? Datos = null
);

public interface INotificationService
{
    Task EnviarNotificacionSucursalAsync(int sucursalId, NotificacionDto notificacion);
}
