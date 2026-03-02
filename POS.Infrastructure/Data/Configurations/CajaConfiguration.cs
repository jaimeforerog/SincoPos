using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class CajaConfiguration : IEntityTypeConfiguration<Caja>
{
    public void Configure(EntityTypeBuilder<Caja> builder)
    {
        builder.ToTable("cajas");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("nombre");

        builder.Property(c => c.SucursalId)
            .IsRequired()
            .HasColumnName("sucursal_id");

        builder.Property(c => c.Estado)
            .HasColumnName("estado")
            .HasConversion<int>();

        builder.Property(c => c.MontoApertura)
            .HasPrecision(18, 2)
            .HasColumnName("monto_apertura");

        builder.Property(c => c.MontoActual)
            .HasPrecision(18, 2)
            .HasColumnName("monto_actual");

        builder.Property(c => c.FechaApertura)
            .HasColumnName("fecha_apertura");

        builder.Property(c => c.FechaCierre)
            .HasColumnName("fecha_cierre");

        builder.Property(c => c.AbiertaPorUsuarioId)
            .HasColumnName("abierta_por_usuario_id");

        builder.Property(c => c.Activo)
            .HasColumnName("activo");

        builder.Property(c => c.FechaCreacion)
            .HasColumnName("fecha_creacion");

        builder.HasOne(c => c.Sucursal)
            .WithMany()
            .HasForeignKey(c => c.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indice unico: nombre + sucursal
        builder.HasIndex(c => new { c.SucursalId, c.Nombre })
            .IsUnique()
            .HasDatabaseName("ix_cajas_sucursal_nombre");
    }
}
