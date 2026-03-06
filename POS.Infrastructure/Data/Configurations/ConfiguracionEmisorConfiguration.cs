using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ConfiguracionEmisorConfiguration : IEntityTypeConfiguration<ConfiguracionEmisor>
{
    public void Configure(EntityTypeBuilder<ConfiguracionEmisor> builder)
    {
        builder.ToTable("configuracion_emisor");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();

        builder.Property(c => c.SucursalId).HasColumnName("sucursal_id");
        builder.HasIndex(c => c.SucursalId)
            .IsUnique()
            .HasDatabaseName("ix_configuracion_emisor_sucursal_id");

        builder.Property(c => c.Nit).IsRequired().HasMaxLength(20).HasColumnName("nit");
        builder.Property(c => c.DigitoVerificacion).IsRequired().HasMaxLength(1).HasColumnName("digito_verificacion");
        builder.Property(c => c.RazonSocial).IsRequired().HasMaxLength(250).HasColumnName("razon_social");
        builder.Property(c => c.NombreComercial).IsRequired().HasMaxLength(250).HasColumnName("nombre_comercial");
        builder.Property(c => c.Direccion).IsRequired().HasMaxLength(500).HasColumnName("direccion");
        builder.Property(c => c.CodigoMunicipio).IsRequired().HasMaxLength(10).HasColumnName("codigo_municipio");
        builder.Property(c => c.CodigoDepartamento).IsRequired().HasMaxLength(10).HasColumnName("codigo_departamento");
        builder.Property(c => c.Telefono).IsRequired().HasMaxLength(20).HasColumnName("telefono");
        builder.Property(c => c.Email).IsRequired().HasMaxLength(100).HasColumnName("email");
        builder.Property(c => c.CodigoCiiu).IsRequired().HasMaxLength(10).HasColumnName("codigo_ciiu");
        builder.Property(c => c.PerfilTributario).IsRequired().HasMaxLength(50).HasColumnName("perfil_tributario");

        builder.Property(c => c.NumeroResolucion).IsRequired().HasMaxLength(50).HasColumnName("numero_resolucion");
        builder.Property(c => c.FechaResolucion).HasColumnName("fecha_resolucion");
        builder.Property(c => c.Prefijo).IsRequired().HasMaxLength(5).HasColumnName("prefijo");
        builder.Property(c => c.NumeroDesde).HasColumnName("numero_desde");
        builder.Property(c => c.NumeroHasta).HasColumnName("numero_hasta");
        builder.Property(c => c.NumeroActual).HasColumnName("numero_actual").HasDefaultValue(0L);
        builder.Property(c => c.FechaVigenciaDesde).HasColumnName("fecha_vigencia_desde");
        builder.Property(c => c.FechaVigenciaHasta).HasColumnName("fecha_vigencia_hasta");

        builder.Property(c => c.Ambiente).IsRequired().HasMaxLength(1).HasColumnName("ambiente").HasDefaultValue("2");
        builder.Property(c => c.PinSoftware).IsRequired().HasMaxLength(100).HasColumnName("pin_software");
        builder.Property(c => c.IdSoftware).IsRequired().HasMaxLength(100).HasColumnName("id_software");
        builder.Property(c => c.CertificadoBase64).IsRequired().HasColumnName("certificado_base64");
        builder.Property(c => c.CertificadoPassword).IsRequired().HasMaxLength(200).HasColumnName("certificado_password");

        builder.HasOne(c => c.Sucursal)
            .WithMany()
            .HasForeignKey(c => c.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
