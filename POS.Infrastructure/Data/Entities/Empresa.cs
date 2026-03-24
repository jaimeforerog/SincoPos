namespace POS.Infrastructure.Data.Entities;

/// <summary>
/// Empresa (tenant) que agrupa sucursales y su catálogo.
/// </summary>
public class Empresa : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? RazonSocial { get; set; }

    // Navegación
    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}
