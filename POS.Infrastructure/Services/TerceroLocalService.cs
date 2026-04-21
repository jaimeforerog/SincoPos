using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

/// <summary>
/// Implementacion local del servicio de terceros.
/// Almacena los datos directamente en la base de datos del POS.
/// </summary>
public class TerceroLocalService : ITerceroService
{
    private readonly AppDbContext _context;
    private readonly ICurrentEmpresaProvider _empresaProvider;

    public TerceroLocalService(AppDbContext context, ICurrentEmpresaProvider empresaProvider)
    {
        _context = context;
        _empresaProvider = empresaProvider;
    }

    public async Task<TerceroDto?> ObtenerPorIdAsync(int id)
    {
        var t = await _context.Terceros
            .IgnoreQueryFilters() // Permitir ver por ID incluso si está inactivo
            .Include(x => x.Actividades)
            .FirstOrDefaultAsync(x => x.Id == id);

        return t == null ? null : MapToDtoLocal(t);
    }

    public async Task<TerceroDto?> ObtenerPorIdentificacionAsync(string identificacion)
    {
        var t = await _context.Terceros
            .IgnoreQueryFilters() // Permitir buscar por Identificación incluso si está inactivo
            .Include(x => x.Actividades)
            .FirstOrDefaultAsync(x => x.Identificacion == identificacion);

        return t == null ? null : MapToDtoLocal(t);
    }

    public async Task<PaginatedResult<TerceroDto>> BuscarAsync(string? query, string? tipoTercero, bool incluirInactivos, int page = 1, int pageSize = 50)
    {
        var q = incluirInactivos
            ? _context.Terceros.IgnoreQueryFilters().Include(x => x.Actividades)
            : (IQueryable<Tercero>)_context.Terceros.Include(x => x.Actividades).Where(t => t.Activo);

        if (!string.IsNullOrEmpty(tipoTercero) && Enum.TryParse<TipoTercero>(tipoTercero, true, out var tipo))
        {
            q = tipo == TipoTercero.Ambos
                ? q
                : q.Where(t => t.TipoTercero == tipo || t.TipoTercero == TipoTercero.Ambos);
        }

        if (!string.IsNullOrEmpty(query))
        {
            var lower = query.ToLower();
            q = q.Where(t =>
                t.Nombre.ToLower().Contains(lower) ||
                t.Identificacion.Contains(query));
        }

        var totalCount = await q.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var list = await q.OrderBy(t => t.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = list.Select(MapToDtoLocal).ToList();
        return new PaginatedResult<TerceroDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<(TerceroDto? Result, string? Error)> CrearAsync(CrearTerceroDto dto)
    {
        if (!Enum.TryParse<TipoIdentificacion>(dto.TipoIdentificacion, true, out var tipoId))
            return (null, $"Tipo de identificacion invalido. Valores: {string.Join(", ", Enum.GetNames<TipoIdentificacion>())}");

        if (!Enum.TryParse<TipoTercero>(dto.TipoTercero, true, out var tipoTercero))
            return (null, $"Tipo de tercero invalido. Valores: {string.Join(", ", Enum.GetNames<TipoTercero>())}");

        var existe = await _context.Terceros.AnyAsync(t => t.Identificacion == dto.Identificacion);
        if (existe)
            return (null, $"Ya existe un tercero con identificacion {dto.Identificacion}.");

        var tercero = new Tercero
        {
            TipoIdentificacion = tipoId,
            Identificacion = dto.Identificacion,
            Nombre = dto.Nombre,
            TipoTercero = tipoTercero,
            Telefono = dto.Telefono,
            Email = dto.Email,
            Direccion = dto.Direccion,
            Ciudad = dto.Ciudad,
            CodigoDepartamento = dto.CodigoDepartamento,
            CodigoMunicipio = dto.CodigoMunicipio,
            PerfilTributario = dto.PerfilTributario ?? "REGIMEN_COMUN",
            EsGranContribuyente = dto.EsGranContribuyente,
            EsAutorretenedor = dto.EsAutorretenedor,
            EsResponsableIVA = dto.EsResponsableIVA,
            EmpresaId = _empresaProvider.EmpresaId ?? throw new InvalidOperationException("EmpresaId es requerido."),
            OrigenDatos = OrigenDatos.Local,
        };

        // Auto-calcular DV para NIT
        if (tipoId == TipoIdentificacion.NIT)
            tercero.DigitoVerificacion = CalcularDV(dto.Identificacion);

        _context.Terceros.Add(tercero);
        await _context.SaveChangesAsync();

        return (MapToDtoLocal(tercero), null);
    }

    public async Task<(bool Success, string? Error)> ActualizarAsync(int id, ActualizarTerceroDto dto)
    {
        var tercero = await _context.Terceros.FindAsync(id);
        if (tercero == null)
            return (false, $"Tercero {id} no encontrado.");

        if (tercero.OrigenDatos == OrigenDatos.ERP)
            return (false, "No se pueden modificar terceros que provienen del ERP.");

        tercero.Nombre = dto.Nombre;
        tercero.Telefono = dto.Telefono;
        tercero.Email = dto.Email;
        tercero.Direccion = dto.Direccion;
        tercero.Ciudad = dto.Ciudad;
        tercero.CodigoDepartamento = dto.CodigoDepartamento;
        tercero.CodigoMunicipio = dto.CodigoMunicipio;
        tercero.EsGranContribuyente = dto.EsGranContribuyente;
        tercero.EsAutorretenedor = dto.EsAutorretenedor;
        tercero.EsResponsableIVA = dto.EsResponsableIVA;
        tercero.FechaModificacion = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.PerfilTributario))
            tercero.PerfilTributario = dto.PerfilTributario;

        if (!string.IsNullOrEmpty(dto.TipoTercero))
        {
            if (!Enum.TryParse<TipoTercero>(dto.TipoTercero, true, out var tipo))
                return (false, $"Tipo de tercero invalido. Valores: {string.Join(", ", Enum.GetNames<TipoTercero>())}");
            tercero.TipoTercero = tipo;
        }

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DesactivarAsync(int id)
    {
        var tercero = await _context.Terceros
            .IgnoreQueryFilters() // Buscar incluso si ya está inactivo
            .FirstOrDefaultAsync(t => t.Id == id);
        
        if (tercero == null)
            return (false, $"Tercero {id} no encontrado.");

        if (!tercero.Activo)
            return (false, $"Tercero {id} ya está inactivo.");

        tercero.Activo = false;
        tercero.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ActivarAsync(int id)
    {
        var tercero = await _context.Terceros
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tercero == null)
            return (false, $"Tercero {id} no encontrado.");

        if (tercero.Activo)
            return (false, $"Tercero {id} ya está activo.");

        tercero.Activo = true;
        tercero.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    // ── Actividades CIIU ──────────────────────────────────────────────────────

    public async Task<(TerceroActividadDto? Result, string? Error)> AgregarActividadAsync(int terceroId, AgregarActividadDto dto)
    {
        var tercero = await _context.Terceros
            .Include(t => t.Actividades)
            .FirstOrDefaultAsync(t => t.Id == terceroId);

        if (tercero == null)
            return (null, $"Tercero {terceroId} no encontrado.");

        var existe = tercero.Actividades.Any(a => a.CodigoCIIU == dto.CodigoCIIU);
        if (existe)
            return (null, $"El tercero ya tiene la actividad CIIU {dto.CodigoCIIU}.");

        // Si se marca como principal, quitar la anterior
        if (dto.EsPrincipal)
        {
            foreach (var act in tercero.Actividades.Where(a => a.EsPrincipal))
                act.EsPrincipal = false;
        }

        var actividad = new TerceroActividad
        {
            TerceroId = terceroId,
            CodigoCIIU = dto.CodigoCIIU,
            Descripcion = dto.Descripcion,
            EsPrincipal = dto.EsPrincipal,
        };

        _context.TerceroActividades.Add(actividad);
        await _context.SaveChangesAsync();

        return (new TerceroActividadDto(actividad.Id, actividad.CodigoCIIU, actividad.Descripcion, actividad.EsPrincipal), null);
    }

    public async Task<(bool Success, string? Error)> EliminarActividadAsync(int terceroId, int actividadId)
    {
        var actividad = await _context.TerceroActividades
            .FirstOrDefaultAsync(a => a.Id == actividadId && a.TerceroId == terceroId);

        if (actividad == null)
            return (false, $"Actividad {actividadId} no encontrada para el tercero {terceroId}.");

        _context.TerceroActividades.Remove(actividad);
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> EstablecerPrincipalAsync(int terceroId, int actividadId)
    {
        var actividades = await _context.TerceroActividades
            .Where(a => a.TerceroId == terceroId)
            .ToListAsync();

        if (!actividades.Any())
            return (false, $"El tercero {terceroId} no tiene actividades.");

        var target = actividades.FirstOrDefault(a => a.Id == actividadId);
        if (target == null)
            return (false, $"Actividad {actividadId} no encontrada para el tercero {terceroId}.");

        foreach (var act in actividades)
            act.EsPrincipal = act.Id == actividadId;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    // ── Importación Excel ─────────────────────────────────────────────────────

    /// <summary>
    /// Columnas esperadas en la plantilla (en orden, fila 1 = encabezado, fila 2+ = datos).
    /// </summary>
    private static readonly string[] Columnas =
    [
        "TipoIdentificacion", "Identificacion", "Nombre", "TipoTercero",
        "Telefono", "Email", "Direccion", "Ciudad",
        "CodigoDepartamento", "CodigoMunicipio", "PerfilTributario",
        "EsGranContribuyente", "EsAutorretenedor", "EsResponsableIVA",
    ];

    /// <summary>
    /// Departamentos de Colombia con sus municipios principales.
    /// Formato municipio: "CÓDIGO - Nombre"
    /// </summary>
    private static readonly (string Code, string Name, string[] Municipios)[] DatosGeo =
    [
        ("05", "Antioquia",            ["05001 - Medellín", "05088 - Bello", "05129 - Caldas", "05170 - Caucasia", "05250 - Envigado", "05266 - Girardota", "05308 - Guarne", "05315 - Itagüí", "05360 - La Ceja", "05380 - La Estrella", "05440 - Marinilla", "05631 - Rionegro", "05697 - Sabaneta", "05756 - Turbo"]),
        ("08", "Atlántico",            ["08001 - Barranquilla", "08078 - Baranoa", "08296 - Galapa", "08433 - Malambo", "08520 - Palmar de Varela", "08606 - Puerto Colombia", "08634 - Repelón", "08675 - Sabanagrande", "08685 - Sabanalarga", "08758 - Soledad"]),
        ("11", "Bogotá D.C.",          ["11001 - Bogotá D.C."]),
        ("13", "Bolívar",              ["13001 - Cartagena", "13244 - El Carmen de Bolívar", "13430 - Magangué", "13468 - Mompox", "13836 - Turbaco"]),
        ("15", "Boyacá",               ["15001 - Tunja", "15176 - Chiquinquirá", "15238 - Duitama", "15516 - Paipa", "15762 - Sogamoso"]),
        ("17", "Caldas",               ["17001 - Manizales", "17380 - La Dorada", "17614 - Riosucio", "17653 - Salamina", "17873 - Villamaría"]),
        ("18", "Caquetá",              ["18001 - Florencia", "18592 - Puerto Rico", "18753 - San Vicente del Caguán"]),
        ("19", "Cauca",                ["19001 - Popayán", "19212 - Corinto", "19573 - Puerto Tejada", "19698 - Santander de Quilichao"]),
        ("20", "Cesar",                ["20001 - Valledupar", "20011 - Aguachica", "20060 - Bosconia", "20175 - Codazzi", "20228 - Curumaní"]),
        ("23", "Córdoba",              ["23001 - Montería", "23162 - Cereté", "23417 - Lorica", "23466 - Montelíbano", "23660 - Sahagún"]),
        ("25", "Cundinamarca",         ["25175 - Chía", "25269 - Facatativá", "25306 - Funza", "25307 - Fusagasugá", "25430 - Madrid", "25473 - Mosquera", "25754 - Soacha", "25899 - Zipaquirá"]),
        ("27", "Chocó",                ["27001 - Quibdó", "27075 - Bahía Solano", "27361 - Istmina"]),
        ("41", "Huila",                ["41001 - Neiva", "41298 - Garzón", "41396 - La Plata", "41551 - Pitalito"]),
        ("44", "La Guajira",           ["44001 - Riohacha", "44430 - Maicao", "44855 - Uribia"]),
        ("47", "Magdalena",            ["47001 - Santa Marta", "47189 - Ciénaga", "47245 - El Banco", "47318 - Fundación"]),
        ("50", "Meta",                 ["50001 - Villavicencio", "50006 - Acacías", "50313 - Granada", "50577 - Puerto López"]),
        ("52", "Nariño",               ["52001 - Pasto", "52356 - Ipiales", "52405 - La Unión", "52835 - Tumaco"]),
        ("54", "Norte de Santander",   ["54001 - Cúcuta", "54405 - Los Patios", "54498 - Ocaña", "54874 - Villa del Rosario"]),
        ("63", "Quindío",              ["63001 - Armenia", "63130 - Calarcá", "63401 - La Tebaida", "63470 - Montenegro"]),
        ("66", "Risaralda",            ["66001 - Pereira", "66170 - Dosquebradas", "66400 - La Virginia", "66682 - Santa Rosa de Cabal"]),
        ("68", "Santander",            ["68001 - Bucaramanga", "68081 - Barrancabermeja", "68276 - Floridablanca", "68307 - Girón", "68547 - Piedecuesta"]),
        ("70", "Sucre",                ["70001 - Sincelejo", "70215 - Corozal", "70670 - Sampués", "70702 - San Marcos"]),
        ("73", "Tolima",               ["73001 - Ibagué", "73268 - Espinal", "73349 - Honda", "73449 - Melgar"]),
        ("76", "Valle del Cauca",      ["76001 - Cali", "76109 - Buenaventura", "76111 - Buga", "76147 - Cartago", "76364 - Jamundí", "76520 - Palmira", "76834 - Tuluá"]),
        ("81", "Arauca",               ["81001 - Arauca", "81065 - Arauquita", "81736 - Saravena", "81794 - Tame"]),
        ("85", "Casanare",             ["85001 - Yopal", "85010 - Aguazul", "85400 - Trinidad", "85440 - Villanueva"]),
        ("86", "Putumayo",             ["86001 - Mocoa", "86568 - Puerto Asís", "86573 - Puerto Leguízamo"]),
        ("88", "San Andrés",           ["88001 - San Andrés", "88564 - Providencia"]),
        ("91", "Amazonas",             ["91001 - Leticia", "91540 - Puerto Nariño"]),
        ("94", "Guainía",              ["94001 - Inírida"]),
        ("95", "Guaviare",             ["95001 - San José del Guaviare", "95015 - Calamar", "95200 - Miraflores"]),
        ("97", "Vaupés",               ["97001 - Mitú"]),
        ("99", "Vichada",              ["99001 - Puerto Carreño", "99524 - La Primavera"]),
    ];

    public async Task<ResultadoImportacionTercerosDto> ImportarDesdeExcelAsync(Stream stream)
    {
        var resultado = new ResultadoImportacionTercerosDto();

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        // Localizar columnas por encabezado (fila 1)
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? Columnas.Length;
        for (int c = 1; c <= lastCol; c++)
        {
            var header = ws.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(header))
                colMap[header] = c;
        }

        string Get(IXLRow row, string col)
            => colMap.TryGetValue(col, out var c) ? row.Cell(c).GetString().Trim() : string.Empty;

        bool GetBool(IXLRow row, string col)
        {
            var v = Get(row, col).ToLower();
            return v is "si" or "sí" or "true" or "1" or "yes" or "x";
        }

        // Extrae solo el código de valores con formato "CÓDIGO - Nombre" (dropdown)
        string ParseCodigo(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var dashIdx = value.IndexOf(" - ", StringComparison.Ordinal);
            return dashIdx >= 0 ? value[..dashIdx].Trim() : value.Trim();
        }

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        resultado.TotalFilas = Math.Max(0, lastRow - 1);

        for (int r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var identificacion = Get(row, "Identificacion");
            var nombre = Get(row, "Nombre");

            // Fila completamente vacía → ignorar sin contar
            if (string.IsNullOrEmpty(identificacion) && string.IsNullOrEmpty(nombre))
            {
                resultado.TotalFilas--;
                continue;
            }

            var filaResult = new ResultadoFilaTerceroDto
            {
                Fila = r,
                Identificacion = identificacion,
                Nombre = nombre,
            };

            // Validaciones básicas
            if (string.IsNullOrEmpty(identificacion)) { filaResult.Estado = "Error"; filaResult.Mensaje = "Identificación vacía."; resultado.Errores++; resultado.Filas.Add(filaResult); continue; }
            if (string.IsNullOrEmpty(nombre)) { filaResult.Estado = "Error"; filaResult.Mensaje = "Nombre vacío."; resultado.Errores++; resultado.Filas.Add(filaResult); continue; }

            var tipoIdStr = Get(row, "TipoIdentificacion");
            if (!Enum.TryParse<TipoIdentificacion>(tipoIdStr, true, out var tipoId))
            {
                filaResult.Estado = "Error";
                filaResult.Mensaje = $"TipoIdentificacion inválido: '{tipoIdStr}'. Válidos: {string.Join(", ", Enum.GetNames<TipoIdentificacion>())}";
                resultado.Errores++;
                resultado.Filas.Add(filaResult);
                continue;
            }

            var tipoTerceroStr = Get(row, "TipoTercero");
            if (!Enum.TryParse<TipoTercero>(tipoTerceroStr, true, out var tipoTercero))
            {
                filaResult.Estado = "Error";
                filaResult.Mensaje = $"TipoTercero inválido: '{tipoTerceroStr}'. Válidos: {string.Join(", ", Enum.GetNames<TipoTercero>())}";
                resultado.Errores++;
                resultado.Filas.Add(filaResult);
                continue;
            }

            // ¿Ya existe?
            var existe = await _context.Terceros.AnyAsync(t => t.Identificacion == identificacion);
            if (existe)
            {
                filaResult.Estado = "Omitido";
                filaResult.Mensaje = "Ya existe un tercero con esta identificación.";
                resultado.Omitidos++;
                resultado.Filas.Add(filaResult);
                continue;
            }

            var perfilTributario = Get(row, "PerfilTributario");
            if (string.IsNullOrEmpty(perfilTributario)) perfilTributario = "REGIMEN_COMUN";

            var tercero = new Tercero
            {
                TipoIdentificacion = tipoId,
                Identificacion = identificacion,
                Nombre = nombre,
                TipoTercero = tipoTercero,
                Telefono = Get(row, "Telefono").NullIfEmpty(),
                Email = Get(row, "Email").NullIfEmpty(),
                Direccion = Get(row, "Direccion").NullIfEmpty(),
                Ciudad = Get(row, "Ciudad").NullIfEmpty(),
                CodigoDepartamento = ParseCodigo(Get(row, "CodigoDepartamento")).NullIfEmpty(),
                CodigoMunicipio = ParseCodigo(Get(row, "CodigoMunicipio")).NullIfEmpty(),
                PerfilTributario = perfilTributario,
                EsGranContribuyente = GetBool(row, "EsGranContribuyente"),
                EsAutorretenedor = GetBool(row, "EsAutorretenedor"),
                EsResponsableIVA = GetBool(row, "EsResponsableIVA"),
                EmpresaId = _empresaProvider.EmpresaId ?? throw new InvalidOperationException("EmpresaId es requerido."),
                OrigenDatos = OrigenDatos.Local,
            };

            if (tipoId == TipoIdentificacion.NIT)
                tercero.DigitoVerificacion = CalcularDV(identificacion);

            _context.Terceros.Add(tercero);

            try
            {
                await _context.SaveChangesAsync();
                filaResult.Estado = "Importado";
                resultado.Importados++;
            }
            catch (Exception ex)
            {
                _context.Entry(tercero).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                filaResult.Estado = "Error";
                filaResult.Mensaje = ex.InnerException?.Message ?? ex.Message;
                resultado.Errores++;
            }

            resultado.Filas.Add(filaResult);
        }

        return resultado;
    }

    public byte[] GenerarPlantillaExcel()
    {
        using var wb = new XLWorkbook();

        // ── 1. Hoja Terceros (principal, posición 1) ──────────────────────────
        var ws = wb.Worksheets.Add("Terceros");

        // ── 2. Hoja Referencia (posición 2) ───────────────────────────────────
        var wsRef = wb.Worksheets.Add("Referencia");

        // ── 3. Hoja Listas (oculta, posición 3) con datos geo ─────────────────
        var wsListas = wb.Worksheets.Add("Listas");
        wsListas.Visibility = XLWorksheetVisibility.Hidden;

        // Encabezados auxiliares (fila 1)
        wsListas.Cell(1, 1).Value = "Código";
        wsListas.Cell(1, 2).Value = "Nombre";
        wsListas.Cell(1, 3).Value = "Selección";

        int deptCount = DatosGeo.Length;
        for (int i = 0; i < deptCount; i++)
        {
            var (code, name, _) = DatosGeo[i];
            wsListas.Cell(i + 2, 1).Value = code;
            wsListas.Cell(i + 2, 2).Value = name;
            wsListas.Cell(i + 2, 3).Value = $"{code} - {name}";
        }

        // Named range _Departamentos → columna C (display "05 - Antioquia")
        var deptDisplayRange = wsListas.Range(2, 3, deptCount + 1, 3);
        wb.DefinedNames.Add("_Departamentos", deptDisplayRange);

        // Columna D: ciudades únicas (nombre sin código), ordenadas alfabéticamente
        // Extraídas de los municipios: "05001 - Medellín" → "Medellín"
        wsListas.Cell(1, 4).Value = "Ciudad";
        var ciudades = DatosGeo
            .SelectMany(d => d.Municipios)
            .Select(m => { var dash = m.IndexOf(" - ", StringComparison.Ordinal); return dash >= 0 ? m[(dash + 3)..] : m; })
            .Distinct()
            .Order()
            .ToArray();
        for (int i = 0; i < ciudades.Length; i++)
            wsListas.Cell(i + 2, 4).Value = ciudades[i];
        var ciudadesRange = wsListas.Range(2, 4, ciudades.Length + 1, 4);
        wb.DefinedNames.Add("_Ciudades", ciudadesRange);

        // Municipios: cada departamento en una columna (E en adelante)
        // Header = código dept, filas 2+ = municipios con formato "05001 - Medellín"
        for (int d = 0; d < deptCount; d++)
        {
            var (code, _, municipios) = DatosGeo[d];
            int col = 5 + d;
            wsListas.Cell(1, col).Value = code;
            for (int m = 0; m < municipios.Length; m++)
                wsListas.Cell(m + 2, col).Value = municipios[m];
            var mpioRange = wsListas.Range(2, col, municipios.Length + 1, col);
            wb.DefinedNames.Add($"MPIO_{code}", mpioRange);
        }

        // ── Poblar hoja Terceros ───────────────────────────────────────────────

        // Encabezados
        for (int i = 0; i < Columnas.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = Columnas[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1976D2");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Fila de ejemplo — usar formato "CÓDIGO - Nombre" para dept/mpio (coincide con dropdown)
        var ej = new[]
        {
            "NIT", "900900900", "Ejemplo Empresa SAS", "Cliente",
            "6011234567", "contacto@empresa.co", "Calle 123 #45-67", "Bogotá D.C.",
            "11 - Bogotá D.C.", "11001 - Bogotá D.C.", "REGIMEN_COMUN",
            "No", "No", "Si",
        };
        for (int i = 0; i < ej.Length; i++)
        {
            var cell = ws.Cell(2, i + 1);
            cell.Value = ej[i];
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
        }

        // ── Validaciones de datos (filas 2-1001) ──────────────────────────────
        // IMPORTANTE: las listas fijas deben ir entre comillas en el XML de OOXML.
        // ClosedXML escribe el string tal cual → usamos comillas explícitas.
        // Los named ranges y fórmulas se escriben SIN comillas externas y SIN = al inicio.

        // Col A: TipoIdentificacion
        ws.Range("A2:A1001").CreateDataValidation()
            .List("\"CC,NIT,CE,Pasaporte,TI,Otro\"");

        // Col D: TipoTercero
        ws.Range("D2:D1001").CreateDataValidation()
            .List("\"Cliente,Proveedor,Ambos\"");

        // Col H: Ciudad → named range _Ciudades (nombres sin código, ordenados)
        ws.Range("H2:H1001").CreateDataValidation()
            .List("_Ciudades");

        // Col I: CodigoDepartamento → named range _Departamentos (sin = ni cruce de hoja)
        ws.Range("I2:I1001").CreateDataValidation()
            .List("_Departamentos");

        // Col J: CodigoMunicipio → cascading mediante INDIRECT (sin = al inicio)
        // INDIRECT("MPIO_"&LEFT(I2,2)) resuelve el named range según dept seleccionado
        ws.Range("J2:J1001").CreateDataValidation()
            .List("INDIRECT(\"MPIO_\"&LEFT(I2,2))");

        // Col K: PerfilTributario
        ws.Range("K2:K1001").CreateDataValidation()
            .List("\"REGIMEN_COMUN,GRAN_CONTRIBUYENTE,REGIMEN_SIMPLE,PERSONA_NATURAL\"");

        // Cols L, M, N: Si/No
        ws.Range("L2:L1001").CreateDataValidation().List("\"Si,No\"");
        ws.Range("M2:M1001").CreateDataValidation().List("\"Si,No\"");
        ws.Range("N2:N1001").CreateDataValidation().List("\"Si,No\"");

        // Congelar primera fila
        ws.SheetView.FreezeRows(1);

        // Ancho de columnas ajustado a encabezado + fila ejemplo
        ws.Columns().AdjustToContents(1, 2);

        // ── Poblar hoja Referencia ─────────────────────────────────────────────
        wsRef.Cell(1, 1).Value = "Campo"; wsRef.Cell(1, 1).Style.Font.Bold = true;
        wsRef.Cell(1, 2).Value = "Valores válidos / Instrucción"; wsRef.Cell(1, 2).Style.Font.Bold = true;
        wsRef.Cell(2, 1).Value = "TipoIdentificacion";
        wsRef.Cell(2, 2).Value = "CC | NIT | CE | Pasaporte | TI | Otro  (dropdown)";
        wsRef.Cell(3, 1).Value = "TipoTercero";
        wsRef.Cell(3, 2).Value = "Cliente | Proveedor | Ambos  (dropdown)";
        wsRef.Cell(4, 1).Value = "PerfilTributario";
        wsRef.Cell(4, 2).Value = "REGIMEN_COMUN | GRAN_CONTRIBUYENTE | REGIMEN_SIMPLE | PERSONA_NATURAL  (dropdown)";
        wsRef.Cell(5, 1).Value = "EsGranContribuyente / EsAutorretenedor / EsResponsableIVA";
        wsRef.Cell(5, 2).Value = "Si | No  (dropdown)";
        wsRef.Cell(6, 1).Value = "CodigoDepartamento";
        wsRef.Cell(6, 2).Value = "Seleccione de la lista (ej: 11 - Bogotá D.C.). Los municipios se filtran automáticamente.";
        wsRef.Cell(7, 1).Value = "CodigoMunicipio";
        wsRef.Cell(7, 2).Value = "Seleccione de la lista (depende del departamento elegido). Requiere que el departamento esté seleccionado primero.";
        wsRef.Column(1).Width = 52;
        wsRef.Column(2).Width = 80;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── DV Módulo 11 DIAN ────────────────────────────────────────────────────

    public static string CalcularDV(string nit)
    {
        var pesos = new[] { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };
        var digits = nit.Where(char.IsDigit).Reverse().ToArray();
        int suma = digits.Select((c, i) => (c - '0') * pesos[i]).Sum();
        int r = suma % 11;
        return (r < 2 ? r : 11 - r).ToString();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TerceroDto MapToDtoLocal(Tercero t) => new TerceroDto(
        t.Id,
        t.TipoIdentificacion.ToString(),
        t.Identificacion,
        t.DigitoVerificacion,
        t.Nombre,
        t.TipoTercero.ToString(),
        t.Telefono,
        t.Email,
        t.Direccion,
        t.Ciudad,
        t.CodigoDepartamento,
        t.CodigoMunicipio,
        t.PerfilTributario,
        t.EsGranContribuyente,
        t.EsAutorretenedor,
        t.EsResponsableIVA,
        t.OrigenDatos.ToString(),
        t.ExternalId,
        t.Activo,
        t.Actividades
            .Select(a => new TerceroActividadDto(a.Id, a.CodigoCIIU, a.Descripcion, a.EsPrincipal))
            .ToList()
    );
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
