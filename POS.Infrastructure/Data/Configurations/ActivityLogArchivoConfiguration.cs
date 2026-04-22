using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ActivityLogArchivoConfiguration : IEntityTypeConfiguration<ActivityLogArchivo>
{
    public void Configure(EntityTypeBuilder<ActivityLogArchivo> builder)
    {
        builder.ToTable("activity_logs_archivo");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.UsuarioEmail).HasColumnName("usuario_email").HasMaxLength(255).IsRequired();
        builder.Property(a => a.UsuarioId).HasColumnName("usuario_id");
        builder.Property(a => a.FechaHora).HasColumnName("fecha_hora").IsRequired();
        builder.Property(a => a.Accion).HasColumnName("accion").HasMaxLength(100).IsRequired();
        builder.Property(a => a.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(a => a.SucursalId).HasColumnName("sucursal_id");
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
        builder.Property(a => a.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
        builder.Property(a => a.TipoEntidad).HasColumnName("tipo_entidad").HasMaxLength(100);
        builder.Property(a => a.EntidadId).HasColumnName("entidad_id").HasMaxLength(50);
        builder.Property(a => a.EntidadNombre).HasColumnName("entidad_nombre").HasMaxLength(255);
        builder.Property(a => a.Descripcion).HasColumnName("descripcion").HasMaxLength(1000);
        builder.Property(a => a.DatosAnteriores).HasColumnName("datos_anteriores").HasColumnType("jsonb");
        builder.Property(a => a.DatosNuevos).HasColumnName("datos_nuevos").HasColumnType("jsonb");
        builder.Property(a => a.Metadatos).HasColumnName("metadatos").HasColumnType("jsonb");
        builder.Property(a => a.Exitosa).HasColumnName("exitosa").HasDefaultValue(true).IsRequired();
        builder.Property(a => a.MensajeError).HasColumnName("mensaje_error").HasMaxLength(1000);
        builder.Property(a => a.FechaArchivado).HasColumnName("fecha_archivado").IsRequired();

        // Sin FK: las entidades referenciadas pueden no existir en el momento del archivado

        // Índices para consultas históricas
        builder.HasIndex(a => a.FechaHora).HasDatabaseName("idx_archivo_fecha");
        builder.HasIndex(a => a.FechaArchivado).HasDatabaseName("idx_archivo_fecha_archivado");
        builder.HasIndex(a => new { a.TipoEntidad, a.EntidadId }).HasDatabaseName("idx_archivo_entidad");
        builder.HasIndex(a => a.UsuarioEmail).HasDatabaseName("idx_archivo_usuario");
    }
}
