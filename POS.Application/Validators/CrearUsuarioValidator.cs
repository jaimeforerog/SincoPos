using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearUsuarioValidator : AbstractValidator<CrearUsuarioDto>
{
    private static readonly string[] RolesValidos = { "admin", "supervisor", "cajero", "vendedor" };

    public CrearUsuarioValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no tiene un formato valido.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre completo debe tener al menos 2 caracteres.")
            .MaximumLength(255).WithMessage("El nombre completo no puede exceder 255 caracteres.");

        RuleFor(x => x.Rol)
            .NotEmpty().WithMessage("El rol es obligatorio.")
            .Must(rol => RolesValidos.Contains(rol.ToLower()))
            .WithMessage($"El rol debe ser uno de: {string.Join(", ", RolesValidos)}");

        RuleFor(x => x.SucursalDefaultId)
            .GreaterThan(0).WithMessage("La sucursal default debe ser un ID valido.")
            .When(x => x.SucursalDefaultId.HasValue);
    }
}

public class ActualizarUsuarioValidator : AbstractValidator<ActualizarUsuarioDto>
{
    private static readonly string[] RolesValidos = { "admin", "supervisor", "cajero", "vendedor" };

    public ActualizarUsuarioValidator()
    {
        RuleFor(x => x.NombreCompleto)
            .MinimumLength(2).WithMessage("El nombre completo debe tener al menos 2 caracteres.")
            .MaximumLength(255).WithMessage("El nombre completo no puede exceder 255 caracteres.")
            .When(x => x.NombreCompleto != null);

        RuleFor(x => x.Rol)
            .Must(rol => RolesValidos.Contains(rol!.ToLower()))
            .WithMessage($"El rol debe ser uno de: {string.Join(", ", RolesValidos)}")
            .When(x => x.Rol != null);

        RuleFor(x => x.SucursalDefaultId)
            .GreaterThan(0).WithMessage("La sucursal default debe ser un ID valido.")
            .When(x => x.SucursalDefaultId.HasValue);
    }
}

public class CambiarRolValidator : AbstractValidator<CambiarRolDto>
{
    private static readonly string[] RolesValidos = { "admin", "supervisor", "cajero", "vendedor" };

    public CambiarRolValidator()
    {
        RuleFor(x => x.Rol)
            .NotEmpty().WithMessage("El rol es obligatorio.")
            .Must(rol => RolesValidos.Contains(rol.ToLower()))
            .WithMessage($"El rol debe ser uno de: {string.Join(", ", RolesValidos)}");
    }
}
