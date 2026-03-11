using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class VentaConfiguration : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> builder)
    {
        builder.ToTable("ventas");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).UseIdentityAlwaysColumn();

        builder.Property(v => v.NumeroVenta)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("numero_venta");

        builder.HasIndex(v => v.NumeroVenta)
            .IsUnique()
            .HasDatabaseName("ix_ventas_numero");

        builder.Property(v => v.SucursalId).HasColumnName("sucursal_id");
        builder.Property(v => v.CajaId).HasColumnName("caja_id");
        builder.Property(v => v.ClienteId).HasColumnName("cliente_id");
        builder.Property(v => v.UsuarioId).HasColumnName("usuario_id");

        builder.Property(v => v.Subtotal).HasPrecision(18, 2).HasColumnName("subtotal");
        builder.Property(v => v.Descuento).HasPrecision(18, 2).HasColumnName("descuento");
        builder.Property(v => v.Impuestos).HasPrecision(18, 2).HasColumnName("impuestos");
        builder.Property(v => v.Total).HasPrecision(18, 2).HasColumnName("total");

        builder.Property(v => v.Estado).HasColumnName("estado");
        builder.Property(v => v.MetodoPago).HasColumnName("metodo_pago");

        builder.Property(v => v.MontoPagado).HasPrecision(18, 2).HasColumnName("monto_pagado");
        builder.Property(v => v.Cambio).HasPrecision(18, 2).HasColumnName("cambio");
        builder.Property(v => v.Observaciones).HasMaxLength(500).HasColumnName("observaciones");
        builder.Property(v => v.FechaVenta).HasColumnName("fecha_venta");

        builder.HasIndex(v => v.FechaVenta).HasDatabaseName("ix_ventas_fecha");
        builder.HasIndex(v => new { v.SucursalId, v.FechaVenta }).HasDatabaseName("ix_ventas_sucursal_fecha");
        builder.HasIndex(v => v.Estado).HasDatabaseName("ix_ventas_estado");
        builder.HasIndex(v => v.ClienteId)
            .HasDatabaseName("ix_ventas_cliente_id")
            .HasFilter("cliente_id IS NOT NULL");

        builder.HasOne(v => v.Sucursal)
            .WithMany()
            .HasForeignKey(v => v.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Caja)
            .WithMany()
            .HasForeignKey(v => v.CajaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Cliente)
            .WithMany()
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class DetalleVentaConfiguration : IEntityTypeConfiguration<DetalleVenta>
{
    public void Configure(EntityTypeBuilder<DetalleVenta> builder)
    {
        builder.ToTable("detalle_ventas");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).UseIdentityAlwaysColumn();

        builder.Property(d => d.VentaId).HasColumnName("venta_id");
        builder.Property(d => d.ProductoId).HasColumnName("producto_id");

        builder.Property(d => d.NombreProducto)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("nombre_producto");

        builder.Property(d => d.LoteInventarioId)
            .HasColumnName("lote_inventario_id");

        builder.Property(d => d.NumeroLote)
            .HasMaxLength(100)
            .HasColumnName("numero_lote");

        builder.Property(d => d.Cantidad).HasPrecision(18, 2).HasColumnName("cantidad");
        builder.Property(d => d.PrecioUnitario).HasPrecision(18, 2).HasColumnName("precio_unitario");
        builder.Property(d => d.CostoUnitario).HasPrecision(18, 2).HasColumnName("costo_unitario");
        builder.Property(d => d.Descuento).HasPrecision(18, 2).HasColumnName("descuento");
        builder.Property(d => d.PorcentajeImpuesto).HasPrecision(5, 4).HasColumnName("porcentaje_impuesto");
        builder.Property(d => d.MontoImpuesto).HasPrecision(18, 2).HasColumnName("monto_impuesto");
        builder.Property(d => d.Subtotal).HasPrecision(18, 2).HasColumnName("subtotal");

        builder.HasIndex(d => d.VentaId).HasDatabaseName("ix_detalle_ventas_venta_id");
        builder.HasIndex(d => d.ProductoId).HasDatabaseName("ix_detalle_ventas_producto_id");

        builder.HasOne(d => d.Venta)
            .WithMany(v => v.Detalles)
            .HasForeignKey(d => d.VentaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Producto)
            .WithMany()
            .HasForeignKey(d => d.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DevolucionVentaConfiguration : IEntityTypeConfiguration<DevolucionVenta>
{
    public void Configure(EntityTypeBuilder<DevolucionVenta> builder)
    {
        builder.ToTable("devoluciones_venta");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).UseIdentityAlwaysColumn();

        builder.Property(d => d.VentaId).HasColumnName("venta_id");

        builder.Property(d => d.NumeroDevolucion)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("numero_devolucion");

        builder.HasIndex(d => d.NumeroDevolucion)
            .IsUnique()
            .HasDatabaseName("ix_devoluciones_numero");

        builder.Property(d => d.Motivo)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("motivo");

        builder.Property(d => d.TotalDevuelto)
            .HasPrecision(18, 2)
            .HasColumnName("total_devuelto");

        builder.Property(d => d.FechaDevolucion)
            .HasColumnName("fecha_devolucion");

        builder.Property(d => d.AutorizadoPorUsuarioId)
            .HasColumnName("autorizado_por_usuario_id");

        builder.HasIndex(d => d.FechaDevolucion)
            .HasDatabaseName("ix_devoluciones_fecha");

        builder.HasIndex(d => new { d.VentaId, d.FechaDevolucion })
            .HasDatabaseName("ix_devoluciones_venta_fecha");

        builder.HasOne(d => d.Venta)
            .WithMany()
            .HasForeignKey(d => d.VentaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Detalles)
            .WithOne(dd => dd.DevolucionVenta)
            .HasForeignKey(dd => dd.DevolucionVentaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DetalleDevolucionConfiguration : IEntityTypeConfiguration<DetalleDevolucion>
{
    public void Configure(EntityTypeBuilder<DetalleDevolucion> builder)
    {
        builder.ToTable("detalle_devolucion");
        builder.HasKey(dd => dd.Id);
        builder.Property(dd => dd.Id).UseIdentityAlwaysColumn();

        builder.Property(dd => dd.DevolucionVentaId)
            .HasColumnName("devolucion_venta_id");

        builder.Property(dd => dd.ProductoId)
            .HasColumnName("producto_id");

        builder.Property(dd => dd.NombreProducto)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("nombre_producto");

        builder.Property(dd => dd.CantidadDevuelta)
            .HasPrecision(18, 3)
            .HasColumnName("cantidad_devuelta");

        builder.Property(dd => dd.PrecioUnitario)
            .HasPrecision(18, 2)
            .HasColumnName("precio_unitario");

        builder.Property(dd => dd.CostoUnitario)
            .HasPrecision(18, 2)
            .HasColumnName("costo_unitario");

        builder.Property(dd => dd.SubtotalDevuelto)
            .HasPrecision(18, 2)
            .HasColumnName("subtotal_devuelto");

        builder.Property(dd => dd.LoteInventarioId)
            .HasColumnName("lote_inventario_id");

        builder.Property(dd => dd.NumeroLote)
            .HasMaxLength(100)
            .HasColumnName("numero_lote");

        builder.HasOne(dd => dd.Producto)
            .WithMany()
            .HasForeignKey(dd => dd.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
