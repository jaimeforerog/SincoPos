using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("productos");

        builder.HasKey(p => p.Id);

        // Guid como PK, no auto-generado (viene del StreamId de Marten)
        builder.Property(p => p.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(p => p.CodigoBarras)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("codigo_barras");

        builder.HasIndex(p => p.CodigoBarras)
            .IsUnique()
            .HasDatabaseName("ix_productos_codigo_barras");

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("nombre");

        builder.Property(p => p.Descripcion)
            .HasMaxLength(500)
            .HasColumnName("descripcion");

        builder.Property(p => p.PrecioVenta)
            .HasPrecision(18, 2)
            .HasColumnName("precio_venta");

        builder.Property(p => p.PrecioCosto)
            .HasPrecision(18, 2)
            .HasColumnName("precio_costo");

        builder.Property(p => p.Activo)
            .HasColumnName("activo");

        builder.Property(p => p.FechaCreacion)
            .HasColumnName("fecha_creacion");

        builder.Property(p => p.FechaModificacion)
            .HasColumnName("fecha_modificacion");

        builder.Property(p => p.CategoriaId)
            .IsRequired()
            .HasColumnName("categoria_id");

        builder.HasOne(p => p.Categoria)
            .WithMany(c => c.Productos)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.ImpuestoId)
            .HasColumnName("impuesto_id");

        builder.HasOne(p => p.Impuesto)
            .WithMany(i => i.Productos)
            .HasForeignKey(p => p.ImpuestoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categorias");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("nombre");

        builder.Property(c => c.Descripcion)
            .HasMaxLength(300)
            .HasColumnName("descripcion");

        builder.Property(c => c.Activo)
            .HasColumnName("activo");

        builder.Property(c => c.MargenGanancia)
            .HasPrecision(5, 2)
            .HasDefaultValue(0.30m)
            .HasColumnName("margen_ganancia");
    }
}
