using POS.Domain.Events.Inventario;

namespace POS.Domain.Aggregates;

/// <summary>
/// Aggregate de inventario por producto+sucursal.
/// Stream key: "inv-{ProductoId}-{SucursalId}"
/// Contiene el estado actual reconstruido de los eventos.
/// </summary>
public class InventarioAggregate
{
    public Guid Id { get; private set; }
    public Guid ProductoId { get; private set; }
    public int SucursalId { get; private set; }
    public decimal Cantidad { get; private set; }
    public decimal StockMinimo { get; private set; }
    public decimal CostoPromedio { get; private set; }

    // Para reconstruccion interna de lotes (FIFO/LIFO)
    private readonly List<LoteInterno> _lotes = new();
    public IReadOnlyList<LoteInterno> Lotes => _lotes.AsReadOnly();

    private InventarioAggregate() { }

    /// <summary>
    /// Genera el stream ID unico para un producto en una sucursal.
    /// </summary>
    /// <remarks>
    /// Usa MD5 como función hash determinística (no criptográfica) para derivar un Guid
    /// de 16 bytes a partir del par (productoId, sucursalId). MD5 es apropiado aquí
    /// porque el objetivo es unicidad de clave, no seguridad.
    /// IMPORTANTE: cambiar este algoritmo requiere una migración de todos los streams
    /// existentes en Marten — los IDs históricos están almacenados en events.mt_streams.
    /// </remarks>
    public static Guid GenerarStreamId(Guid productoId, int sucursalId)
    {
        var input = System.Text.Encoding.UTF8.GetBytes($"inv-{productoId}-{sucursalId}");
        var hash = System.Security.Cryptography.MD5.HashData(input);
        return new Guid(hash);
    }

    // ─── Comandos: validan reglas de negocio y emiten eventos ───

    public static (InventarioAggregate aggregate, EntradaCompraRegistrada evento) RegistrarEntrada(
        Guid streamId,
        Guid productoId,
        int sucursalId,
        decimal cantidad,
        decimal costoUnitario,
        decimal porcentajeImpuesto,
        decimal montoImpuesto,
        int? terceroId,
        string? nombreTercero,
        string? referencia,
        string? observaciones,
        int? usuarioId,
        int? sucursalUsuarioId,
        DateTime? fechaMovimiento = null)
    {
        if (cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a 0.");
        if (costoUnitario < 0)
            throw new InvalidOperationException("El costo no puede ser negativo.");

        var aggregate = new InventarioAggregate();
        var evento = new EntradaCompraRegistrada
        {
            ProductoId = productoId,
            SucursalId = sucursalId,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = cantidad * costoUnitario,
            PorcentajeImpuesto = porcentajeImpuesto,
            MontoImpuesto = montoImpuesto,
            TerceroId = terceroId,
            NombreTercero = nombreTercero,
            Referencia = referencia,
            Observaciones = observaciones,
            UsuarioId = usuarioId,
            Timestamp = fechaMovimiento ?? DateTime.UtcNow
        };

        aggregate.Apply(evento);
        aggregate.Id = streamId;
        return (aggregate, evento);
    }

    public EntradaCompraRegistrada AgregarEntrada(
        decimal cantidad,
        decimal costoUnitario,
        int? terceroId,
        string? nombreTercero,
        string? referencia,
        string? observaciones,
        int? usuarioId,
        DateTime? fechaMovimiento = null)
    {
        if (cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a 0.");

        var evento = new EntradaCompraRegistrada
        {
            ProductoId = this.ProductoId,
            SucursalId = this.SucursalId,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = cantidad * costoUnitario,
            TerceroId = terceroId,
            NombreTercero = nombreTercero,
            Referencia = referencia,
            Observaciones = observaciones,
            UsuarioId = usuarioId,
            Timestamp = fechaMovimiento ?? DateTime.UtcNow
        };

        Apply(evento);
        return evento;
    }

    public static (InventarioAggregate aggregate, EntradaManualRegistrada evento) RegistrarEntradaManual(
        Guid streamId,
        Guid productoId,
        int sucursalId,
        decimal cantidad,
        decimal costoUnitario,
        decimal porcentajeImpuesto,
        decimal montoImpuesto,
        int? terceroId,
        string? nombreTercero,
        string referencia,
        string? observaciones,
        int? usuarioId,
        int? sucursalUsuarioId,
        DateTime? fechaMovimiento = null)
    {
        if (cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a 0.");
        if (costoUnitario < 0)
            throw new InvalidOperationException("El costo no puede ser negativo.");

        var aggregate = new InventarioAggregate();
        var evento = new EntradaManualRegistrada
        {
            ProductoId = productoId,
            SucursalId = sucursalId,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = cantidad * costoUnitario,
            PorcentajeImpuesto = porcentajeImpuesto,
            MontoImpuesto = montoImpuesto,
            TerceroId = terceroId,
            NombreTercero = nombreTercero,
            Referencia = referencia,
            Observaciones = observaciones,
            UsuarioId = usuarioId,
            Timestamp = fechaMovimiento ?? DateTime.UtcNow
        };

        aggregate.Apply(evento);
        aggregate.Id = streamId;
        return (aggregate, evento);
    }

    public EntradaManualRegistrada AgregarEntradaManual(
        decimal cantidad,
        decimal costoUnitario,
        int? terceroId,
        string? nombreTercero,
        string referencia,
        string? observaciones,
        int? usuarioId,
        DateTime? fechaMovimiento = null)
    {
        if (cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a 0.");

        var evento = new EntradaManualRegistrada
        {
            ProductoId = this.ProductoId,
            SucursalId = this.SucursalId,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = cantidad * costoUnitario,
            TerceroId = terceroId,
            NombreTercero = nombreTercero,
            Referencia = referencia,
            Observaciones = observaciones,
            UsuarioId = usuarioId,
            Timestamp = fechaMovimiento ?? DateTime.UtcNow
        };

        Apply(evento);
        return evento;
    }

    public DevolucionProveedorRegistrada RegistrarDevolucion(
        decimal cantidad,
        int terceroId,
        string? nombreTercero,
        string? referencia,
        string? observaciones,
        int? usuarioId)
    {
        if (cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a 0.");
        if (Cantidad < cantidad)
            throw new InvalidOperationException(
                $"Stock insuficiente. Disponible: {Cantidad}, Solicitado: {cantidad}");

        var evento = new DevolucionProveedorRegistrada
        {
            ProductoId = this.ProductoId,
            SucursalId = this.SucursalId,
            Cantidad = cantidad,
            CostoUnitario = this.CostoPromedio,
            CostoTotal = cantidad * this.CostoPromedio,
            TerceroId = terceroId,
            NombreTercero = nombreTercero,
            Referencia = referencia,
            Observaciones = observaciones,
            UsuarioId = usuarioId
        };

        Apply(evento);
        return evento;
    }

    public AjusteInventarioRegistrado RegistrarAjuste(
        decimal cantidadNueva,
        string? observaciones,
        int? usuarioId)
    {
        if (cantidadNueva < 0)
            throw new InvalidOperationException("La cantidad no puede ser negativa.");

        var diferencia = cantidadNueva - Cantidad;
        var evento = new AjusteInventarioRegistrado
        {
            ProductoId = this.ProductoId,
            SucursalId = this.SucursalId,
            CantidadAnterior = this.Cantidad,
            CantidadNueva = cantidadNueva,
            Diferencia = diferencia,
            EsPositivo = diferencia >= 0,
            CostoUnitario = this.CostoPromedio,
            CostoTotal = Math.Abs(diferencia) * this.CostoPromedio,
            Observaciones = observaciones ?? $"Ajuste manual: {Cantidad} → {cantidadNueva}",
            UsuarioId = usuarioId
        };

        Apply(evento);
        return evento;
    }

    public SalidaVentaRegistrada RegistrarSalidaVenta(
        decimal cantidad,
        decimal precioVenta,
        decimal porcentajeImpuesto,
        decimal montoImpuesto,
        string? referenciaVenta,
        int? usuarioId,
        DateTime? fechaMovimiento = null)
    {
        if (cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a 0.");
        if (Cantidad < cantidad)
            throw new InvalidOperationException(
                $"Stock insuficiente. Disponible: {Cantidad}, Solicitado: {cantidad}");

        var evento = new SalidaVentaRegistrada
        {
            ProductoId = this.ProductoId,
            SucursalId = this.SucursalId,
            Cantidad = cantidad,
            CostoUnitario = this.CostoPromedio,
            CostoTotal = cantidad * this.CostoPromedio,
            PrecioVenta = precioVenta,
            PorcentajeImpuesto = porcentajeImpuesto,
            MontoImpuesto = montoImpuesto,
            ReferenciaVenta = referenciaVenta,
            UsuarioId = usuarioId,
            Timestamp = fechaMovimiento ?? DateTime.UtcNow
        };

        Apply(evento);
        return evento;
    }

    public StockMinimoActualizado ActualizarStockMinimo(decimal nuevoMinimo, int? usuarioId)
    {
        if (nuevoMinimo < 0)
            throw new InvalidOperationException("El stock minimo no puede ser negativo.");

        var evento = new StockMinimoActualizado
        {
            ProductoId = this.ProductoId,
            SucursalId = this.SucursalId,
            StockMinimoAnterior = this.StockMinimo,
            StockMinimoNuevo = nuevoMinimo,
            UsuarioId = usuarioId
        };

        Apply(evento);
        return evento;
    }

    /// <summary>
    /// Registra salida por traslado a otra sucursal
    /// </summary>
    public TrasladoSalidaRegistrado RegistrarSalidaTraslado(
        decimal cantidad,
        decimal costoUnitario,
        int sucursalDestinoId,
        string numeroTraslado,
        string? observaciones,
        int? usuarioId)
    {
        if (cantidad <= 0)
            throw new InvalidOperationException("La cantidad debe ser mayor a cero");

        if (Cantidad < cantidad)
            throw new InvalidOperationException($"Stock insuficiente. Disponible: {Cantidad}, Solicitado: {cantidad}");

        var evento = new TrasladoSalidaRegistrado
        {
            ProductoId = this.ProductoId,
            SucursalOrigenId = this.SucursalId,
            SucursalDestinoId = sucursalDestinoId,
            NumeroTraslado = numeroTraslado,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            CostoTotal = cantidad * costoUnitario,
            Observaciones = observaciones,
            UsuarioId = usuarioId,
            Timestamp = DateTime.UtcNow
        };

        Apply(evento);
        return evento;
    }

    /// <summary>
    /// Registra entrada por traslado desde otra sucursal
    /// </summary>
    public TrasladoEntradaRegistrado RegistrarEntradaTraslado(
        decimal cantidadRecibida,
        decimal costoUnitario,
        int sucursalOrigenId,
        string numeroTraslado,
        string? observaciones,
        int? usuarioId)
    {
        if (cantidadRecibida <= 0)
            throw new InvalidOperationException("La cantidad recibida debe ser mayor a cero");

        var evento = new TrasladoEntradaRegistrado
        {
            ProductoId = this.ProductoId,
            SucursalOrigenId = sucursalOrigenId,
            SucursalDestinoId = this.SucursalId,
            NumeroTraslado = numeroTraslado,
            CantidadRecibida = cantidadRecibida,
            CostoUnitario = costoUnitario,
            CostoTotal = cantidadRecibida * costoUnitario,
            Observaciones = observaciones,
            UsuarioId = usuarioId,
            Timestamp = DateTime.UtcNow
        };

        Apply(evento);
        return evento;
    }

    // ─── Apply: Marten los invoca al rehidratar desde eventos ───

    public void Apply(EntradaCompraRegistrada e)
    {
        ProductoId = e.ProductoId;
        SucursalId = e.SucursalId;

        // Costo promedio ponderado (en el aggregate siempre promedio, la Projection usa MetodoCosteo)
        var costoTotalAnterior = Cantidad * CostoPromedio;
        var costoEntrada = e.Cantidad * e.CostoUnitario;
        var cantidadTotal = Cantidad + e.Cantidad;
        CostoPromedio = cantidadTotal > 0
            ? (costoTotalAnterior + costoEntrada) / cantidadTotal
            : e.CostoUnitario;

        Cantidad = cantidadTotal;

        // Agregar lote — usa Timestamp del evento para garantizar idempotencia en replay
        _lotes.Add(new LoteInterno(e.Cantidad, e.CostoUnitario, e.Timestamp));
    }

    public void Apply(EntradaManualRegistrada e)
    {
        ProductoId = e.ProductoId;
        SucursalId = e.SucursalId;

        var costoTotalAnterior = Cantidad * CostoPromedio;
        var costoEntrada = e.Cantidad * e.CostoUnitario;
        var cantidadTotal = Cantidad + e.Cantidad;
        CostoPromedio = cantidadTotal > 0
            ? (costoTotalAnterior + costoEntrada) / cantidadTotal
            : e.CostoUnitario;

        Cantidad = cantidadTotal;
        _lotes.Add(new LoteInterno(e.Cantidad, e.CostoUnitario, e.Timestamp));
    }

    public void Apply(DevolucionProveedorRegistrada e)
    {
        Cantidad -= e.Cantidad;
    }

    public void Apply(AjusteInventarioRegistrado e)
    {
        Cantidad = e.CantidadNueva;
    }

    public void Apply(SalidaVentaRegistrada e)
    {
        Cantidad -= e.Cantidad;
    }

    public void Apply(StockMinimoActualizado e)
    {
        StockMinimo = e.StockMinimoNuevo;
    }

    public void Apply(TrasladoSalidaRegistrado e)
    {
        Cantidad -= e.Cantidad;
    }

    public void Apply(TrasladoEntradaRegistrado e)
    {
        // Actualizar cantidad
        var cantidadAnterior = Cantidad;
        Cantidad += e.CantidadRecibida;

        // Recalcular costo promedio
        var costoTotalAnterior = cantidadAnterior * CostoPromedio;
        var costoEntrada = e.CantidadRecibida * e.CostoUnitario;
        var cantidadTotal = Cantidad;
        CostoPromedio = cantidadTotal > 0
            ? (costoTotalAnterior + costoEntrada) / cantidadTotal
            : e.CostoUnitario;

        // Agregar lote
        _lotes.Add(new LoteInterno(e.CantidadRecibida, e.CostoUnitario, DateTime.UtcNow));
    }

    // ─── Lote interno (para reconstruccion de estado) ───

    public class LoteInterno
    {
        public decimal CantidadDisponible { get; set; }
        public decimal CostoUnitario { get; }
        public DateTime FechaEntrada { get; }

        public LoteInterno(decimal cantidad, decimal costoUnitario, DateTime fechaEntrada)
        {
            CantidadDisponible = cantidad;
            CostoUnitario = costoUnitario;
            FechaEntrada = fechaEntrada;
        }
    }
}
