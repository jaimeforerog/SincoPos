using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ImpuestoConfiguration : IEntityTypeConfiguration<Impuesto>
{
    public void Configure(EntityTypeBuilder<Impuesto> builder)
    {
        builder.ToTable("impuestos");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Nombre)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.Porcentaje)
            .HasPrecision(5, 4) // Ej. 0.1900
            .IsRequired();

        builder.Property(i => i.Activo)
            .HasDefaultValue(true);

        // Seed data basico
        builder.HasData(
            new Impuesto { Id = 1, Nombre = "Exento 0%", Porcentaje = 0.00m, Activo = true, FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Impuesto { Id = 2, Nombre = "IVA 5%", Porcentaje = 0.05m, Activo = true, FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Impuesto { Id = 3, Nombre = "IVA 19%", Porcentaje = 0.19m, Activo = true, FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
