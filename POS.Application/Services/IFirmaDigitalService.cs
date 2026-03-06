namespace POS.Application.Services;

public interface IFirmaDigitalService
{
    string FirmarXml(string xmlSinFirmar, string certificadoBase64, string password);
}
