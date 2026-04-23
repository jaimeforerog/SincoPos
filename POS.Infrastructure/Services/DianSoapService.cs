using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

public sealed class DianSoapService : IDianSoapService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DianSoapService> _logger;

    private const string EndpointPruebas = "https://vpfe-hab.dian.gov.co/WcfDianCustomerServices.svc";
    private const string EndpointProduccion = "https://vpfe.dian.gov.co/WcfDianCustomerServices.svc";
    private const string SoapActionEnviar = "http://wcf.dian.colombia/IWcfDianCustomerServices/SendBillSync";
    private const string SoapActionConsultar = "http://wcf.dian.colombia/IWcfDianCustomerServices/GetStatusZip";

    public DianSoapService(HttpClient httpClient, ILogger<DianSoapService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DianRespuesta> EnviarDocumentoAsync(string xmlFirmado, string cufe,
        string nitEmisor, string ambiente)
    {
        var endpoint = ambiente == "1" ? EndpointProduccion : EndpointPruebas;

        // Empaquetar XML en ZIP, nombre del archivo = cufe.xml
        var zipBase64 = EmpaquetarEnZip(xmlFirmado, $"{cufe}.xml");

        var soapEnvelope = BuildSendBillSyncEnvelope($"{cufe}.zip", zipBase64);

        try
        {
            var response = await EnviarSoapAsync(endpoint, SoapActionEnviar, soapEnvelope);
            return ParsearRespuestaEnvio(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando documento CUFE={Cufe} a DIAN {Ambiente}", cufe, ambiente);
            return new DianRespuesta(false, "ERROR_CONEXION", ex.Message);
        }
    }

    public async Task<DianRespuesta> ConsultarEstadoAsync(string cufe, string ambiente)
    {
        var endpoint = ambiente == "1" ? EndpointProduccion : EndpointPruebas;
        var soapEnvelope = BuildGetStatusZipEnvelope(cufe);

        try
        {
            var response = await EnviarSoapAsync(endpoint, SoapActionConsultar, soapEnvelope);
            return ParsearRespuestaConsulta(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando estado CUFE={Cufe} en DIAN", cufe);
            return new DianRespuesta(false, "ERROR_CONEXION", ex.Message);
        }
    }

    // ─── Helpers privados ─────────────────────────────────────────────────────

    private static string EmpaquetarEnZip(string contenidoXml, string nombreArchivo)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(nombreArchivo, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(contenidoXml);
        }
        ms.Position = 0;
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string BuildSendBillSyncEnvelope(string nombreZip, string contentBase64) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope"
                       xmlns:wcf="http://wcf.dian.colombia">
          <soap:Header/>
          <soap:Body>
            <wcf:SendBillSync>
              <wcf:fileName>{nombreZip}</wcf:fileName>
              <wcf:contentFile>{contentBase64}</wcf:contentFile>
            </wcf:SendBillSync>
          </soap:Body>
        </soap:Envelope>
        """;

    private static string BuildGetStatusZipEnvelope(string cufe) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope"
                       xmlns:wcf="http://wcf.dian.colombia">
          <soap:Header/>
          <soap:Body>
            <wcf:GetStatusZip>
              <wcf:trackId>{cufe}</wcf:trackId>
            </wcf:GetStatusZip>
          </soap:Body>
        </soap:Envelope>
        """;

    private async Task<string> EnviarSoapAsync(string endpoint, string soapAction, string envelope)
    {
        using var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");
        content.Headers.Add("SOAPAction", soapAction);

        var response = await _httpClient.PostAsync(endpoint, content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("DIAN respondió HTTP {Status}: {Body}", response.StatusCode, body[..Math.Min(500, body.Length)]);
        }

        return body;
    }

    private static DianRespuesta ParsearRespuestaEnvio(string soapResponse)
    {
        try
        {
            var doc = XDocument.Parse(soapResponse);
            XNamespace wcf = "http://wcf.dian.colombia";

            // Buscar el resultado en SendBillSyncResult / XmlBase64Bytes
            var resultado = doc.Descendants(wcf + "SendBillSyncResult").FirstOrDefault()
                         ?? doc.Descendants(wcf + "XmlDocumentKey").FirstOrDefault();

            if (resultado == null)
            {
                // Puede ser un fault SOAP
                var fault = doc.Descendants("faultstring").FirstOrDefault();
                return new DianRespuesta(false, "FAULT", fault?.Value ?? "Respuesta inesperada de DIAN");
            }

            // La respuesta contiene Base64 de un XML con IsValid, StatusCode, StatusDescription
            var responseXmlBase64 = resultado.Value;
            if (string.IsNullOrEmpty(responseXmlBase64))
                return new DianRespuesta(false, "RESPUESTA_VACIA", "DIAN no retornó datos");

            var responseXmlBytes = Convert.FromBase64String(responseXmlBase64);
            var responseXml = XDocument.Parse(Encoding.UTF8.GetString(responseXmlBytes));

            var isValid = responseXml.Descendants("IsValid").FirstOrDefault()?.Value;
            var statusCode = responseXml.Descendants("StatusCode").FirstOrDefault()?.Value ?? "000";
            var statusMessage = responseXml.Descendants("StatusDescription").FirstOrDefault()?.Value ?? "";

            return new DianRespuesta(isValid == "true", statusCode, statusMessage);
        }
        catch (Exception ex)
        {
            return new DianRespuesta(false, "PARSE_ERROR", $"Error parseando respuesta DIAN: {ex.Message}");
        }
    }

    private static DianRespuesta ParsearRespuestaConsulta(string soapResponse)
    {
        try
        {
            var doc = XDocument.Parse(soapResponse);

            var isValid = doc.Descendants("IsValid").FirstOrDefault()?.Value;
            var statusCode = doc.Descendants("StatusCode").FirstOrDefault()?.Value ?? "000";
            var statusMessage = doc.Descendants("StatusDescription").FirstOrDefault()?.Value
                             ?? doc.Descendants("StatusMessage").FirstOrDefault()?.Value ?? "";

            return new DianRespuesta(isValid == "true", statusCode, statusMessage);
        }
        catch (Exception ex)
        {
            return new DianRespuesta(false, "PARSE_ERROR", $"Error parseando consulta DIAN: {ex.Message}");
        }
    }
}
