using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class PrecioSucursalConfiguration : IEntityTypeConfiguration<PrecioSucursal>
{
    public void Configure(EntityTypeBuilder<PrecioSucursal> builder)
    {
        builder.ToTable("precios_sucursal");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).UseIdentityAlwaysColumn();

        builder.Property(p => p.ProductoId).HasColumnName("producto_id");
        builder.Property(p => p.SucursalId).HasColumnName("sucursal_id");

        builder.Property(p => p.PrecioVenta)
            .HasPrecision(18, 2)
            .HasColumnName("precio_venta");

        builder.Property(p => p.PrecioMinimo)
            .HasPrecision(18, 2)
            .HasColumnName("precio_minimo");

        builder.HasIndex(p => new { p.ProductoId, p.SucursalId })
            .IsUnique()
            .HasDatabaseName("ix_precios_sucursal_producto_sucursal");

        builder.HasOne(p => p.Producto)
            .WithMany()
            .HasForeignKey(p => p.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Sucursal)
            .WithMany()
            .HasForeignKey(p => p.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
