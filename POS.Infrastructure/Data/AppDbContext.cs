using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Infrastructure.Data.Entities;
using POS.Domain;
using System.Security.Claims;
using System.Linq.Expressions;

namespace POS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ICurrentEmpresaProvider? _empresaProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IHttpContextAccessor httpContextAccessor,
        ICurrentEmpresaProvider empresaProvider) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _empresaProvider = empresaProvider;
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<Tercero> Terceros => Set<Tercero>();
    public DbSet<TerceroActividad> TerceroActividades => Set<TerceroActividad>();
    public DbSet<Stock> Stock => Set<Stock>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<LoteInventario> LotesInventario => Set<LoteInventario>();
    public DbSet<PrecioSucursal> PreciosSucursal => Set<PrecioSucursal>();
    public DbSet<Impuesto> Impuestos => Set<Impuesto>();
    public DbSet<RetencionRegla> RetencionesReglas => Set<RetencionRegla>();
    public DbSet<TramoBebidasAzucaradas> TramosBebidasAzucaradas => Set<TramoBebidasAzucaradas>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetalleVentas => Set<DetalleVenta>();
    public DbSet<DetalleVentaLote> DetalleVentaLotes => Set<DetalleVentaLote>();
    public DbSet<DevolucionVenta> DevolucionesVenta => Set<DevolucionVenta>();
    public DbSet<DetalleDevolucion> DetallesDevolucion => Set<DetalleDevolucion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<ActivityLogArchivo> ActivityLogsArchivo => Set<ActivityLogArchivo>();
    public DbSet<Traslado> Traslados => Set<Traslado>();
    public DbSet<DetalleTraslado> DetallesTraslado => Set<DetalleTraslado>();
    public DbSet<OrdenCompra> OrdenesCompra => Set<OrdenCompra>();
    public DbSet<DetalleOrdenCompra> DetallesOrdenCompra => Set<DetalleOrdenCompra>();
    public DbSet<DevolucionCompra> DevolucionesCompra => Set<DevolucionCompra>();
    public DbSet<DetalleDevolucionCompra> DetallesDevolucionCompra => Set<DetalleDevolucionCompra>();
    public DbSet<MigracionLog> MigracionesLog => Set<MigracionLog>();
    public DbSet<UsuarioSucursal> UsuarioSucursales => Set<UsuarioSucursal>();
    public DbSet<ConceptoRetencion> ConceptosRetencion => Set<ConceptoRetencion>();
    public DbSet<ConfiguracionEmisor> ConfiguracionesEmisor => Set<ConfiguracionEmisor>();
    public DbSet<DocumentoElectronico> DocumentosElectronicos => Set<DocumentoElectronico>();
    public DbSet<ErpOutboxMessage> ErpOutboxMessages => Set<ErpOutboxMessage>();
    public DbSet<DocumentoContable> DocumentosContables => Set<DocumentoContable>();
    public DbSet<DetalleDocumentoContable> DetallesDocumentoContable => Set<DetalleDocumentoContable>();
    public DbSet<ReglaEtica> ReglasEticas => Set<ReglaEtica>();
    public DbSet<ActivacionReglaEtica> ActivacionesReglaEtica => Set<ActivacionReglaEtica>();
    public DbSet<ConfiguracionVariable> ConfiguracionesVariables => Set<ConfiguracionVariable>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Aplicar filtro global de Soft Delete
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                // Solo si la propiedad Activo está mapeada (no ignorada)
                var property = entityType.FindProperty(nameof(ISoftDelete.Activo));
                if (property != null)
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var body = Expression.Equal(
                        Expression.Property(parameter, nameof(ISoftDelete.Activo)),
                        Expression.Constant(true));
                    var lambda = Expression.Lambda(body, parameter);

                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }
        }

        // ── Filtros globales multi-empresa ──────────────────────────────────────
        // Cuando EmpresaId == null en el proveedor (tests, background services, seed),
        // el filtro pasa todo. Cuando está seteado, filtra por empresa o por registros
        // sin empresa asignada (EmpresaId = null = catálogo global/legado).
        modelBuilder.Entity<Producto>().HasQueryFilter(p =>
            _empresaProvider == null ||
            _empresaProvider.EmpresaId == null ||
            p.EmpresaId == _empresaProvider.EmpresaId);

        modelBuilder.Entity<Categoria>().HasQueryFilter(c =>
            _empresaProvider == null ||
            _empresaProvider.EmpresaId == null ||
            c.EmpresaId == _empresaProvider.EmpresaId);

        modelBuilder.Entity<Tercero>().HasQueryFilter(t =>
            _empresaProvider == null ||
            _empresaProvider.EmpresaId == null ||
            t.EmpresaId == _empresaProvider.EmpresaId);

        // Entidades transaccionales: filtro estricto por empresa.
        // EmpresaId == null en el registro NO se comparte entre empresas —
        // solo es visible cuando no hay contexto de empresa (tests, background services).
        modelBuilder.Entity<Venta>().HasQueryFilter(v =>
            v.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             v.EmpresaId == _empresaProvider.EmpresaId));

        modelBuilder.Entity<DevolucionVenta>().HasQueryFilter(d =>
            d.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             d.EmpresaId == _empresaProvider.EmpresaId));

        modelBuilder.Entity<Caja>().HasQueryFilter(c =>
            c.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             c.EmpresaId == _empresaProvider.EmpresaId));

        modelBuilder.Entity<OrdenCompra>().HasQueryFilter(o =>
            o.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             o.EmpresaId == _empresaProvider.EmpresaId));

        modelBuilder.Entity<Traslado>().HasQueryFilter(t =>
            t.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             t.EmpresaId == _empresaProvider.EmpresaId));

        modelBuilder.Entity<DocumentoElectronico>().HasQueryFilter(d =>
            d.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             d.EmpresaId == _empresaProvider.EmpresaId));

        // Sucursal: filtro estricto por empresa (igual que entidades transaccionales)
        modelBuilder.Entity<Sucursal>().HasQueryFilter(s =>
            s.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             s.EmpresaId == _empresaProvider.EmpresaId));

        // Variables de configuración: siempre por empresa
        modelBuilder.Entity<ConfiguracionVariable>().HasQueryFilter(c =>
            c.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             c.EmpresaId == _empresaProvider.EmpresaId));

        // Tax Engine: impuestos, retenciones y conceptos de retención por empresa.
        // EmpresaId es siempre requerido (NOT NULL); cada empresa tiene sus propios registros.
        modelBuilder.Entity<Impuesto>().HasQueryFilter(i =>
            i.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             i.EmpresaId == _empresaProvider.EmpresaId));

        modelBuilder.Entity<RetencionRegla>().HasQueryFilter(r =>
            r.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             r.EmpresaId == _empresaProvider.EmpresaId));

        modelBuilder.Entity<ConceptoRetencion>().HasQueryFilter(c =>
            c.Activo &&
            (_empresaProvider == null ||
             _empresaProvider.EmpresaId == null ||
             c.EmpresaId == _empresaProvider.EmpresaId));
    }

    /// <summary>
    /// Sobrescribe SaveChangesAsync para capturar automáticamente información de auditoría
    /// en todas las entidades que heredan de EntidadAuditable.
    /// </summary>
    public override int SaveChanges()
    {
        CapturarInformacionAuditoria();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CapturarInformacionAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

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

        // 0. Procesar Soft Delete (cuando el estado de Activo cambia a false)
        var entriesSoftDelete = ChangeTracker.Entries<ISoftDelete>()
            .Where(e => e.State == EntityState.Modified && e.OriginalValues.GetValue<bool>(nameof(ISoftDelete.Activo)) 
                        && !e.CurrentValues.GetValue<bool>(nameof(ISoftDelete.Activo)));

        foreach (var entry in entriesSoftDelete)
        {
            entry.Entity.FechaDesactivacion = ahora;
        }

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
