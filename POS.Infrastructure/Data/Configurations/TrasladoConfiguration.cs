using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class TrasladoConfiguration : IEntityTypeConfiguration<Traslado>
{
    public void Configure(EntityTypeBuilder<Traslado> builder)
    {
        builder.ToTable("traslados");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityAlwaysColumn();

        builder.Property(t => t.NumeroTraslado)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("numero_traslado");

        builder.HasIndex(t => t.NumeroTraslado)
            .IsUnique()
            .HasDatabaseName("ix_traslados_numero");

        builder.Property(t => t.SucursalOrigenId)
            .IsRequired()
            .HasColumnName("sucursal_origen_id");

        builder.Property(t => t.SucursalDestinoId)
            .IsRequired()
            .HasColumnName("sucursal_destino_id");

        builder.Property(t => t.Estado)
            .HasColumnName("estado")
            .HasConversion<int>();

        builder.Property(t => t.FechaTraslado)
            .HasColumnName("fecha_traslado");

        builder.Property(t => t.FechaEnvio)
            .HasColumnName("fecha_envio");

        builder.Property(t => t.FechaRecepcion)
            .HasColumnName("fecha_recepcion");

        builder.Property(t => t.RecibidoPorUsuarioId)
            .HasColumnName("recibido_por_usuario_id");

        builder.Property(t => t.Observaciones)
            .HasMaxLength(500)
            .HasColumnName("observaciones");

        builder.Property(t => t.MotivoRechazo)
            .HasMaxLength(500)
            .HasColumnName("motivo_rechazo");

        // Índices
        builder.HasIndex(t => t.FechaTraslado)
            .HasDatabaseName("ix_traslados_fecha");

        builder.HasIndex(t => new { t.SucursalOrigenId, t.FechaTraslado })
            .HasDatabaseName("ix_traslados_origen_fecha");

        builder.HasIndex(t => new { t.SucursalOrigenId, t.Estado })
            .HasDatabaseName("ix_traslados_origen_estado");

        builder.HasIndex(t => new { t.SucursalDestinoId, t.Estado })
            .HasDatabaseName("ix_traslados_destino_estado");

        // Relaciones
        builder.HasOne(t => t.SucursalOrigen)
            .WithMany()
            .HasForeignKey(t => t.SucursalOrigenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.SucursalDestino)
            .WithMany()
            .HasForeignKey(t => t.SucursalDestinoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Detalles)
            .WithOne(dt => dt.Traslado)
            .HasForeignKey(dt => dt.TrasladoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DetalleTrasladoConfiguration : IEntityTypeConfiguration<DetalleTraslado>
{
    public void Configure(EntityTypeBuilder<DetalleTraslado> builder)
    {
        builder.ToTable("detalle_traslados");
        builder.HasKey(dt => dt.Id);
        builder.Property(dt => dt.Id).UseIdentityAlwaysColumn();

        builder.Property(dt => dt.TrasladoId)
            .IsRequired()
            .HasColumnName("traslado_id");

        builder.Property(dt => dt.ProductoId)
            .IsRequired()
            .HasColumnName("producto_id");

        builder.Property(dt => dt.NombreProducto)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("nombre_producto");

        builder.Property(dt => dt.CantidadSolicitada)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad_solicitada");

        builder.Property(dt => dt.CantidadRecibida)
            .HasPrecision(18, 4)
            .HasColumnName("cantidad_recibida");

        builder.Property(dt => dt.CostoUnitario)
            .HasPrecision(18, 4)
            .HasColumnName("costo_unitario");

        builder.Property(dt => dt.CostoTotal)
            .HasPrecision(18, 4)
            .HasColumnName("costo_total");

        builder.Property(dt => dt.Observaciones)
            .HasMaxLength(300)
            .HasColumnName("observaciones");

        builder.HasOne(dt => dt.Producto)
            .WithMany()
            .HasForeignKey(dt => dt.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
