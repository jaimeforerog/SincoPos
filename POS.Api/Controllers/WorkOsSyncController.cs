using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Infrastructure.Data;

namespace POS.Api.Controllers;

/// <summary>
/// Operaciones de sincronización entre la BD local y WorkOS (Organizations + Memberships).
/// Pensado para backfill cuando se incorporó la sincronización tras tener data preexistente.
/// </summary>
[Authorize(Policy = "Admin")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workos-sync")]
public sealed class WorkOsSyncController : ControllerBase
{
    private readonly IEmpresaService _empresaService;
    private readonly IUsuarioAdminService _usuarioAdminService;
    private readonly AppDbContext _context;
    private readonly ILogger<WorkOsSyncController> _logger;

    public WorkOsSyncController(
        IEmpresaService empresaService,
        IUsuarioAdminService usuarioAdminService,
        AppDbContext context,
        ILogger<WorkOsSyncController> logger)
    {
        _empresaService = empresaService;
        _usuarioAdminService = usuarioAdminService;
        _context = context;
        _logger = logger;
    }

    public sealed record SyncResultDto(
        int EmpresasSincronizadas,
        int EmpresasFallidas,
        int UsuariosSincronizados);

    /// <summary>
    /// Ejecuta el backfill completo: crea Organizations faltantes en WorkOS y
    /// sincroniza memberships de todos los usuarios.
    /// Idempotente: se puede correr varias veces sin efectos secundarios.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> Sincronizar()
    {
        var (orgsOk, orgsFail) = await _empresaService.SincronizarOrganizacionesAsync();

        var usuarioIds = await _context.Usuarios
            .IgnoreQueryFilters()
            .Where(u => u.ExternalId != "")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var id in usuarioIds)
            await _usuarioAdminService.SincronizarMembershipsAsync(id);

        _logger.LogInformation(
            "WorkOS backfill ejecutado: empresas {Ok}/{Fail}, usuarios {Users}",
            orgsOk, orgsFail, usuarioIds.Count);

        return Ok(new SyncResultDto(orgsOk, orgsFail, usuarioIds.Count));
    }
}
