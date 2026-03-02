using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Servicio para registrar migraciones de base de datos en el log de auditoría
/// </summary>
public class MigracionLogService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MigracionLogService> _logger;

    public MigracionLogService(
        AppDbContext context,
        ILogger<MigracionLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Registra una migración aplicada en el log
    /// </summary>
    public async Task RegistrarMigracion(
        string migracionId,
        string descripcion,
        string productVersion,
        string aplicadoPor = "sistema",
        long duracionMs = 0,
        string? notas = null)
    {
        try
        {
            var log = new MigracionLog
            {
                MigracionId = migracionId,
                Descripcion = descripcion,
                ProductVersion = productVersion,
                FechaAplicacion = DateTime.UtcNow,
                AplicadoPor = aplicadoPor,
                Estado = "Success",
                DuracionMs = duracionMs,
                Notas = notas
            };

            _context.MigracionesLog.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Migración registrada: {MigracionId} - {Descripcion}",
                migracionId, descripcion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al registrar migración {MigracionId}: {Error}",
                migracionId, ex.Message);
        }
    }

    /// <summary>
    /// Registra una migración fallida
    /// </summary>
    public async Task RegistrarMigracionFallida(
        string migracionId,
        string descripcion,
        string productVersion,
        string error,
        string aplicadoPor = "sistema")
    {
        try
        {
            var log = new MigracionLog
            {
                MigracionId = migracionId,
                Descripcion = descripcion,
                ProductVersion = productVersion,
                FechaAplicacion = DateTime.UtcNow,
                AplicadoPor = aplicadoPor,
                Estado = "Failed",
                Notas = error
            };

            _context.MigracionesLog.Add(log);
            await _context.SaveChangesAsync();

            _logger.LogError(
                "Migración fallida registrada: {MigracionId} - {Error}",
                migracionId, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al registrar migración fallida {MigracionId}: {Error}",
                migracionId, ex.Message);
        }
    }

    /// <summary>
    /// Sincroniza el log con las migraciones existentes en __ef_migrations_history
    /// </summary>
    public async Task SincronizarMigracionesExistentes()
    {
        try
        {
            // Obtener migraciones de __ef_migrations_history
            var migracionesEF = await _context.Database
                .SqlQuery<EfMigrationHistory>(
                    $"SELECT \"MigrationId\", \"ProductVersion\" FROM public.__ef_migrations_history ORDER BY \"MigrationId\"")
                .ToListAsync();

            // Obtener IDs de migraciones ya registradas en el log
            var migracionesEnLog = await _context.MigracionesLog
                .Select(m => m.MigracionId)
                .ToListAsync();

            // Registrar las que faltan
            foreach (var efMigration in migracionesEF)
            {
                if (!migracionesEnLog.Contains(efMigration.MigrationId))
                {
                    var descripcion = ExtraerDescripcionDeMigracion(efMigration.MigrationId);

                    var log = new MigracionLog
                    {
                        MigracionId = efMigration.MigrationId,
                        Descripcion = descripcion,
                        ProductVersion = efMigration.ProductVersion,
                        FechaAplicacion = DateTime.UtcNow, // No sabemos la fecha real
                        AplicadoPor = "sistema",
                        Estado = "Success",
                        Notas = "Migración histórica - sincronizada automáticamente"
                    };

                    _context.MigracionesLog.Add(log);
                }
            }

            var registradas = await _context.SaveChangesAsync();
            if (registradas > 0)
            {
                _logger.LogInformation(
                    "Sincronizadas {Count} migraciones históricas",
                    registradas);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al sincronizar migraciones existentes: {Error}",
                ex.Message);
        }
    }

    /// <summary>
    /// Extrae una descripción legible del nombre de la migración
    /// Ejemplo: "20260302210242_AgregarOrigenDatoAPrecioSucursal" -> "Agregar Origen Dato A Precio Sucursal"
    /// </summary>
    private string ExtraerDescripcionDeMigracion(string migracionId)
    {
        // Eliminar el timestamp (primeros 15 caracteres)
        var nombreSinTimestamp = migracionId.Length > 15
            ? migracionId.Substring(15).TrimStart('_')
            : migracionId;

        // Separar por mayúsculas
        var palabras = System.Text.RegularExpressions.Regex
            .Replace(nombreSinTimestamp, "([A-Z])", " $1")
            .Trim();

        return palabras;
    }

    /// <summary>
    /// Obtiene el historial de migraciones
    /// </summary>
    public async Task<List<MigracionLog>> ObtenerHistorial(int limite = 50)
    {
        return await _context.MigracionesLog
            .OrderByDescending(m => m.FechaAplicacion)
            .Take(limite)
            .ToListAsync();
    }
}

/// <summary>
/// DTO para mapear resultados de __ef_migrations_history
/// </summary>
public class EfMigrationHistory
{
    public string MigrationId { get; set; } = string.Empty;
    public string ProductVersion { get; set; } = string.Empty;
}
