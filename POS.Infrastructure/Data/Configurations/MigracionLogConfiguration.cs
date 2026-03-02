using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class MigracionLogConfiguration : IEntityTypeConfiguration<MigracionLog>
{
    public void Configure(EntityTypeBuilder<MigracionLog> builder)
    {
        builder.ToTable("migraciones_log");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MigracionId)
            .IsRequired()
            .HasMaxLength(150)
            .HasColumnName("migracion_id");

        builder.Property(m => m.Descripcion)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("descripcion");

        builder.Property(m => m.ProductVersion)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("product_version");

        builder.Property(m => m.FechaAplicacion)
            .IsRequired()
            .HasColumnName("fecha_aplicacion");

        builder.Property(m => m.AplicadoPor)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnName("aplicado_por");

        builder.Property(m => m.Estado)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("estado");

        builder.Property(m => m.DuracionMs)
            .HasColumnName("duracion_ms");

        builder.Property(m => m.Notas)
            .HasColumnName("notas");

        builder.Property(m => m.SqlEjecutado)
            .HasColumnName("sql_ejecutado");

        // Índice para búsquedas rápidas por migración
        builder.HasIndex(m => m.MigracionId)
            .HasDatabaseName("IX_migraciones_log_migracion_id");

        // Índice para ordenar por fecha
        builder.HasIndex(m => m.FechaAplicacion)
            .HasDatabaseName("IX_migraciones_log_fecha_aplicacion");
    }
}
