using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class DocumentoElectronicoConfiguration : IEntityTypeConfiguration<DocumentoElectronico>
{
    public void Configure(EntityTypeBuilder<DocumentoElectronico> builder)
    {
        builder.ToTable("documentos_electronicos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).UseIdentityAlwaysColumn();

        builder.Property(d => d.VentaId).HasColumnName("venta_id");
        builder.Property(d => d.SucursalId).HasColumnName("sucursal_id");
        builder.Property(d => d.TipoDocumento).IsRequired().HasMaxLength(2).HasColumnName("tipo_documento");
        builder.Property(d => d.Prefijo).IsRequired().HasMaxLength(5).HasColumnName("prefijo");
        builder.Property(d => d.Numero).HasColumnName("numero");
        builder.Property(d => d.NumeroCompleto).IsRequired().HasMaxLength(20).HasColumnName("numero_completo");
        builder.Property(d => d.Cufe).IsRequired().HasMaxLength(200).HasColumnName("cufe");
        builder.Property(d => d.FechaEmision).HasColumnName("fecha_emision");
        builder.Property(d => d.XmlUbl).IsRequired().HasColumnName("xml_ubl");
        builder.Property(d => d.Estado).HasColumnName("estado").HasDefaultValue(EstadoDocumento.Pendiente);
        builder.Property(d => d.FechaEnvioDian).HasColumnName("fecha_envio_dian");
        builder.Property(d => d.CodigoRespuestaDian).HasMaxLength(20).HasColumnName("codigo_respuesta_dian");
        builder.Property(d => d.MensajeRespuestaDian).HasMaxLength(1000).HasColumnName("mensaje_respuesta_dian");
        builder.Property(d => d.Intentos).HasColumnName("intentos").HasDefaultValue(0);

        // Auditoría heredada
        builder.Property(d => d.CreadoPor).HasMaxLength(200).HasColumnName("creado_por");
        builder.Property(d => d.FechaCreacion).HasColumnName("fecha_creacion");
        builder.Property(d => d.ModificadoPor).HasMaxLength(200).HasColumnName("modificado_por");
        builder.Property(d => d.FechaModificacion).HasColumnName("fecha_modificacion");
        builder.Property(d => d.Activo).HasDefaultValue(true); // Participa en el filtro global de Soft Delete
        builder.Property(d => d.FechaDesactivacion).HasColumnName("fecha_desactivacion");

        builder.HasIndex(d => d.Cufe)
            .IsUnique()
            .HasDatabaseName("ix_documentos_electronicos_cufe");
        builder.HasIndex(d => d.Estado)
            .HasDatabaseName("ix_documentos_electronicos_estado");
        builder.HasIndex(d => new { d.SucursalId, d.FechaEmision })
            .HasDatabaseName("ix_documentos_electronicos_sucursal_fecha");
        builder.HasIndex(d => d.VentaId)
            .HasDatabaseName("ix_documentos_electronicos_venta_id")
            .HasFilter("venta_id IS NOT NULL");

        builder.HasOne(d => d.Venta)
            .WithMany()
            .HasForeignKey(d => d.VentaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Sucursal)
            .WithMany()
            .HasForeignKey(d => d.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
