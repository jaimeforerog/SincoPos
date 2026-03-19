using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public class FacturacionService : IFacturacionService
{
    private readonly AppDbContext _context;
    private readonly IUblBuilderService _ublBuilder;
    private readonly IFirmaDigitalService _firmaDigital;
    private readonly IDianSoapService _dianSoap;
    private readonly ILogger<FacturacionService> _logger;
    private readonly INotificationService _notificationService;

    public FacturacionService(
        AppDbContext context,
        IUblBuilderService ublBuilder,
        IFirmaDigitalService firmaDigital,
        IDianSoapService dianSoap,
        ILogger<FacturacionService> logger,
        INotificationService notificationService)
    {
        _context = context;
        _ublBuilder = ublBuilder;
        _firmaDigital = firmaDigital;
        _dianSoap = dianSoap;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<(DocumentoElectronicoDto? doc, string? error)> EmitirFacturaVentaAsync(int ventaId)
    {
        // 1. Cargar venta con todas sus relaciones
        var venta = await _context.Ventas
            .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(v => v.Sucursal)
            .Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.Id == ventaId);

        if (venta == null)
            return (null, "NOT_FOUND");

        if (!venta.RequiereFacturaElectronica)
            return (null, "La venta no requiere factura electrónica (monto < 5 UVT).");

        // Verificar si ya existe documento para esta venta
        var existente = await _context.DocumentosElectronicos
            .FirstOrDefaultAsync(d => d.VentaId == ventaId && d.TipoDocumento == "FV");
        if (existente != null)
            return (MapToDto(existente, venta.Sucursal.Nombre), null);

        // 2. Cargar configuración del emisor
        var emisor = await _context.ConfiguracionesEmisor
            .FirstOrDefaultAsync(c => c.SucursalId == venta.SucursalId);
        if (emisor == null)
            return (null, "No hay configuración de emisor para esta sucursal. Configure en /facturacion/configuracion.");

        // Validar vigencia de la resolución
        var hoy = DateTime.UtcNow;
        if (hoy > emisor.FechaVigenciaHasta)
            return (null, $"La resolución DIAN venció el {emisor.FechaVigenciaHasta:dd/MM/yyyy}. Solicite una nueva resolución.");
        if (emisor.NumeroActual >= emisor.NumeroHasta)
            return (null, $"Se agotó el rango de numeración ({emisor.NumeroDesde}-{emisor.NumeroHasta}). Solicite nueva resolución.");

        // 3. Obtener siguiente número con bloqueo pesimista
        long siguienteNumero;
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Bloqueo a nivel de fila con raw SQL
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT id FROM public.configuracion_emisor WHERE id = {0} FOR UPDATE",
                emisor.Id);

            // Recargar para obtener el valor más reciente
            await _context.Entry(emisor).ReloadAsync();

            if (emisor.NumeroActual >= emisor.NumeroHasta)
            {
                await tx.RollbackAsync();
                return (null, "Rango de numeración agotado.");
            }

            emisor.NumeroActual++;
            siguienteNumero = emisor.NumeroActual;
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var numeroCompleto = $"{emisor.Prefijo}{siguienteNumero:D8}";

        // 4. Construir datos UBL
        var cliente = venta.Cliente;
        var ublData = new VentaUblData(
            NumeroCompleto: numeroCompleto,
            Prefijo: emisor.Prefijo,
            Numero: siguienteNumero,
            FechaEmision: venta.FechaVenta,
            SubtotalSinImpuestos: venta.Subtotal - venta.Descuento,
            TotalIva19: venta.Detalles.Where(d => d.PorcentajeImpuesto == 0.19m).Sum(d => d.MontoImpuesto),
            TotalIva5: venta.Detalles.Where(d => d.PorcentajeImpuesto == 0.05m).Sum(d => d.MontoImpuesto),
            TotalInc: 0m,
            Total: venta.Total,
            ClienteNit: cliente?.Identificacion,
            ClienteDV: cliente?.DigitoVerificacion,
            ClienteNombre: cliente?.Nombre,
            ClienteDireccion: cliente?.Direccion,
            ClienteCodigoMunicipio: cliente?.CodigoMunicipio,
            ClienteEmail: cliente?.Email,
            ClienteTipoDocumento: cliente != null ? MapTipoIdentificacion(cliente.TipoIdentificacion) : "13",
            ClientePerfilTributario: cliente?.PerfilTributario ?? "REGIMEN_COMUN",
            Lineas: venta.Detalles.Select((d, i) => new LineaUblData(
                NumeroLinea: i + 1,
                CodigoProducto: d.ProductoId.ToString(),
                DescripcionProducto: d.NombreProducto,
                UnidadMedida: d.Producto?.UnidadMedida ?? "94",
                Cantidad: d.Cantidad,
                PrecioUnitario: d.PrecioUnitario,
                Descuento: d.Descuento,
                Subtotal: d.Subtotal,
                PorcentajeIva: d.PorcentajeImpuesto * 100,
                MontoIva: d.MontoImpuesto
            )).ToList()
        );

        // 5. Generar XML UBL
        var (xmlSinFirmar, cufe) = _ublBuilder.GenerarFacturaVenta(ublData, ToEmisorData(emisor));

        // 6. Firmar XML
        string xmlFirmado;
        try
        {
            xmlFirmado = !string.IsNullOrEmpty(emisor.CertificadoBase64)
                ? _firmaDigital.FirmarXml(xmlSinFirmar, emisor.CertificadoBase64, emisor.CertificadoPassword)
                : xmlSinFirmar; // Sin certificado: modo prueba sin firma
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error firmando XML, guardando sin firma");
            xmlFirmado = xmlSinFirmar;
        }

        // 7. Guardar documento en estado Firmado
        var documento = new DocumentoElectronico
        {
            VentaId = ventaId,
            EmpresaId = venta.Sucursal.EmpresaId,
            SucursalId = venta.SucursalId,
            TipoDocumento = "FV",
            Prefijo = emisor.Prefijo,
            Numero = siguienteNumero,
            NumeroCompleto = numeroCompleto,
            Cufe = cufe,
            FechaEmision = venta.FechaVenta,
            XmlUbl = xmlFirmado,
            Estado = EstadoDocumento.Firmado
        };
        _context.DocumentosElectronicos.Add(documento);
        await _context.SaveChangesAsync();

        // 8. Enviar a DIAN
        if (!string.IsNullOrEmpty(emisor.CertificadoBase64))
        {
            try
            {
                var respuesta = await _dianSoap.EnviarDocumentoAsync(xmlFirmado, cufe, emisor.Nit, emisor.Ambiente);
                documento.FechaEnvioDian = DateTime.UtcNow;
                documento.CodigoRespuestaDian = respuesta.Codigo;
                documento.MensajeRespuestaDian = respuesta.Descripcion;
                documento.Intentos++;
                documento.Estado = respuesta.EsValido ? EstadoDocumento.Aceptado : EstadoDocumento.Rechazado;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Factura {Numero} enviada a DIAN. EsValido={EsValido}, Código={Codigo}",
                    numeroCompleto, respuesta.EsValido, respuesta.Codigo);

                var tipo = respuesta.EsValido ? "factura_aceptada" : "factura_rechazada";
                var nivel = respuesta.EsValido ? "success" : "error";
                await _notificationService.EnviarNotificacionSucursalAsync(documento.SucursalId,
                    new NotificacionDto(tipo, "Facturación DIAN",
                        respuesta.EsValido
                            ? $"Factura {numeroCompleto} aceptada por DIAN"
                            : $"Factura {numeroCompleto} rechazada: {respuesta.Descripcion}",
                        nivel, DateTime.UtcNow,
                        new { documento.Id, NumeroCompleto = numeroCompleto, respuesta.Codigo }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando factura {Numero} a DIAN", numeroCompleto);
                documento.Estado = EstadoDocumento.Rechazado;
                documento.MensajeRespuestaDian = ex.Message;
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Sin certificado: dejar en estado Firmado para envío manual posterior
            _logger.LogInformation(
                "Factura {Numero} generada en modo prueba (sin certificado digital)", numeroCompleto);
        }

        return (MapToDto(documento, venta.Sucursal.Nombre), null);
    }

    public async Task<(DocumentoElectronicoDto? doc, string? error)> EmitirNotaCreditoAsync(int devolucionId)
    {
        var devolucion = await _context.DevolucionesVenta
            .Include(d => d.Detalles)
                .ThenInclude(dd => dd.Producto)
            .Include(d => d.Venta)
                .ThenInclude(v => v.Sucursal)
            .Include(d => d.Venta)
                .ThenInclude(v => v.Cliente)
            .FirstOrDefaultAsync(d => d.Id == devolucionId);

        if (devolucion == null)
            return (null, "NOT_FOUND");

        // Buscar la factura electrónica de la venta original
        var facturaOriginal = await _context.DocumentosElectronicos
            .FirstOrDefaultAsync(d => d.VentaId == devolucion.VentaId && d.TipoDocumento == "FV");

        if (facturaOriginal == null)
            return (null, "No existe factura electrónica para la venta original.");

        var venta = devolucion.Venta;
        var emisor = await _context.ConfiguracionesEmisor
            .FirstOrDefaultAsync(c => c.SucursalId == venta.SucursalId);
        if (emisor == null)
            return (null, "No hay configuración de emisor para esta sucursal.");

        // Obtener siguiente número (mismo contador que FV por ahora, DIAN lo maneja por prefijo NC)
        long siguienteNumero;
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT id FROM public.configuracion_emisor WHERE id = {0} FOR UPDATE", emisor.Id);
            await _context.Entry(emisor).ReloadAsync();

            if (emisor.NumeroActual >= emisor.NumeroHasta)
            {
                await tx.RollbackAsync();
                return (null, "Rango de numeración agotado.");
            }

            emisor.NumeroActual++;
            siguienteNumero = emisor.NumeroActual;
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var prefijoNc = "NC";
        var numeroCompleto = $"{prefijoNc}{siguienteNumero:D8}";
        var cliente = venta.Cliente;

        var ublData = new DevolucionUblData(
            NumeroCompleto: numeroCompleto,
            Prefijo: prefijoNc,
            Numero: siguienteNumero,
            FechaEmision: devolucion.FechaDevolucion,
            CufeFacturaOriginal: facturaOriginal.Cufe,
            NumeroFacturaOriginal: facturaOriginal.NumeroCompleto,
            FechaFacturaOriginal: facturaOriginal.FechaEmision,
            MotivoDevolucion: devolucion.Motivo,
            SubtotalSinImpuestos: devolucion.TotalDevuelto,
            TotalIva19: devolucion.Detalles
                .Where(d => d.Producto?.Impuesto != null)
                .Sum(d => d.SubtotalDevuelto * 0.19m),
            TotalIva5: 0m,
            TotalInc: 0m,
            Total: devolucion.TotalDevuelto,
            ClienteNit: cliente?.Identificacion,
            ClienteDV: cliente?.DigitoVerificacion,
            ClienteNombre: cliente?.Nombre,
            ClienteDireccion: cliente?.Direccion,
            ClienteCodigoMunicipio: cliente?.CodigoMunicipio,
            ClienteEmail: cliente?.Email,
            ClienteTipoDocumento: cliente != null ? MapTipoIdentificacion(cliente.TipoIdentificacion) : "13",
            ClientePerfilTributario: cliente?.PerfilTributario ?? "REGIMEN_COMUN",
            Lineas: devolucion.Detalles.Select((d, i) => new LineaUblData(
                NumeroLinea: i + 1,
                CodigoProducto: d.ProductoId.ToString(),
                DescripcionProducto: d.NombreProducto,
                UnidadMedida: d.Producto?.UnidadMedida ?? "94",
                Cantidad: d.CantidadDevuelta,
                PrecioUnitario: d.PrecioUnitario,
                Descuento: 0m,
                Subtotal: d.SubtotalDevuelto,
                PorcentajeIva: 19m,
                MontoIva: d.SubtotalDevuelto * 0.19m
            )).ToList()
        );

        var (xmlSinFirmar, cufe) = _ublBuilder.GenerarNotaCredito(ublData, ToEmisorData(emisor));

        string xmlFirmado;
        try
        {
            xmlFirmado = !string.IsNullOrEmpty(emisor.CertificadoBase64)
                ? _firmaDigital.FirmarXml(xmlSinFirmar, emisor.CertificadoBase64, emisor.CertificadoPassword)
                : xmlSinFirmar;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error firmando Nota Crédito, guardando sin firma");
            xmlFirmado = xmlSinFirmar;
        }

        var documento = new DocumentoElectronico
        {
            VentaId = venta.Id,
            EmpresaId = venta.Sucursal.EmpresaId,
            SucursalId = venta.SucursalId,
            TipoDocumento = "NC",
            Prefijo = prefijoNc,
            Numero = siguienteNumero,
            NumeroCompleto = numeroCompleto,
            Cufe = cufe,
            FechaEmision = devolucion.FechaDevolucion,
            XmlUbl = xmlFirmado,
            Estado = EstadoDocumento.Firmado
        };
        _context.DocumentosElectronicos.Add(documento);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(emisor.CertificadoBase64))
        {
            try
            {
                var respuesta = await _dianSoap.EnviarDocumentoAsync(xmlFirmado, cufe, emisor.Nit, emisor.Ambiente);
                documento.FechaEnvioDian = DateTime.UtcNow;
                documento.CodigoRespuestaDian = respuesta.Codigo;
                documento.MensajeRespuestaDian = respuesta.Descripcion;
                documento.Intentos++;
                documento.Estado = respuesta.EsValido ? EstadoDocumento.Aceptado : EstadoDocumento.Rechazado;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando Nota Crédito {Numero} a DIAN", numeroCompleto);
                documento.Estado = EstadoDocumento.Rechazado;
                documento.MensajeRespuestaDian = ex.Message;
                await _context.SaveChangesAsync();
            }
        }

        return (MapToDto(documento, venta.Sucursal.Nombre), null);
    }

    public async Task<PaginatedResult<DocumentoElectronicoDto>> ListarAsync(FiltroDocumentosElectronicosDto filtro)
    {
        var query = _context.DocumentosElectronicos
            .Include(d => d.Sucursal)
            .AsQueryable();

        if (filtro.SucursalId.HasValue)
            query = query.Where(d => d.SucursalId == filtro.SucursalId.Value);
        if (filtro.FechaDesde.HasValue)
            query = query.Where(d => d.FechaEmision >= filtro.FechaDesde.Value);
        if (filtro.FechaHasta.HasValue)
            query = query.Where(d => d.FechaEmision <= filtro.FechaHasta.Value);
        if (!string.IsNullOrEmpty(filtro.TipoDocumento))
            query = query.Where(d => d.TipoDocumento == filtro.TipoDocumento);
        if (filtro.Estado.HasValue)
            query = query.Where(d => (int)d.Estado == filtro.Estado.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.FechaEmision)
            .Skip((filtro.PageNumber - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .Select(d => MapToDto(d, d.Sucursal.Nombre))
            .ToListAsync();

        return new PaginatedResult<DocumentoElectronicoDto>(
            items, total, filtro.PageNumber, filtro.PageSize,
            (int)Math.Ceiling(total / (double)filtro.PageSize));
    }

    public async Task<DocumentoElectronicoDto?> ObtenerAsync(int id)
    {
        var doc = await _context.DocumentosElectronicos
            .Include(d => d.Sucursal)
            .FirstOrDefaultAsync(d => d.Id == id);
        return doc == null ? null : MapToDto(doc, doc.Sucursal.Nombre);
    }

    public async Task<string?> ObtenerXmlAsync(int id)
    {
        var doc = await _context.DocumentosElectronicos.FindAsync(id);
        return doc?.XmlUbl;
    }

    public async Task<(DocumentoElectronicoDto? doc, string? error)> ReintentarAsync(int documentoId)
    {
        var documento = await _context.DocumentosElectronicos
            .Include(d => d.Sucursal)
            .FirstOrDefaultAsync(d => d.Id == documentoId);

        if (documento == null)
            return (null, "NOT_FOUND");

        if (documento.Estado == EstadoDocumento.Aceptado)
            return (null, "El documento ya fue aceptado por DIAN.");

        var emisor = await _context.ConfiguracionesEmisor
            .FirstOrDefaultAsync(c => c.SucursalId == documento.SucursalId);
        if (emisor == null)
            return (null, "No hay configuración de emisor para esta sucursal.");

        var respuesta = await _dianSoap.EnviarDocumentoAsync(
            documento.XmlUbl, documento.Cufe, emisor.Nit, emisor.Ambiente);

        documento.FechaEnvioDian = DateTime.UtcNow;
        documento.CodigoRespuestaDian = respuesta.Codigo;
        documento.MensajeRespuestaDian = respuesta.Descripcion;
        documento.Intentos++;
        documento.Estado = respuesta.EsValido ? EstadoDocumento.Aceptado : EstadoDocumento.Rechazado;
        await _context.SaveChangesAsync();

        return (MapToDto(documento, documento.Sucursal.Nombre), null);
    }

    public async Task<DianRespuesta?> ConsultarEstadoDianAsync(int documentoId)
    {
        var documento = await _context.DocumentosElectronicos.FindAsync(documentoId);
        if (documento == null) return null;

        var emisor = await _context.ConfiguracionesEmisor
            .FirstOrDefaultAsync(c => c.SucursalId == documento.SucursalId);
        if (emisor == null) return null;

        return await _dianSoap.ConsultarEstadoAsync(documento.Cufe, emisor.Ambiente);
    }

    public async Task<ConfiguracionEmisorDto?> ObtenerConfiguracionAsync(int sucursalId)
    {
        var config = await _context.ConfiguracionesEmisor
            .Include(c => c.Sucursal)
            .FirstOrDefaultAsync(c => c.SucursalId == sucursalId);
        return config == null ? null : MapConfigToDto(config);
    }

    public async Task ActualizarConfiguracionAsync(int sucursalId, ActualizarConfiguracionEmisorDto dto)
    {
        var config = await _context.ConfiguracionesEmisor
            .FirstOrDefaultAsync(c => c.SucursalId == sucursalId);

        if (config == null)
        {
            config = new ConfiguracionEmisor { SucursalId = sucursalId };
            _context.ConfiguracionesEmisor.Add(config);
        }

        config.Nit = dto.Nit;
        config.DigitoVerificacion = dto.DigitoVerificacion;
        config.RazonSocial = dto.RazonSocial;
        config.NombreComercial = dto.NombreComercial;
        config.Direccion = dto.Direccion;
        config.CodigoMunicipio = dto.CodigoMunicipio;
        config.CodigoDepartamento = dto.CodigoDepartamento;
        config.Telefono = dto.Telefono;
        config.Email = dto.Email;
        config.CodigoCiiu = dto.CodigoCiiu;
        config.PerfilTributario = dto.PerfilTributario;
        config.NumeroResolucion = dto.NumeroResolucion;
        config.FechaResolucion = dto.FechaResolucion;
        config.Prefijo = dto.Prefijo;
        config.NumeroDesde = dto.NumeroDesde;
        config.NumeroHasta = dto.NumeroHasta;
        config.FechaVigenciaDesde = dto.FechaVigenciaDesde;
        config.FechaVigenciaHasta = dto.FechaVigenciaHasta;
        config.Ambiente = dto.Ambiente;
        config.PinSoftware = dto.PinSoftware;
        config.IdSoftware = dto.IdSoftware;

        if (!string.IsNullOrEmpty(dto.CertificadoBase64))
            config.CertificadoBase64 = dto.CertificadoBase64;
        if (!string.IsNullOrEmpty(dto.CertificadoPassword))
            config.CertificadoPassword = dto.CertificadoPassword;

        await _context.SaveChangesAsync();
    }

    // ─── Mappers ──────────────────────────────────────────────────────────────

    private static EmisorUblData ToEmisorData(ConfiguracionEmisor c) =>
        new(c.Nit, c.DigitoVerificacion, c.RazonSocial, c.NombreComercial,
            c.Direccion, c.CodigoMunicipio, c.CodigoDepartamento,
            c.Telefono, c.Email, c.CodigoCiiu, c.PerfilTributario,
            c.NumeroResolucion, c.FechaVigenciaDesde, c.FechaVigenciaHasta,
            c.Prefijo, c.NumeroDesde, c.NumeroHasta,
            c.Ambiente, c.PinSoftware, c.IdSoftware,
            c.CertificadoBase64, c.CertificadoPassword);

    private static DocumentoElectronicoDto MapToDto(DocumentoElectronico d, string nombreSucursal) =>
        new(d.Id, d.VentaId, d.SucursalId, nombreSucursal, d.TipoDocumento, d.Prefijo,
            d.Numero, d.NumeroCompleto, d.Cufe, d.FechaEmision,
            d.Estado.ToString(), (int)d.Estado,
            d.FechaEnvioDian, d.CodigoRespuestaDian, d.MensajeRespuestaDian,
            d.Intentos, d.FechaCreacion);

    private static ConfiguracionEmisorDto MapConfigToDto(ConfiguracionEmisor c) =>
        new(c.Id, c.SucursalId, c.Sucursal?.Nombre ?? "",
            c.Nit, c.DigitoVerificacion, c.RazonSocial, c.NombreComercial,
            c.Direccion, c.CodigoMunicipio, c.CodigoDepartamento,
            c.Telefono, c.Email, c.CodigoCiiu, c.PerfilTributario,
            c.NumeroResolucion, c.FechaResolucion, c.Prefijo,
            c.NumeroDesde, c.NumeroHasta, c.NumeroActual,
            c.FechaVigenciaDesde, c.FechaVigenciaHasta,
            c.Ambiente,
            TienePinSoftware: !string.IsNullOrEmpty(c.PinSoftware),
            c.IdSoftware,
            TieneCertificado: !string.IsNullOrEmpty(c.CertificadoBase64));

    private static string MapTipoIdentificacion(TipoIdentificacion tipo) => tipo switch
    {
        TipoIdentificacion.NIT => "31",
        TipoIdentificacion.CC => "13",
        TipoIdentificacion.CE => "22",
        _ => "13"
    };
}
