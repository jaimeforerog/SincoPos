using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ErpOutboxMessageConfiguration : IEntityTypeConfiguration<ErpOutboxMessage>
{
    public void Configure(EntityTypeBuilder<ErpOutboxMessage> builder)
    {
        builder.ToTable("erp_outbox_messages");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityAlwaysColumn();

        builder.Property(e => e.TipoDocumento)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("tipo_documento");

        builder.Property(e => e.EntidadId)
            .IsRequired()
            .HasColumnName("entidad_id");

        builder.Property(e => e.Payload)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("payload");

        builder.Property(e => e.FechaCreacion)
            .IsRequired()
            .HasColumnName("fecha_creacion");

        builder.Property(e => e.FechaProcesamiento)
            .HasColumnName("fecha_procesamiento");

        builder.Property(e => e.Intentos)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("intentos");

        builder.Property(e => e.UltimoError)
            .HasColumnName("ultimo_error");

        builder.Property(e => e.Estado)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(EstadoOutbox.Pendiente)
            .HasColumnName("estado");

        // Índices clave para el Background Service (Lectura rápida de pendientes)
        builder.HasIndex(e => new { e.Estado, e.FechaCreacion })
            .HasDatabaseName("ix_erp_outbox_estado_fecha");
            
        builder.HasIndex(e => new { e.TipoDocumento, e.EntidadId })
            .HasDatabaseName("ix_erp_outbox_tipo_entidad");
    }
}
