using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearProductoValidator : AbstractValidator<CrearProductoDto>
{
    public CrearProductoValidator()
    {
        RuleFor(x => x.CodigoBarras)
            .NotEmpty().WithMessage("El codigo de barras es obligatorio.")
            .MaximumLength(50);

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.PrecioVenta)
            .GreaterThanOrEqualTo(0).WithMessage("El precio de venta no puede ser negativo.");

        RuleFor(x => x.CategoriaId)
            .GreaterThan(0).WithMessage("La categoria es obligatoria.");

        RuleFor(x => x.PrecioCosto)
            .GreaterThanOrEqualTo(0).WithMessage("El precio de costo no puede ser negativo.");

        // Comentado: El precio de venta se maneja por sucursal, puede ser 0 en el producto base
        // RuleFor(x => x)
        //     .Must(x => x.PrecioVenta >= x.PrecioCosto)
        //     .WithMessage("El precio de venta no puede ser menor al precio de costo.");
    }
}

public class ActualizarProductoValidator : AbstractValidator<ActualizarProductoDto>
{
    public ActualizarProductoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.PrecioVenta)
            .GreaterThanOrEqualTo(0).WithMessage("El precio de venta no puede ser negativo.");

        RuleFor(x => x.PrecioCosto)
            .GreaterThanOrEqualTo(0).WithMessage("El precio de costo no puede ser negativo.");
    }
}
