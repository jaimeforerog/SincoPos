using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class TerceroConfiguration : IEntityTypeConfiguration<Tercero>
{
    public void Configure(EntityTypeBuilder<Tercero> builder)
    {
        builder.ToTable("terceros");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityAlwaysColumn();

        builder.Property(t => t.TipoIdentificacion)
            .HasColumnName("tipo_identificacion")
            .HasConversion<int>();

        builder.Property(t => t.Identificacion)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("identificacion");

        builder.HasIndex(t => t.Identificacion)
            .IsUnique()
            .HasDatabaseName("ix_terceros_identificacion");

        builder.Property(t => t.Nombre)
            .IsRequired()
            .HasMaxLength(250)
            .HasColumnName("nombre");

        builder.Property(t => t.TipoTercero)
            .HasColumnName("tipo_tercero")
            .HasConversion<int>();

        builder.Property(t => t.Telefono)
            .HasMaxLength(20)
            .HasColumnName("telefono");

        builder.Property(t => t.Email)
            .HasMaxLength(150)
            .HasColumnName("email");

        builder.Property(t => t.Direccion)
            .HasMaxLength(300)
            .HasColumnName("direccion");

        builder.Property(t => t.Ciudad)
            .HasMaxLength(100)
            .HasColumnName("ciudad");

        builder.Property(t => t.OrigenDatos)
            .HasColumnName("origen_datos")
            .HasConversion<int>()
            .HasDefaultValue(OrigenDatos.Local);

        builder.Property(t => t.ExternalId)
            .HasMaxLength(100)
            .HasColumnName("external_id");

        builder.HasIndex(t => t.ExternalId)
            .HasDatabaseName("ix_terceros_external_id")
            .HasFilter("external_id IS NOT NULL");

        builder.Property(t => t.Activo)
            .HasColumnName("activo");

        builder.Property(t => t.FechaCreacion)
            .HasColumnName("fecha_creacion");

        builder.Property(t => t.FechaModificacion)
            .HasColumnName("fecha_modificacion");

        // Campos fiscales
        builder.Property(t => t.PerfilTributario)
            .HasMaxLength(50)
            .HasColumnName("perfil_tributario")
            .HasDefaultValue("REGIMEN_COMUN");

        builder.Property(t => t.DigitoVerificacion)
            .HasMaxLength(1)
            .HasColumnName("digito_verificacion");

        builder.Property(t => t.CodigoDepartamento)
            .HasMaxLength(10)
            .HasColumnName("codigo_departamento");

        builder.Property(t => t.CodigoMunicipio)
            .HasMaxLength(10)
            .HasColumnName("codigo_municipio");

        builder.Property(t => t.EsGranContribuyente)
            .HasColumnName("es_gran_contribuyente")
            .HasDefaultValue(false);

        builder.Property(t => t.EsAutorretenedor)
            .HasColumnName("es_autorretenedor")
            .HasDefaultValue(false);

        builder.Property(t => t.EsResponsableIVA)
            .HasColumnName("es_responsable_iva")
            .HasDefaultValue(false);

        builder.HasIndex(t => t.TipoTercero)
            .HasDatabaseName("ix_terceros_tipo_tercero");

        builder.HasIndex(t => t.Activo)
            .HasDatabaseName("ix_terceros_activo")
            .HasFilter("activo = true");

        builder.HasMany(t => t.Actividades)
            .WithOne(a => a.Tercero)
            .HasForeignKey(a => a.TerceroId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TerceroActividadConfiguration : IEntityTypeConfiguration<TerceroActividad>
{
    public void Configure(EntityTypeBuilder<TerceroActividad> builder)
    {
        builder.ToTable("tercero_actividades");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();

        builder.Property(a => a.TerceroId)
            .HasColumnName("tercero_id");

        builder.Property(a => a.CodigoCIIU)
            .IsRequired()
            .HasMaxLength(10)
            .HasColumnName("codigo_ciiu");

        builder.Property(a => a.Descripcion)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("descripcion");

        builder.Property(a => a.EsPrincipal)
            .HasColumnName("es_principal")
            .HasDefaultValue(false);

        builder.HasIndex(a => new { a.TerceroId, a.CodigoCIIU })
            .IsUnique()
            .HasDatabaseName("ix_tercero_actividades_tercero_ciiu");
    }
}
