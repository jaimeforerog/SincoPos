using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class UsuarioSucursalConfiguration : IEntityTypeConfiguration<UsuarioSucursal>
{
    public void Configure(EntityTypeBuilder<UsuarioSucursal> builder)
    {
        builder.ToTable("usuario_sucursales");

        builder.HasKey(us => new { us.UsuarioId, us.SucursalId });

        builder.Property(us => us.UsuarioId)
            .HasColumnName("usuario_id");

        builder.Property(us => us.SucursalId)
            .HasColumnName("sucursal_id");

        builder.Property(us => us.FechaAsignacion)
            .HasColumnName("fecha_asignacion")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(us => us.SucursalId)
            .HasDatabaseName("ix_usuario_sucursales_sucursal_id");

        builder.HasOne(us => us.Usuario)
            .WithMany(u => u.Sucursales)
            .HasForeignKey(us => us.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(us => us.Sucursal)
            .WithMany()
            .HasForeignKey(us => us.SucursalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
