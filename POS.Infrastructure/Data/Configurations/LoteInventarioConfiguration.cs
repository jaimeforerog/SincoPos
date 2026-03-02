using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class LoteInventarioConfiguration : IEntityTypeConfiguration<LoteInventario>
{
    public void Configure(EntityTypeBuilder<LoteInventario> builder)
    {
        builder.ToTable("lotes_inventario");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).UseIdentityAlwaysColumn();

        builder.Property(l => l.ProductoId)
            .IsRequired()
            .HasColumnName("producto_id");

        builder.Property(l => l.SucursalId)
            .IsRequired()
            .HasColumnName("sucursal_id");

        builder.Property(l => l.CantidadInicial)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad_inicial");

        builder.Property(l => l.CantidadDisponible)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad_disponible");

        builder.Property(l => l.CostoUnitario)
            .HasPrecision(18, 4)
            .HasColumnName("costo_unitario");

        builder.Property(l => l.PorcentajeImpuesto)
            .HasPrecision(5, 4)
            .HasColumnName("porcentaje_impuesto");

        builder.Property(l => l.MontoImpuestoUnitario)
            .HasPrecision(18, 4)
            .HasColumnName("monto_impuesto_unitario");

        builder.Property(l => l.Referencia)
            .HasMaxLength(100)
            .HasColumnName("referencia");

        builder.Property(l => l.TerceroId)
            .HasColumnName("tercero_id");

        builder.Property(l => l.FechaEntrada)
            .HasColumnName("fecha_entrada");

        // Indice para buscar lotes con disponibilidad (FIFO/LIFO)
        builder.HasIndex(l => new { l.ProductoId, l.SucursalId, l.FechaEntrada })
            .HasDatabaseName("ix_lotes_producto_sucursal_fecha");

        builder.HasOne(l => l.Producto)
            .WithMany()
            .HasForeignKey(l => l.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Sucursal)
            .WithMany()
            .HasForeignKey(l => l.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Tercero)
            .WithMany()
            .HasForeignKey(l => l.TerceroId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
