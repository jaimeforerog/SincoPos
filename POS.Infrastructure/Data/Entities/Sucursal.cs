namespace POS.Infrastructure.Data.Entities;

public class Sucursal : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public MetodoCosteo MetodoCosteo { get; set; } = MetodoCosteo.PromedioPonderado;
}

public enum MetodoCosteo
{
    PromedioPonderado = 0,
    PEPS = 1,              // FIFO
    UEPS = 2               // LIFO
}
