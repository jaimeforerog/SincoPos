using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");

        // Primary Key
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // WHO
        builder.Property(a => a.UsuarioEmail)
            .HasColumnName("usuario_email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.UsuarioId)
            .HasColumnName("usuario_id");

        // WHEN
        builder.Property(a => a.FechaHora)
            .HasColumnName("fecha_hora")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        // WHAT
        builder.Property(a => a.Accion)
            .HasColumnName("accion")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Tipo)
            .HasColumnName("tipo")
            .HasConversion<int>()
            .IsRequired();

        // WHERE
        builder.Property(a => a.SucursalId)
            .HasColumnName("sucursal_id");

        builder.Property(a => a.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(50);

        builder.Property(a => a.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        // ENTITY CONTEXT
        builder.Property(a => a.TipoEntidad)
            .HasColumnName("tipo_entidad")
            .HasMaxLength(100);

        builder.Property(a => a.EntidadId)
            .HasColumnName("entidad_id")
            .HasMaxLength(50);

        builder.Property(a => a.EntidadNombre)
            .HasColumnName("entidad_nombre")
            .HasMaxLength(255);

        // DETAILS
        builder.Property(a => a.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(1000);

        builder.Property(a => a.DatosAnteriores)
            .HasColumnName("datos_anteriores")
            .HasColumnType("jsonb");

        builder.Property(a => a.DatosNuevos)
            .HasColumnName("datos_nuevos")
            .HasColumnType("jsonb");

        builder.Property(a => a.Metadatos)
            .HasColumnName("metadatos")
            .HasColumnType("jsonb");

        // RESULT
        builder.Property(a => a.Exitosa)
            .HasColumnName("exitosa")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(a => a.MensajeError)
            .HasColumnName("mensaje_error")
            .HasMaxLength(1000);

        // INDEXES ESTRATÉGICOS PARA PERFORMANCE

        // Búsqueda por fecha (queries temporales, reportes)
        builder.HasIndex(a => a.FechaHora)
            .HasDatabaseName("idx_activity_fecha");

        // Búsqueda por usuario
        builder.HasIndex(a => a.UsuarioEmail)
            .HasDatabaseName("idx_activity_usuario");

        // Filtrado por tipo de actividad
        builder.HasIndex(a => a.Tipo)
            .HasDatabaseName("idx_activity_tipo");

        // Historial de una entidad específica
        builder.HasIndex(a => new { a.TipoEntidad, a.EntidadId })
            .HasDatabaseName("idx_activity_entidad");

        // Dashboard queries (fecha + tipo + sucursal) - composite index
        builder.HasIndex(a => new { a.FechaHora, a.Tipo, a.SucursalId })
            .HasDatabaseName("idx_activity_dashboard");

        // RELACIONES (Foreign Keys)

        // Relación con Usuario (opcional - puede ser null si usuario fue eliminado)
        builder.HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relación con Sucursal (opcional)
        builder.HasOne(a => a.Sucursal)
            .WithMany()
            .HasForeignKey(a => a.SucursalId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
