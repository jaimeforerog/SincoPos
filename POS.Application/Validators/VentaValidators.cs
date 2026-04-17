using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class CrearVentaValidator : AbstractValidator<CrearVentaDto>
{
    public CrearVentaValidator()
    {
        RuleFor(x => x.SucursalId).GreaterThan(0).WithMessage("SucursalId es requerido.");
        RuleFor(x => x.CajaId).GreaterThan(0).WithMessage("CajaId es requerido.");
        RuleFor(x => x.ClienteId).NotNull().GreaterThan(0).WithMessage("El cliente es obligatorio.");
        RuleFor(x => x.Lineas).NotEmpty().WithMessage("La venta debe tener al menos una linea.");
        RuleForEach(x => x.Lineas).SetValidator(new LineaVentaValidator());
    }
}

public class LineaVentaValidator : AbstractValidator<LineaVentaDto>
{
    public LineaVentaValidator()
    {
        RuleFor(x => x.ProductoId).NotEmpty().WithMessage("ProductoId es requerido.");
        RuleFor(x => x.Cantidad).GreaterThan(0).WithMessage("Cantidad debe ser mayor a 0.");
        RuleFor(x => x.Descuento).GreaterThanOrEqualTo(0).WithMessage("Descuento no puede ser negativo.");
        RuleFor(x => x.PrecioUnitario)
            .GreaterThan(0)
            .When(x => x.PrecioUnitario.HasValue)
            .WithMessage("PrecioUnitario debe ser mayor a 0.");
    }
}

public class CrearPrecioSucursalValidator : AbstractValidator<CrearPrecioSucursalDto>
{
    public CrearPrecioSucursalValidator()
    {
        RuleFor(x => x.ProductoId).NotEmpty();
        RuleFor(x => x.SucursalId).GreaterThan(0);
        RuleFor(x => x.PrecioVenta).GreaterThan(0).WithMessage("PrecioVenta debe ser mayor a 0.");
        RuleFor(x => x.PrecioMinimo)
            .GreaterThan(0)
            .When(x => x.PrecioMinimo.HasValue)
            .WithMessage("PrecioMinimo debe ser mayor a 0.");
        RuleFor(x => x)
            .Must(x => !x.PrecioMinimo.HasValue || x.PrecioMinimo <= x.PrecioVenta)
            .WithMessage("PrecioMinimo no puede ser mayor que PrecioVenta.");
    }
}

public class CrearDevolucionParcialValidator : AbstractValidator<CrearDevolucionParcialDto>
{
    public CrearDevolucionParcialValidator()
    {
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("El motivo es obligatorio")
            .MaximumLength(500).WithMessage("El motivo no puede exceder 500 caracteres");

        RuleFor(x => x.Lineas)
            .NotEmpty().WithMessage("Debe incluir al menos un producto a devolver");

        RuleForEach(x => x.Lineas).SetValidator(new LineaDevolucionValidator());
    }
}

public class LineaDevolucionValidator : AbstractValidator<LineaDevolucionDto>
{
    public LineaDevolucionValidator()
    {
        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("ProductoId es requerido");

        RuleFor(x => x.Cantidad)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0");
    }
}
