namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Empresa (tenant) que agrupa sucursales y su catálogo.
/// Se llena manualmente en la base de datos — no tiene UI en POS.
/// </summary>
public class Empresa
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? RazonSocial { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navegación
    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}
