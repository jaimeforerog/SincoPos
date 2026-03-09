using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ConceptoRetencionConfiguration : IEntityTypeConfiguration<ConceptoRetencion>
{
    public void Configure(EntityTypeBuilder<ConceptoRetencion> builder)
    {
        builder.ToTable("conceptos_retencion");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.CodigoDian)
            .HasMaxLength(10);

        builder.Property(c => c.PorcentajeSugerido)
            .HasPrecision(5, 2);

        builder.Property(c => c.Activo)
            .HasDefaultValue(true);

        // ── Seed Data: Conceptos DIAN comunes ───────────────────────────────────
        var fecha = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new ConceptoRetencion
            {
                Id = 1,
                Nombre = "Compras generales",
                CodigoDian = "2307",
                PorcentajeSugerido = 2.5m,
                Activo = true,
                FechaCreacion = fecha
            },
            new ConceptoRetencion
            {
                Id = 2,
                Nombre = "Servicios generales",
                CodigoDian = "2304",
                PorcentajeSugerido = 4m,
                Activo = true,
                FechaCreacion = fecha
            },
            new ConceptoRetencion
            {
                Id = 3,
                Nombre = "Honorarios",
                CodigoDian = "2301",
                PorcentajeSugerido = 11m,
                Activo = true,
                FechaCreacion = fecha
            },
            new ConceptoRetencion
            {
                Id = 4,
                Nombre = "Comisiones",
                CodigoDian = "2302",
                PorcentajeSugerido = 11m,
                Activo = true,
                FechaCreacion = fecha
            },
            new ConceptoRetencion
            {
                Id = 5,
                Nombre = "Arrendamientos",
                CodigoDian = "2306",
                PorcentajeSugerido = 3.5m,
                Activo = true,
                FechaCreacion = fecha
            }
        );
    }
}
