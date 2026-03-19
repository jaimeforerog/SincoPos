using Microsoft.EntityFrameworkCore;
using POS.Api.Extensions;
using POS.Application.Services;
using POS.Infrastructure.Data;

namespace POS.Api.Middleware;

/// <summary>
/// Resuelve el EmpresaId del usuario autenticado al inicio de cada request.
/// Prioridad: 1) Header X-Empresa-Id (validado contra las sucursales del usuario)
///            2) Primera empresa activa de las sucursales del usuario (fallback DB)
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
                    var connection = db.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                        await connection.OpenAsync();

                    // 1) Intentar usar el header X-Empresa-Id si el frontend lo envía
                    if (context.Request.Headers.TryGetValue("X-Empresa-Id", out var headerValue) &&
                        int.TryParse(headerValue.FirstOrDefault(), out var requestedEmpresaId))
                    {
                        using var validateCmd = connection.CreateCommand();
                        validateCmd.CommandText = @"
                            SELECT COUNT(*)
                            FROM public.usuario_sucursales us
                            JOIN public.usuarios u ON us.usuario_id = u.id
                            JOIN public.sucursales s ON us.sucursal_id = s.""Id""
                            WHERE u.keycloak_id = @externalId AND s.""EmpresaId"" = @empresaId";
                        var p1 = validateCmd.CreateParameter();
                        p1.ParameterName = "@externalId";
                        p1.Value = externalId;
                        validateCmd.Parameters.Add(p1);
                        var p2 = validateCmd.CreateParameter();
                        p2.ParameterName = "@empresaId";
                        p2.Value = requestedEmpresaId;
                        validateCmd.Parameters.Add(p2);

                        var count = Convert.ToInt64(await validateCmd.ExecuteScalarAsync() ?? 0L);
                        if (count > 0)
                        {
                            empresaProvider.EmpresaId = requestedEmpresaId;
                            await _next(context);
                            return;
                        }

                        _logger.LogWarning(
                            "EmpresaContextMiddleware: usuario {ExternalId} no tiene acceso a empresa {EmpresaId}. Usando fallback.",
                            externalId, requestedEmpresaId);
                    }

                    // 2) Fallback: primera empresa activa de las sucursales del usuario
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
                    _logger.LogWarning(ex,
                        "EmpresaContextMiddleware: no se pudo resolver empresa para {ExternalId}", externalId);
                }
            }
        }

        await _next(context);
    }
}
