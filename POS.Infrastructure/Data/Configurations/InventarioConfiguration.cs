using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stock");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).UseIdentityAlwaysColumn();

        builder.Property(s => s.ProductoId)
            .IsRequired()
            .HasColumnName("producto_id");

        builder.Property(s => s.SucursalId)
            .IsRequired()
            .HasColumnName("sucursal_id");

        // Indice unico: un registro de stock por producto+sucursal
        builder.HasIndex(s => new { s.ProductoId, s.SucursalId })
            .IsUnique()
            .HasDatabaseName("ix_stock_producto_sucursal");

        builder.Property(s => s.Cantidad)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad");

        builder.Property(s => s.StockMinimo)
            .HasPrecision(18, 4)
            .HasColumnName("stock_minimo");

        builder.Property(s => s.CostoPromedio)
            .HasPrecision(18, 4)
            .HasColumnName("costo_promedio");

        builder.Property(s => s.UltimaActualizacion)
            .HasColumnName("ultima_actualizacion");

        builder.HasOne(s => s.Producto)
            .WithMany()
            .HasForeignKey(s => s.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Sucursal)
            .WithMany()
            .HasForeignKey(s => s.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MovimientoInventarioConfiguration : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> builder)
    {
        builder.ToTable("movimientos_inventario");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).UseIdentityAlwaysColumn();

        builder.Property(m => m.ProductoId)
            .IsRequired()
            .HasColumnName("producto_id");

        builder.Property(m => m.SucursalId)
            .IsRequired()
            .HasColumnName("sucursal_id");

        builder.Property(m => m.TipoMovimiento)
            .HasColumnName("tipo_movimiento")
            .HasConversion<int>();

        builder.Property(m => m.Cantidad)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad");

        builder.Property(m => m.CostoUnitario)
            .HasPrecision(18, 4)
            .HasColumnName("costo_unitario");

        builder.Property(m => m.CostoTotal)
            .HasPrecision(18, 4)
            .HasColumnName("costo_total");

        builder.Property(m => m.PorcentajeImpuesto)
            .HasPrecision(5, 4)
            .HasColumnName("porcentaje_impuesto");

        builder.Property(m => m.MontoImpuesto)
            .HasPrecision(18, 4)
            .HasColumnName("monto_impuesto");

        builder.Property(m => m.Referencia)
            .HasMaxLength(100)
            .HasColumnName("referencia");

        builder.Property(m => m.Observaciones)
            .HasMaxLength(500)
            .HasColumnName("observaciones");

        builder.Property(m => m.TerceroId)
            .HasColumnName("tercero_id");

        builder.Property(m => m.SucursalDestinoId)
            .HasColumnName("sucursal_destino_id");

        builder.Property(m => m.UsuarioId)
            .HasColumnName("usuario_id");

        builder.Property(m => m.FechaMovimiento)
            .HasColumnName("fecha_movimiento");

        // Indice para consultas frecuentes
        builder.HasIndex(m => new { m.ProductoId, m.SucursalId, m.FechaMovimiento })
            .HasDatabaseName("ix_movimientos_producto_sucursal_fecha");

        builder.HasOne(m => m.Producto)
            .WithMany()
            .HasForeignKey(m => m.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Sucursal)
            .WithMany()
            .HasForeignKey(m => m.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Tercero)
            .WithMany()
            .HasForeignKey(m => m.TerceroId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.SucursalDestino)
            .WithMany()
            .HasForeignKey(m => m.SucursalDestinoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
