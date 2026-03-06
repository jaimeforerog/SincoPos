namespace POS.Application.Services;

public record DianRespuesta(bool EsValido, string Codigo, string Descripcion);

public interface IDianSoapService
{
    Task<DianRespuesta> EnviarDocumentoAsync(string xmlFirmado, string cufe, string nitEmisor, string ambiente);
    Task<DianRespuesta> ConsultarEstadoAsync(string cufe, string ambiente);
}
