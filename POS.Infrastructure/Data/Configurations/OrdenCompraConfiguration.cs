using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class OrdenCompraConfiguration : IEntityTypeConfiguration<OrdenCompra>
{
    public void Configure(EntityTypeBuilder<OrdenCompra> builder)
    {
        builder.ToTable("ordenes_compra");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).UseIdentityAlwaysColumn();

        builder.Property(o => o.NumeroOrden)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("numero_orden");

        builder.Property(o => o.SucursalId)
            .IsRequired()
            .HasColumnName("sucursal_id");

        builder.Property(o => o.ProveedorId)
            .IsRequired()
            .HasColumnName("proveedor_id");

        builder.Property(o => o.Estado)
            .HasColumnName("estado")
            .HasConversion<int>();

        builder.Property(o => o.FechaOrden)
            .HasColumnName("fecha_orden");

        builder.Property(o => o.FechaEntregaEsperada)
            .HasColumnName("fecha_entrega_esperada");

        builder.Property(o => o.FechaAprobacion)
            .HasColumnName("fecha_aprobacion");

        builder.Property(o => o.FechaRecepcion)
            .HasColumnName("fecha_recepcion");

        builder.Property(o => o.AprobadoPorUsuarioId)
            .HasColumnName("aprobado_por_usuario_id");

        builder.Property(o => o.RecibidoPorUsuarioId)
            .HasColumnName("recibido_por_usuario_id");

        builder.Property(o => o.Observaciones)
            .HasMaxLength(500)
            .HasColumnName("observaciones");

        builder.Property(o => o.MotivoRechazo)
            .HasMaxLength(500)
            .HasColumnName("motivo_rechazo");

        builder.Property(o => o.Subtotal)
            .HasPrecision(18, 2)
            .HasColumnName("subtotal");

        builder.Property(o => o.Impuestos)
            .HasPrecision(18, 2)
            .HasColumnName("impuestos");

        builder.Property(o => o.Total)
            .HasPrecision(18, 2)
            .HasColumnName("total");

        builder.Property(o => o.RequiereFacturaElectronica)
            .HasColumnName("requiere_factura_electronica")
            .HasDefaultValue(false);

        // Índices — número único POR SUCURSAL (no globalmente)
        builder.HasIndex(o => new { o.SucursalId, o.NumeroOrden })
            .IsUnique()
            .HasDatabaseName("ix_ordenes_compra_sucursal_numero");

        builder.HasIndex(o => o.FechaOrden)
            .HasDatabaseName("ix_ordenes_compra_fecha");

        builder.HasIndex(o => new { o.ProveedorId, o.FechaOrden })
            .HasDatabaseName("ix_ordenes_compra_proveedor_fecha");

        builder.HasIndex(o => new { o.SucursalId, o.Estado })
            .HasDatabaseName("ix_ordenes_compra_sucursal_estado");

        // Relaciones
        builder.HasOne(o => o.Sucursal)
            .WithMany()
            .HasForeignKey(o => o.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Proveedor)
            .WithMany()
            .HasForeignKey(o => o.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Detalles)
            .WithOne(d => d.OrdenCompra)
            .HasForeignKey(d => d.OrdenCompraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DetalleOrdenCompraConfiguration : IEntityTypeConfiguration<DetalleOrdenCompra>
{
    public void Configure(EntityTypeBuilder<DetalleOrdenCompra> builder)
    {
        builder.ToTable("detalle_ordenes_compra");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).UseIdentityAlwaysColumn();

        builder.Property(d => d.OrdenCompraId)
            .IsRequired()
            .HasColumnName("orden_compra_id");

        builder.Property(d => d.ProductoId)
            .IsRequired()
            .HasColumnName("producto_id");

        builder.Property(d => d.NombreProducto)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("nombre_producto");

        builder.Property(d => d.CantidadSolicitada)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad_solicitada");

        builder.Property(d => d.CantidadRecibida)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad_recibida");

        builder.Property(d => d.PrecioUnitario)
            .HasPrecision(18, 4)
            .HasColumnName("precio_unitario");

        builder.Property(d => d.PorcentajeImpuesto)
            .HasPrecision(5, 4)
            .HasColumnName("porcentaje_impuesto");

        builder.Property(d => d.MontoImpuesto)
            .HasPrecision(18, 2)
            .HasColumnName("monto_impuesto");

        builder.Property(d => d.Subtotal)
            .HasPrecision(18, 2)
            .HasColumnName("subtotal");

        builder.Property(d => d.NombreImpuesto)
            .HasMaxLength(100)
            .HasColumnName("nombre_impuesto");

        builder.Property(d => d.Observaciones)
            .HasMaxLength(300)
            .HasColumnName("observaciones");

        builder.HasOne(d => d.OrdenCompra)
            .WithMany(o => o.Detalles)
            .HasForeignKey(d => d.OrdenCompraId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Producto)
            .WithMany()
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DevolucionCompraConfiguration : IEntityTypeConfiguration<DevolucionCompra>
{
    public void Configure(EntityTypeBuilder<DevolucionCompra> builder)
    {
        builder.ToTable("devoluciones_compra");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).UseIdentityAlwaysColumn();

        builder.Property(d => d.OrdenCompraId).IsRequired().HasColumnName("orden_compra_id");
        builder.Property(d => d.NumeroDevolucion).IsRequired().HasMaxLength(20).HasColumnName("numero_devolucion");
        builder.Property(d => d.Motivo).IsRequired().HasMaxLength(500).HasColumnName("motivo");
        builder.Property(d => d.Total).HasPrecision(18, 2).HasColumnName("total");
        builder.Property(d => d.FechaDevolucion).HasColumnName("fecha_devolucion");
        builder.Property(d => d.AutorizadoPorUsuarioId).HasColumnName("autorizado_por_usuario_id");

        builder.HasIndex(d => d.NumeroDevolucion).IsUnique().HasDatabaseName("ix_devoluciones_compra_numero");

        builder.HasOne(d => d.OrdenCompra)
            .WithMany()
            .HasForeignKey(d => d.OrdenCompraId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DetalleDevolucionCompraConfiguration : IEntityTypeConfiguration<DetalleDevolucionCompra>
{
    public void Configure(EntityTypeBuilder<DetalleDevolucionCompra> builder)
    {
        builder.ToTable("detalle_devoluciones_compra");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).UseIdentityAlwaysColumn();

        builder.Property(d => d.DevolucionCompraId).IsRequired().HasColumnName("devolucion_compra_id");
        builder.Property(d => d.ProductoId).IsRequired().HasColumnName("producto_id");
        builder.Property(d => d.NombreProducto).IsRequired().HasMaxLength(200).HasColumnName("nombre_producto");
        builder.Property(d => d.CantidadDevuelta).HasPrecision(18, 4).HasColumnName("cantidad_devuelta");
        builder.Property(d => d.PrecioUnitario).HasPrecision(18, 4).HasColumnName("precio_unitario");
        builder.Property(d => d.Subtotal).HasPrecision(18, 2).HasColumnName("subtotal");

        builder.HasOne(d => d.DevolucionCompra)
            .WithMany(dc => dc.Detalles)
            .HasForeignKey(d => d.DevolucionCompraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
