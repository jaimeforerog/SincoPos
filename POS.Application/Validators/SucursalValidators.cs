using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearSucursalValidator : AbstractValidator<CrearSucursalDto>
{
    public CrearSucursalValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la sucursal es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Telefono)
            .MaximumLength(20)
            .When(x => x.Telefono != null);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}

public class ActualizarSucursalValidator : AbstractValidator<ActualizarSucursalDto>
{
    public ActualizarSucursalValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la sucursal es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.Telefono)
            .MaximumLength(20)
            .When(x => x.Telefono != null);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .MaximumLength(150)
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
