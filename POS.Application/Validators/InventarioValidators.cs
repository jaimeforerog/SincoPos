using FluentValidation;
using POS.Application.DTOs;

namespace POS.Application.Validators;

public class EntradaInventarioValidator : AbstractValidator<EntradaInventarioDto>
{
    public EntradaInventarioValidator()
    {
        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("El producto es obligatorio.");

        RuleFor(x => x.SucursalId)
            .GreaterThan(0).WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.Cantidad)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0.");

        RuleFor(x => x.CostoUnitario)
            .GreaterThanOrEqualTo(0).WithMessage("El costo unitario no puede ser negativo.");
    }
}

public class AjusteInventarioValidator : AbstractValidator<AjusteInventarioDto>
{
    public AjusteInventarioValidator()
    {
        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("El producto es obligatorio.");

        RuleFor(x => x.SucursalId)
            .GreaterThan(0).WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.CantidadNueva)
            .GreaterThanOrEqualTo(0).WithMessage("La cantidad no puede ser negativa.");
    }
}

public class DevolucionProveedorValidator : AbstractValidator<DevolucionProveedorDto>
{
    public DevolucionProveedorValidator()
    {
        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("El producto es obligatorio.");

        RuleFor(x => x.SucursalId)
            .GreaterThan(0).WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.Cantidad)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0.");

        RuleFor(x => x.TerceroId)
            .GreaterThan(0).WithMessage("El proveedor es obligatorio para una devolucion.");
    }
}
