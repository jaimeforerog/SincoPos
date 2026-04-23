using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Data.Entities;

namespace POS.Infrastructure.Services;

public sealed class TerceroImportService : ITerceroImportService
{
    private readonly AppDbContext _context;
    private readonly ICurrentEmpresaProvider _empresaProvider;

    public TerceroImportService(AppDbContext context, ICurrentEmpresaProvider empresaProvider)
    {
        _context = context;
        _empresaProvider = empresaProvider;
    }

    // ── Columnas esperadas en la plantilla ────────────────────────────────────

    private static readonly string[] Columnas =
    [
        "TipoIdentificacion", "Identificacion", "Nombre", "TipoTercero",
        "Telefono", "Email", "Direccion", "Ciudad",
        "CodigoDepartamento", "CodigoMunicipio", "PerfilTributario",
        "EsGranContribuyente", "EsAutorretenedor", "EsResponsableIVA",
    ];

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
                tercero.DigitoVerificacion = TerceroLocalService.CalcularDV(identificacion);

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

        var ws = wb.Worksheets.Add("Terceros");
        var wsRef = wb.Worksheets.Add("Referencia");
        var wsListas = wb.Worksheets.Add("Listas");
        wsListas.Visibility = XLWorksheetVisibility.Hidden;

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

        var deptDisplayRange = wsListas.Range(2, 3, deptCount + 1, 3);
        wb.DefinedNames.Add("_Departamentos", deptDisplayRange);

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

        for (int i = 0; i < Columnas.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = Columnas[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1976D2");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

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

        ws.Range("A2:A1001").CreateDataValidation().List("\"CC,NIT,CE,Pasaporte,TI,Otro\"");
        ws.Range("D2:D1001").CreateDataValidation().List("\"Cliente,Proveedor,Ambos\"");
        ws.Range("H2:H1001").CreateDataValidation().List("_Ciudades");
        ws.Range("I2:I1001").CreateDataValidation().List("_Departamentos");
        ws.Range("J2:J1001").CreateDataValidation().List("INDIRECT(\"MPIO_\"&LEFT(I2,2))");
        ws.Range("K2:K1001").CreateDataValidation().List("\"REGIMEN_COMUN,GRAN_CONTRIBUYENTE,REGIMEN_SIMPLE,PERSONA_NATURAL\"");
        ws.Range("L2:L1001").CreateDataValidation().List("\"Si,No\"");
        ws.Range("M2:M1001").CreateDataValidation().List("\"Si,No\"");
        ws.Range("N2:N1001").CreateDataValidation().List("\"Si,No\"");

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 2);

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
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
