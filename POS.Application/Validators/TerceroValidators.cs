using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearTerceroValidator : AbstractValidator<CrearTerceroDto>
{
    public CrearTerceroValidator()
    {
        RuleFor(x => x.TipoIdentificacion)
            .NotEmpty().WithMessage("El tipo de identificacion es obligatorio.");

        RuleFor(x => x.Identificacion)
            .NotEmpty().WithMessage("La identificacion es obligatoria.")
            .MaximumLength(50);

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(250);

        RuleFor(x => x.TipoTercero)
            .NotEmpty().WithMessage("El tipo de tercero es obligatorio.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Telefono)
            .MaximumLength(20)
            .When(x => x.Telefono != null);
    }
}

public class ActualizarTerceroValidator : AbstractValidator<ActualizarTerceroDto>
{
    public ActualizarTerceroValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(250);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Telefono)
            .MaximumLength(20)
            .When(x => x.Telefono != null);
    }
}
