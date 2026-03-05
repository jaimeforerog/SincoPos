using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class RetencionReglaConfiguration : IEntityTypeConfiguration<RetencionRegla>
{
    public void Configure(EntityTypeBuilder<RetencionRegla> builder)
    {
        builder.ToTable("retenciones_reglas");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Tipo)
            .IsRequired();

        builder.Property(r => r.Porcentaje)
            .HasPrecision(8, 6)       // ej. 0.025000
            .IsRequired();

        builder.Property(r => r.BaseMinUVT)
            .HasPrecision(8, 2)
            .HasDefaultValue(4m);

        builder.Property(r => r.CodigoMunicipio)
            .HasMaxLength(10);        // Código DANE 5 dígitos

        builder.Property(r => r.PerfilVendedor)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.PerfilComprador)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.CodigoCuentaContable)
            .HasMaxLength(20);

        builder.Property(r => r.Activo)
            .HasDefaultValue(true);

        // ── Seed Data Colombia 2026 ─────────────────────────────────────────────
        // Reglas preconfiguradas siguiendo el Estatuto Tributario colombiano.
        // El administrador puede agregar/modificar según el tipo de negocio.
        var fecha = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            // ReteFuente 2.5% — Gran Contribuyente retiene a Régimen Ordinario
            // (Art. 383-395 ET, compras superiores a 4 UVT)
            new RetencionRegla
            {
                Id = 1,
                Nombre = "ReteFuente Compras 2.5%",
                Tipo = TipoRetencion.ReteFuente,
                Porcentaje = 0.025m,
                BaseMinUVT = 4m,                  // 4 UVT ≈ $188.260 COP
                CodigoMunicipio = null,            // Nacional
                PerfilVendedor = "REGIMEN_ORDINARIO",
                PerfilComprador = "GRAN_CONTRIBUYENTE",
                CodigoCuentaContable = "1355",    // Anticipo de impuestos
                Activo = true,
                FechaCreacion = fecha
            },
            // ReteFuente 11% — Gran Contribuyente retiene honorarios
            new RetencionRegla
            {
                Id = 2,
                Nombre = "ReteFuente Honorarios 11%",
                Tipo = TipoRetencion.ReteFuente,
                Porcentaje = 0.11m,
                BaseMinUVT = 0m,                  // Sin umbral mínimo
                CodigoMunicipio = null,
                PerfilVendedor = "REGIMEN_ORDINARIO",
                PerfilComprador = "GRAN_CONTRIBUYENTE",
                CodigoCuentaContable = "1355",
                Activo = false,                   // Inactiva por defecto, activar si aplica
                FechaCreacion = fecha
            },
            // ReteICA Bogotá — 9.66‰ (0.966%) sobre ventas en Bogotá
            new RetencionRegla
            {
                Id = 3,
                Nombre = "ReteICA Bogotá 0.966%",
                Tipo = TipoRetencion.ReteICA,
                Porcentaje = 0.00966m,
                BaseMinUVT = 0m,
                CodigoMunicipio = "11001",        // Bogotá D.C.
                PerfilVendedor = "REGIMEN_ORDINARIO",
                PerfilComprador = "GRAN_CONTRIBUYENTE",
                CodigoCuentaContable = "1356",    // Anticipo ReteICA
                Activo = true,
                FechaCreacion = fecha
            }
        );
    }
}
