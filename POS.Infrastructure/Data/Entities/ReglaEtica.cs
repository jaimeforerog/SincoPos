namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Regla de supervisión ética configurable sin redeploy.
/// Permite definir límites de negocio (descuento máximo, monto mínimo, etc.)
/// que el EthicalGuardService evalúa en tiempo real durante la venta.
/// </summary>
public class ReglaEtica
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }

    /// <summary>Nombre descriptivo de la regla (p.ej. "Descuento máximo cajero").</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Contexto de aplicación: Venta, Traslado, etc.</summary>
    public ContextoReglaEtica Contexto { get; set; } = ContextoReglaEtica.Venta;

    /// <summary>Tipo de condición evaluada.</summary>
    public TipoCondicionEtica Condicion { get; set; }

    /// <summary>Valor límite numérico (porcentaje o monto según condición).</summary>
    public decimal ValorLimite { get; set; }

    /// <summary>Acción al superar el límite.</summary>
    public AccionReglaEtica Accion { get; set; } = AccionReglaEtica.Alertar;

    /// <summary>Mensaje mostrado al cajero o auditor.</summary>
    public string? Mensaje { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<ActivacionReglaEtica> Activaciones { get; set; } = new List<ActivacionReglaEtica>();
}

/// <summary>
/// Registro de auditoría cada vez que una regla ética se activa.
/// </summary>
public class ActivacionReglaEtica
{
    public int Id { get; set; }
    public int ReglaEticaId { get; set; }
    public int? VentaId { get; set; }
    public int? SucursalId { get; set; }
    public int? UsuarioId { get; set; }
    public string? Detalle { get; set; }   // qué valor superó el límite
    public AccionReglaEtica AccionTomada { get; set; }
    public DateTime FechaActivacion { get; set; } = DateTime.UtcNow;

    public ReglaEtica Regla { get; set; } = null!;
}

public enum ContextoReglaEtica
{
    Venta = 0,
    Traslado = 1,
}

public enum TipoCondicionEtica
{
    /// <summary>Descuento total de la venta supera X %.</summary>
    DescuentoMaximoPorcentaje = 0,

    /// <summary>Total de la venta supera X (monto máximo por transacción).</summary>
    MontoMaximoTransaccion = 1,

    /// <summary>Número de líneas en la venta supera X.</summary>
    MaximoLineasVenta = 2,

    /// <summary>Precio unitario en una línea es menor que X % del precio base.</summary>
    PrecioMinimoSobreBase = 3,
}

public enum AccionReglaEtica
{
    /// <summary>Registrar en log pero permitir la venta.</summary>
    Alertar = 0,

    /// <summary>Bloquear la venta y devolver error al cajero.</summary>
    Bloquear = 1,
}
