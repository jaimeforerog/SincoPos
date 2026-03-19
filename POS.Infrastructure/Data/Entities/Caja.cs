namespace POS.Infrastructure.Data.Entities;

public class Caja : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public int? EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public EstadoCaja Estado { get; set; } = EstadoCaja.Cerrada;
    public decimal MontoApertura { get; set; }
    public decimal MontoActual { get; set; }
    public DateTime? FechaApertura { get; set; }
    public DateTime? FechaCierre { get; set; }
    public int? AbiertaPorUsuarioId { get; set; }

    // Navegacion
    public Sucursal Sucursal { get; set; } = null!;
}

public enum EstadoCaja
{
    Cerrada = 0,
    Abierta = 1
}
