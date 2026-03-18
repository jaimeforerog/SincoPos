using Microsoft.EntityFrameworkCore;
using POS.Api.Extensions;
using POS.Application.Services;
using POS.Infrastructure.Data;

namespace POS.Api.Middleware;

/// <summary>
/// Resuelve el EmpresaId del usuario autenticado al inicio de cada request.
/// Consulta la primera empresa activa que posee alguna de las sucursales del usuario.
/// Si no hay empresa asignada, ICurrentEmpresaProvider.EmpresaId queda en null
/// y los filtros globales se omiten (backward-compatible con tests y seed).
/// </summary>
public class EmpresaContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EmpresaContextMiddleware> _logger;

    public EmpresaContextMiddleware(RequestDelegate next, ILogger<EmpresaContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ICurrentEmpresaProvider empresaProvider)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var externalId = context.User.GetExternalId();
            if (!string.IsNullOrEmpty(externalId))
            {
                try
                {
                    // Raw SQL to avoid EF Core navigation/filter issues with EmpresaId column
                    var connection = db.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                        await connection.OpenAsync();

                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT s.""EmpresaId""
                        FROM public.usuario_sucursales us
                        JOIN public.usuarios u ON us.usuario_id = u.id
                        JOIN public.sucursales s ON us.sucursal_id = s.""Id""
                        WHERE u.keycloak_id = @externalId AND s.""EmpresaId"" IS NOT NULL
                        LIMIT 1";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@externalId";
                    param.Value = externalId;
                    cmd.Parameters.Add(param);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        empresaProvider.EmpresaId = Convert.ToInt32(result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "EmpresaContextMiddleware: no se pudo resolver empresa para {ExternalId}", externalId);
                    // empresaProvider.EmpresaId stays null → filters bypassed (backward-compatible)
                }
            }
        }

        await _next(context);
    }
}
