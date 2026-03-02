using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Data.Entities;
using System.Security.Claims;

namespace POS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<Tercero> Terceros => Set<Tercero>();
    public DbSet<Stock> Stock => Set<Stock>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<LoteInventario> LotesInventario => Set<LoteInventario>();
    public DbSet<PrecioSucursal> PreciosSucursal => Set<PrecioSucursal>();
    public DbSet<Impuesto> Impuestos => Set<Impuesto>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetalleVentas => Set<DetalleVenta>();
    public DbSet<DevolucionVenta> DevolucionesVenta => Set<DevolucionVenta>();
    public DbSet<DetalleDevolucion> DetallesDevolucion => Set<DetalleDevolucion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Traslado> Traslados => Set<Traslado>();
    public DbSet<DetalleTraslado> DetallesTraslado => Set<DetalleTraslado>();
    public DbSet<OrdenCompra> OrdenesCompra => Set<OrdenCompra>();
    public DbSet<DetalleOrdenCompra> DetallesOrdenCompra => Set<DetalleOrdenCompra>();
    public DbSet<MigracionLog> MigracionesLog => Set<MigracionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>
    /// Sobrescribe SaveChangesAsync para capturar automáticamente información de auditoría
    /// en todas las entidades que heredan de EntidadAuditable.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Capturar información de auditoría antes de guardar
        CapturarInformacionAuditoria();

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Captura automáticamente CreadoPor, ModificadoPor y fechas para entidades auditables
    /// </summary>
    private void CapturarInformacionAuditoria()
    {
        // Obtener usuario actual del contexto HTTP
        var usuarioActual = ObtenerUsuarioActual();
        var ahora = DateTime.UtcNow;

        // 1. Procesar entidades que heredan de EntidadAuditable
        var entriesAuditables = ChangeTracker.Entries<EntidadAuditable>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entriesAuditables)
        {
            if (entry.State == EntityState.Added)
            {
                // Nueva entidad - registrar quien la creó
                entry.Entity.CreadoPor = usuarioActual;
                entry.Entity.FechaCreacion = ahora;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Entidad modificada - registrar quien la modificó
                entry.Entity.ModificadoPor = usuarioActual;
                entry.Entity.FechaModificacion = ahora;

                // Prevenir que se modifiquen los campos de creación
                entry.Property(e => e.CreadoPor).IsModified = false;
                entry.Property(e => e.FechaCreacion).IsModified = false;
            }
        }

        // 2. Procesar Producto (que tiene Guid Id y no hereda de EntidadAuditable)
        var entriesProducto = ChangeTracker.Entries<Producto>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entriesProducto)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreadoPor = usuarioActual;
                entry.Entity.FechaCreacion = ahora;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModificadoPor = usuarioActual;
                entry.Entity.FechaModificacion = ahora;

                // Prevenir que se modifiquen los campos de creación
                entry.Property(e => e.CreadoPor).IsModified = false;
                entry.Property(e => e.FechaCreacion).IsModified = false;
            }
        }
    }

    /// <summary>
    /// Obtiene el email del usuario autenticado actual, o "sistema" si no hay usuario
    /// </summary>
    private string ObtenerUsuarioActual()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            // Intentar obtener email del usuario
            var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value
                       ?? httpContext.User.FindFirst("email")?.Value
                       ?? httpContext.User.FindFirst(ClaimTypes.Name)?.Value
                       ?? httpContext.User.FindFirst("preferred_username")?.Value;

            return email ?? "usuario-autenticado";
        }

        // Si no hay usuario autenticado (seeds, migraciones, etc.)
        return "sistema";
    }
}
