using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class TerceroConfiguration : IEntityTypeConfiguration<Tercero>
{
    public void Configure(EntityTypeBuilder<Tercero> builder)
    {
        builder.ToTable("terceros");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityAlwaysColumn();

        builder.Property(t => t.TipoIdentificacion)
            .HasColumnName("tipo_identificacion")
            .HasConversion<int>();

        builder.Property(t => t.Identificacion)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("identificacion");

        builder.HasIndex(t => t.Identificacion)
            .IsUnique()
            .HasDatabaseName("ix_terceros_identificacion");

        builder.Property(t => t.Nombre)
            .IsRequired()
            .HasMaxLength(250)
            .HasColumnName("nombre");

        builder.Property(t => t.TipoTercero)
            .HasColumnName("tipo_tercero")
            .HasConversion<int>();

        builder.Property(t => t.Telefono)
            .HasMaxLength(20)
            .HasColumnName("telefono");

        builder.Property(t => t.Email)
            .HasMaxLength(150)
            .HasColumnName("email");

        builder.Property(t => t.Direccion)
            .HasMaxLength(300)
            .HasColumnName("direccion");

        builder.Property(t => t.Ciudad)
            .HasMaxLength(100)
            .HasColumnName("ciudad");

        builder.Property(t => t.OrigenDatos)
            .HasColumnName("origen_datos")
            .HasConversion<int>()
            .HasDefaultValue(OrigenDatos.Local);

        builder.Property(t => t.ExternalId)
            .HasMaxLength(100)
            .HasColumnName("external_id");

        builder.HasIndex(t => t.ExternalId)
            .HasDatabaseName("ix_terceros_external_id")
            .HasFilter("external_id IS NOT NULL");

        builder.Property(t => t.Activo)
            .HasColumnName("activo");

        builder.Property(t => t.FechaCreacion)
            .HasColumnName("fecha_creacion");

        builder.Property(t => t.FechaModificacion)
            .HasColumnName("fecha_modificacion");
    }
}
