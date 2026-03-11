using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class SucursalConfiguration : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> builder)
    {
        builder.ToTable("sucursales");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).UseIdentityAlwaysColumn();

        builder.Property(s => s.Nombre)
            .IsRequired()
            .HasMaxLength(150)
            .HasColumnName("nombre");

        builder.HasIndex(s => s.Nombre)
            .IsUnique()
            .HasDatabaseName("ix_sucursales_nombre");

        builder.Property(s => s.Direccion)
            .HasMaxLength(300)
            .HasColumnName("direccion");

        builder.Property(s => s.Ciudad)
            .HasMaxLength(100)
            .HasColumnName("ciudad");

        builder.Property(s => s.Telefono)
            .HasMaxLength(20)
            .HasColumnName("telefono");

        builder.Property(s => s.Email)
            .HasMaxLength(150)
            .HasColumnName("email");

        builder.Property(s => s.Activo)
            .HasColumnName("activo");

        builder.Property(s => s.MetodoCosteo)
            .HasColumnName("metodo_costeo")
            .HasConversion<int>()
            .HasDefaultValue(MetodoCosteo.PromedioPonderado);

        builder.Property(s => s.FechaCreacion)
            .HasColumnName("fecha_creacion");

        builder.Property(s => s.DiasAlertaVencimientoLotes)
            .HasDefaultValue(30)
            .HasColumnName("dias_alerta_vencimiento_lotes");
    }
}
