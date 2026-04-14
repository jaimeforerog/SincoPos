using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ConfiguracionVariableConfiguration : IEntityTypeConfiguration<ConfiguracionVariable>
{
    public void Configure(EntityTypeBuilder<ConfiguracionVariable> builder)
    {
        builder.ToTable("configuracion_variables");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("nombre");

        builder.Property(c => c.Valor)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("valor");

        builder.Property(c => c.Descripcion)
            .HasMaxLength(500)
            .HasColumnName("descripcion");

        builder.Property(c => c.Activo)
            .HasColumnName("activo");

        builder.Property(c => c.EmpresaId)
            .IsRequired()
            .HasColumnName("empresa_id");

        builder.Property(c => c.FechaCreacion)
            .HasColumnName("fecha_creacion");

        builder.Property(c => c.CreadoPor)
            .HasMaxLength(200)
            .HasColumnName("creado_por");

        builder.Property(c => c.ModificadoPor)
            .HasMaxLength(200)
            .HasColumnName("modificado_por");

        builder.Property(c => c.FechaModificacion)
            .HasColumnName("fecha_modificacion");

        builder.Property(c => c.FechaDesactivacion)
            .HasColumnName("fecha_desactivacion");

        // Nombre único por empresa (null = global)
        builder.HasIndex(c => new { c.Nombre, c.EmpresaId })
            .IsUnique()
            .HasDatabaseName("ix_configuracion_variables_nombre_empresa");

        builder.HasOne(c => c.Empresa)
            .WithMany()
            .HasForeignKey(c => c.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
