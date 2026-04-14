using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearConfiguracionVariableValidator : AbstractValidator<CrearConfiguracionVariableDto>
{
    public CrearConfiguracionVariableValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la variable es obligatorio.")
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("El nombre solo puede contener letras, números y guiones bajos.");

        RuleFor(x => x.Valor)
            .NotEmpty().WithMessage("El valor de la variable es obligatorio.")
            .MaximumLength(500);

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .When(x => x.Descripcion != null);
    }
}

public class ActualizarConfiguracionVariableValidator : AbstractValidator<ActualizarConfiguracionVariableDto>
{
    public ActualizarConfiguracionVariableValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la variable es obligatorio.")
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("El nombre solo puede contener letras, números y guiones bajos.");

        RuleFor(x => x.Valor)
            .NotEmpty().WithMessage("El valor de la variable es obligatorio.")
            .MaximumLength(500);

        RuleFor(x => x.Descripcion)
            .MaximumLength(500)
            .When(x => x.Descripcion != null);
    }
}
