using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearCategoriaValidator : AbstractValidator<CrearCategoriaDto>
{
    public CrearCategoriaValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la categoria es obligatorio.")
            .MaximumLength(100);
    }
}

public class ActualizarCategoriaValidator : AbstractValidator<ActualizarCategoriaDto>
{
    public ActualizarCategoriaValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la categoria es obligatorio.")
            .MaximumLength(100);
    }
}
