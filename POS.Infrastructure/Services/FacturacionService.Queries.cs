using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed partial class FacturacionService
{
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

    // ─── ConfiguracionEmisor ─────────────────────────────────────────────────

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
}
