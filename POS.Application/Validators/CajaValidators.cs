using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearCajaValidator : AbstractValidator<CrearCajaDto>
{
    public CrearCajaValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la caja es obligatorio.")
            .MaximumLength(50);

        RuleFor(x => x.SucursalId)
            .GreaterThan(0).WithMessage("La sucursal es obligatoria.");
    }
}

public class AbrirCajaValidator : AbstractValidator<AbrirCajaDto>
{
    public AbrirCajaValidator()
    {
        RuleFor(x => x.MontoApertura)
            .GreaterThanOrEqualTo(0).WithMessage("El monto de apertura no puede ser negativo.");
    }
}

public class CerrarCajaValidator : AbstractValidator<CerrarCajaDto>
{
    public CerrarCajaValidator()
    {
        RuleFor(x => x.MontoReal)
            .GreaterThanOrEqualTo(0).WithMessage("El monto real no puede ser negativo.");
    }
}
