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
            .HasMaxLength(100);

        builder.Property(i => i.Tipo)
            .IsRequired()
            .HasDefaultValue(TipoImpuesto.IVA);

        builder.Property(i => i.Porcentaje)
            .HasPrecision(8, 4);              // hasta 9999.9999%

        builder.Property(i => i.ValorFijo)
            .HasPrecision(18, 2);

        builder.Property(i => i.CodigoCuentaContable)
            .HasMaxLength(20);

        builder.Property(i => i.CodigoPais)
            .HasMaxLength(2)
            .HasDefaultValue("CO");

        builder.Property(i => i.Descripcion)
            .HasMaxLength(500);

        builder.Property(i => i.Activo)
            .HasDefaultValue(true);

        builder.Property(i => i.AplicaSobreBase)
            .HasDefaultValue(true);

        // Seed Data eliminado de HasData — los registros se siembran por SQL
        // per-empresa en la migración MakeEmpresaIdRequired para respetar
        // el constraint NOT NULL de EmpresaId.
        /* builder.HasData(
            // IVA (Estatuto Tributario Art. 468)
            new Impuesto
            {
                Id = 1, Nombre = "Exento 0%",   Tipo = TipoImpuesto.IVA,
                Porcentaje = 0.00m, Activo = true,
                AplicaSobreBase = true, CodigoPais = "CO",
                CodigoCuentaContable = "2408",
                Descripcion = "Bienes y servicios exentos de IVA",
                FechaCreacion = fecha
            },
            new Impuesto
            {
                Id = 2, Nombre = "IVA 5%",      Tipo = TipoImpuesto.IVA,
                Porcentaje = 0.05m, Activo = true,
                AplicaSobreBase = true, CodigoPais = "CO",
                CodigoCuentaContable = "2408",
                Descripcion = "IVA tarifa diferencial 5%",
                FechaCreacion = fecha
            },
            new Impuesto
            {
                Id = 3, Nombre = "IVA 19%",     Tipo = TipoImpuesto.IVA,
                Porcentaje = 0.19m, Activo = true,
                AplicaSobreBase = true, CodigoPais = "CO",
                CodigoCuentaContable = "2408",
                Descripcion = "IVA tarifa general 19%",
                FechaCreacion = fecha
            },
            // INC — Impuesto Nacional al Consumo (monofásico, excluyente de IVA)
            new Impuesto
            {
                Id = 4, Nombre = "INC 8%",      Tipo = TipoImpuesto.INC,
                Porcentaje = 0.08m, Activo = true,
                AplicaSobreBase = false,          // monofásico: no se acumula con IVA
                CodigoPais = "CO",
                CodigoCuentaContable = "2412",
                Descripcion = "INC restaurantes, bares, cafeterías (Art. 512-1 ET)",
                FechaCreacion = fecha
            },
            // Impuesto a las bolsas plásticas (valor fijo por unidad)
            new Impuesto
            {
                Id = 5, Nombre = "Bolsa $66",   Tipo = TipoImpuesto.Bolsa,
                Porcentaje = 0.00m, ValorFijo = 66m,
                Activo = true, AplicaSobreBase = false,
                CodigoPais = "CO",
                CodigoCuentaContable = "2424",
                Descripcion = "Impuesto bolsas plásticas 2026 (Ley 1819/2016)",
                FechaCreacion = fecha
            }
        ); */
    }
}
