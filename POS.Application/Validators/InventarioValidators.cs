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

public class CrearTrasladoValidator : AbstractValidator<CrearTrasladoDto>
{
    public CrearTrasladoValidator()
    {
        RuleFor(x => x.SucursalOrigenId)
            .GreaterThan(0).WithMessage("SucursalOrigenId es requerido");

        RuleFor(x => x.SucursalDestinoId)
            .GreaterThan(0).WithMessage("SucursalDestinoId es requerido");

        RuleFor(x => x)
            .Must(x => x.SucursalOrigenId != x.SucursalDestinoId)
            .WithMessage("La sucursal origen y destino deben ser diferentes");

        RuleFor(x => x.Lineas)
            .NotEmpty().WithMessage("Debe incluir al menos un producto");

        RuleForEach(x => x.Lineas).SetValidator(new LineaTrasladoValidator());
    }
}

public class LineaTrasladoValidator : AbstractValidator<LineaTrasladoDto>
{
    public LineaTrasladoValidator()
    {
        RuleFor(x => x.ProductoId).NotEmpty();
        RuleFor(x => x.Cantidad).GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0");
    }
}

public class RecibirTrasladoValidator : AbstractValidator<RecibirTrasladoDto>
{
    public RecibirTrasladoValidator()
    {
        RuleFor(x => x.Lineas)
            .NotEmpty().WithMessage("Debe incluir al menos un producto recibido");

        RuleForEach(x => x.Lineas).SetValidator(new LineaRecepcionValidator());
    }
}

public class LineaRecepcionValidator : AbstractValidator<LineaRecepcionDto>
{
    public LineaRecepcionValidator()
    {
        RuleFor(x => x.ProductoId).NotEmpty();
        RuleFor(x => x.CantidadRecibida)
            .GreaterThanOrEqualTo(0)
            .WithMessage("La cantidad recibida no puede ser negativa");
    }
}

public class RechazarTrasladoValidator : AbstractValidator<RechazarTrasladoDto>
{
    public RechazarTrasladoValidator()
    {
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("Debe proporcionar un motivo de rechazo");
    }
}

public class CancelarTrasladoValidator : AbstractValidator<CancelarTrasladoDto>
{
    public CancelarTrasladoValidator()
    {
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Debe proporcionar un motivo de cancelación");
    }
}

// ===== VALIDATORS ÓRDENES DE COMPRA =====

public class CrearOrdenCompraValidator : AbstractValidator<CrearOrdenCompraDto>
{
    public CrearOrdenCompraValidator()
    {
        RuleFor(x => x.SucursalId)
            .GreaterThan(0).WithMessage("La sucursal es obligatoria");

        RuleFor(x => x.ProveedorId)
            .GreaterThan(0).WithMessage("El proveedor es obligatorio");

        RuleFor(x => x.Lineas)
            .NotEmpty().WithMessage("La orden debe tener al menos una línea");

        RuleForEach(x => x.Lineas)
            .SetValidator(new LineaOrdenCompraValidator());
    }
}

public class LineaOrdenCompraValidator : AbstractValidator<LineaOrdenCompraDto>
{
    public LineaOrdenCompraValidator()
    {
        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("El producto es obligatorio");

        RuleFor(x => x.Cantidad)
            .GreaterThan(0).WithMessage("La cantidad debe ser mayor a 0");

        RuleFor(x => x.PrecioUnitario)
            .GreaterThan(0).WithMessage("El precio unitario debe ser mayor a 0");

        RuleFor(x => x.ImpuestoId)
            .GreaterThan(0).WithMessage("El ID de impuesto debe ser mayor a 0")
            .When(x => x.ImpuestoId.HasValue);
    }
}

public class RecibirOrdenCompraValidator : AbstractValidator<RecibirOrdenCompraDto>
{
    public RecibirOrdenCompraValidator()
    {
        RuleFor(x => x.Lineas)
            .NotEmpty().WithMessage("Debe especificar al menos una línea para recibir");

        RuleForEach(x => x.Lineas)
            .SetValidator(new LineaRecepcionOrdenCompraValidator());
    }
}

public class LineaRecepcionOrdenCompraValidator : AbstractValidator<LineaRecepcionOrdenCompraDto>
{
    public LineaRecepcionOrdenCompraValidator()
    {
        RuleFor(x => x.ProductoId)
            .NotEmpty().WithMessage("El producto es obligatorio");

        RuleFor(x => x.CantidadRecibida)
            .GreaterThan(0).WithMessage("La cantidad recibida debe ser mayor a 0");
    }
}

public class RechazarOrdenCompraValidator : AbstractValidator<RechazarOrdenCompraDto>
{
    public RechazarOrdenCompraValidator()
    {
        RuleFor(x => x.MotivoRechazo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio")
            .MaximumLength(500).WithMessage("El motivo de rechazo no puede exceder 500 caracteres");
    }
}

public class CancelarOrdenCompraValidator : AbstractValidator<CancelarOrdenCompraDto>
{
    public CancelarOrdenCompraValidator()
    {
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("El motivo de cancelación es obligatorio")
            .MaximumLength(500).WithMessage("El motivo de cancelación no puede exceder 500 caracteres");
    }
}
