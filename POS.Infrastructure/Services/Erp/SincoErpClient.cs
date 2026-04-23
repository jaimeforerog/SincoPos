using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Infrastructure.Services.Erp;

public sealed class SincoErpClient : IErpClient
{
    private readonly HttpClient _httpClient;
    private readonly ErpSincoOptions _options;
    private readonly ILogger<SincoErpClient> _logger;

    public SincoErpClient(
        HttpClient httpClient,
        IOptions<ErpSincoOptions> options,
        ILogger<SincoErpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ErpResponse> ContabilizarCompraAsync(CompraErpPayload payload)
    {
        try
        {
            _logger.LogInformation("Enviando orden de compra {NumeroOrden} al ERP Sinco", payload.NumeroOrden);

            var response = await _httpClient.PostAsJsonAsync("api/v1/comprobantes/compras", payload);

            if (response.IsSuccessStatusCode)
            {
                // Parsear la referencia del documento creado en el ERP
                var body = await response.Content.ReadFromJsonAsync<SincoCompraResponse>();
                var referencia = body?.Referencia ?? body?.Id ?? $"SINCO-{response.Headers.Location?.Segments.LastOrDefault()}";

                return new ErpResponse(
                    Exitoso: true,
                    ErpReferencia: referencia,
                    MensajeError: null
                );
            }

            _logger.LogError("Fallo al enviar OC {NumeroOrden}: HTTP {Code}", payload.NumeroOrden, response.StatusCode);
            var err = await response.Content.ReadAsStringAsync();
            return new ErpResponse(false, null, $"Error HTTP {response.StatusCode}: {err}");
        }
        catch (TaskCanceledException)
        {
            return new ErpResponse(false, null, "Timeout conectando con el ERP");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico conectando con el ERP Sinco");
            return new ErpResponse(false, null, ex.Message);
        }
    }

    public async Task<ErpResponse> ContabilizarVentaAsync(VentaErpPayload payload)
    {
        try
        {
            _logger.LogInformation("Enviando venta {NumeroVenta} al ERP Sinco", payload.NumeroVenta);

            var response = await _httpClient.PostAsJsonAsync("api/v1/comprobantes/ventas", payload);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<SincoCompraResponse>();
                var referencia = body?.Referencia ?? body?.Id ?? $"SINCO-V-{response.Headers.Location?.Segments.LastOrDefault()}";
                return new ErpResponse(Exitoso: true, ErpReferencia: referencia, MensajeError: null);
            }

            _logger.LogError("Fallo al enviar venta {NumeroVenta}: HTTP {Code}", payload.NumeroVenta, response.StatusCode);
            var err = await response.Content.ReadAsStringAsync();
            return new ErpResponse(false, null, $"Error HTTP {response.StatusCode}: {err}");
        }
        catch (TaskCanceledException)
        {
            return new ErpResponse(false, null, "Timeout conectando con el ERP");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico conectando con el ERP Sinco (venta)");
            return new ErpResponse(false, null, ex.Message);
        }
    }

    /// <summary>
    /// Estructura de respuesta esperada del ERP Sinco al crear un comprobante.
    /// </summary>
    private record SincoCompraResponse(string? Id, string? Referencia);

    public async Task<ErpResponse> ConsultarEstadoDocumentoAsync(string erpReferencia)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/comprobantes/{erpReferencia}");
            if (response.IsSuccessStatusCode)
            {
                return new ErpResponse(true, erpReferencia, null);
            }
            return new ErpResponse(false, erpReferencia, "No encontrado en ERP");
        }
        catch (Exception ex)
        {
            return new ErpResponse(false, erpReferencia, ex.Message);
        }
    }
}
