using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Infrastructure.Services.Erp;

/// <summary>
/// Cliente ERP simulado para desarrollo local. Genera referencias ficticias
/// sin contactar ningún servicio externo. Se registra en lugar de SincoErpClient
/// cuando ErpSinco:BaseUrl está vacío o no configurado.
/// </summary>
public class MockErpClient : IErpClient
{
    private readonly ILogger<MockErpClient> _logger;
    private static int _secuencia = 1000;

    public MockErpClient(ILogger<MockErpClient> logger)
    {
        _logger = logger;
    }

    public Task<ErpResponse> ContabilizarCompraAsync(CompraErpPayload payload)
    {
        var referencia = $"MOCK-OC-{Interlocked.Increment(ref _secuencia)}";
        _logger.LogWarning(
            "[MockErpClient] Simulando contabilización de OC {NumeroOrden} → Ref: {Referencia}. " +
            "Configure ErpSinco:BaseUrl para conectar con el ERP real",
            payload.NumeroOrden, referencia);

        return Task.FromResult(new ErpResponse(
            Exitoso: true,
            ErpReferencia: referencia,
            MensajeError: null
        ));
    }

    public Task<ErpResponse> ContabilizarVentaAsync(VentaErpPayload payload)
    {
        var referencia = $"MOCK-VTA-{Interlocked.Increment(ref _secuencia)}";
        _logger.LogWarning(
            "[MockErpClient] Simulando contabilización de venta {NumeroVenta} → Ref: {Referencia}. " +
            "Configure ErpSinco:BaseUrl para conectar con el ERP real",
            payload.NumeroVenta, referencia);

        return Task.FromResult(new ErpResponse(
            Exitoso: true,
            ErpReferencia: referencia,
            MensajeError: null
        ));
    }

    public Task<ErpResponse> ConsultarEstadoDocumentoAsync(string erpReferencia)
    {
        _logger.LogWarning("[MockErpClient] Simulando consulta de documento {Referencia}", erpReferencia);
        return Task.FromResult(new ErpResponse(true, erpReferencia, null));
    }
}
