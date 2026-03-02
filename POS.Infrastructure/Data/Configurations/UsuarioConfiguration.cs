using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(u => u.KeycloakId)
            .HasColumnName("keycloak_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(u => u.KeycloakId)
            .IsUnique()
            .HasDatabaseName("ix_usuarios_keycloak_id");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .HasDatabaseName("ix_usuarios_email");

        builder.Property(u => u.NombreCompleto)
            .HasColumnName("nombre_completo")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(50);

        builder.Property(u => u.Rol)
            .HasColumnName("rol")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.SucursalDefaultId)
            .HasColumnName("sucursal_default_id");

        builder.Property(u => u.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true);

        builder.Property(u => u.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(u => u.FechaModificacion)
            .HasColumnName("fecha_modificacion");

        builder.Property(u => u.UltimoAcceso)
            .HasColumnName("ultimo_acceso");

        // Relaciones
        builder.HasOne(u => u.SucursalDefault)
            .WithMany()
            .HasForeignKey(u => u.SucursalDefaultId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
