using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

public sealed partial class UblBuilderService : IUblBuilderService
{
    // Namespaces UBL 2.1 / DIAN
    private static readonly XNamespace Ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace UblNc = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private static readonly XNamespace Sts = "dian:gov:co:facturaelectronica:Structs-2-1";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    private static readonly TimeZoneInfo TzColombia = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota");

    public (string xml, string cufe) GenerarFacturaVenta(VentaUblData data, EmisorUblData emisor)
    {
        var fechaColombia = TimeZoneInfo.ConvertTimeFromUtc(data.FechaEmision, TzColombia);
        var cufe = CalcularCufe(data, emisor, fechaColombia);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ubl + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sts", Sts.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
                BuildUblExtensions(emisor, cufe),
                new XElement(Cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(Cbc + "CustomizationID", "10"),
                new XElement(Cbc + "ProfileID", "DIAN 2.1"),
                new XElement(Cbc + "ProfileExecutionID", emisor.Ambiente),
                new XElement(Cbc + "ID", data.NumeroCompleto),
                new XElement(Cbc + "UUID",
                    new XAttribute("schemeID", emisor.Ambiente),
                    new XAttribute("schemeName", "CUFE-SHA384"),
                    cufe),
                new XElement(Cbc + "IssueDate", fechaColombia.ToString("yyyy-MM-dd")),
                new XElement(Cbc + "IssueTime", fechaColombia.ToString("HH:mm:ss")),
                new XElement(Cbc + "InvoiceTypeCode",
                    new XAttribute("listAgencyID", "195"),
                    new XAttribute("listAgencyName", "DIAN (Direccion de Impuestos y Aduanas Nacionales)"),
                    new XAttribute("listID", emisor.Ambiente),
                    "01"),
                new XElement(Cbc + "Note",
                    new XAttribute(XNamespace.Xml + "lang", "es"),
                    $"Factura de Venta {data.NumeroCompleto}"),
                new XElement(Cbc + "DocumentCurrencyCode",
                    new XAttribute("listAgencyID", "6"),
                    new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"),
                    new XAttribute("listID", "ISO 4217 Alpha"),
                    "COP"),
                new XElement(Cbc + "LineCountNumeric", data.Lineas.Count.ToString()),
                BuildInvoiceControl(emisor, data),
                BuildAccountingSupplierParty(emisor),
                BuildAccountingCustomerParty(data),
                BuildTaxTotals(data),
                BuildLegalMonetaryTotal(data),
                data.Lineas.Select(linea => BuildInvoiceLine(linea))
            )
        );

        return (doc.ToString(), cufe);
    }

    public (string xml, string cufe) GenerarNotaCredito(DevolucionUblData data, EmisorUblData emisor)
    {
        var fechaColombia = TimeZoneInfo.ConvertTimeFromUtc(data.FechaEmision, TzColombia);
        var cufe = CalcularCufeNc(data, emisor, fechaColombia);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(UblNc + "CreditNote",
                new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "sts", Sts.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
                BuildUblExtensions(emisor, cufe),
                new XElement(Cbc + "UBLVersionID", "UBL 2.1"),
                new XElement(Cbc + "CustomizationID", "20"),
                new XElement(Cbc + "ProfileID", "DIAN 2.1"),
                new XElement(Cbc + "ProfileExecutionID", emisor.Ambiente),
                new XElement(Cbc + "ID", data.NumeroCompleto),
                new XElement(Cbc + "UUID",
                    new XAttribute("schemeID", emisor.Ambiente),
                    new XAttribute("schemeName", "CUFE-SHA384"),
                    cufe),
                new XElement(Cbc + "IssueDate", fechaColombia.ToString("yyyy-MM-dd")),
                new XElement(Cbc + "IssueTime", fechaColombia.ToString("HH:mm:ss")),
                new XElement(Cbc + "Note",
                    new XAttribute(XNamespace.Xml + "lang", "es"),
                    data.MotivoDevolucion),
                new XElement(Cbc + "DocumentCurrencyCode",
                    new XAttribute("listAgencyID", "6"),
                    new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"),
                    new XAttribute("listID", "ISO 4217 Alpha"),
                    "COP"),
                new XElement(Cbc + "LineCountNumeric", data.Lineas.Count.ToString()),
                new XElement(Cac + "DiscrepancyResponse",
                    new XElement(Cbc + "ReferenceID", data.NumeroFacturaOriginal),
                    new XElement(Cbc + "ResponseCode",
                        new XAttribute("listAgencyID", "195"),
                        new XAttribute("listAgencyName", "DIAN"),
                        "2"),
                    new XElement(Cbc + "Description", data.MotivoDevolucion)),
                new XElement(Cac + "BillingReference",
                    new XElement(Cac + "InvoiceDocumentReference",
                        new XElement(Cbc + "ID", data.NumeroFacturaOriginal),
                        new XElement(Cbc + "UUID",
                            new XAttribute("schemeID", emisor.Ambiente),
                            new XAttribute("schemeName", "CUFE-SHA384"),
                            data.CufeFacturaOriginal),
                        new XElement(Cbc + "IssueDate", TimeZoneInfo
                            .ConvertTimeFromUtc(data.FechaFacturaOriginal, TzColombia)
                            .ToString("yyyy-MM-dd")))),
                BuildAccountingSupplierParty(emisor),
                BuildAccountingCustomerPartyFromDevolucion(data),
                BuildTaxTotalsFromDevolucion(data),
                BuildLegalMonetaryTotalFromDevolucion(data),
                data.Lineas.Select(linea => BuildCreditNoteLine(linea))
            )
        );

        return (doc.ToString(), cufe);
    }

    // ─── CUFE ─────────────────────────────────────────────────────────────────

    private static string CalcularCufe(VentaUblData data, EmisorUblData emisor, DateTime fechaColombia) =>
        ComputeSha384(BuildCadenaCufe(
            data.NumeroCompleto, fechaColombia, data.SubtotalSinImpuestos,
            data.TotalIva19 + data.TotalIva5, data.TotalInc, data.Total,
            emisor.Nit, MapTipoDocCliente(data.ClienteTipoDocumento),
            data.ClienteNit ?? "222222222", emisor.PinSoftware, emisor.Ambiente));

    private static string CalcularCufeNc(DevolucionUblData data, EmisorUblData emisor, DateTime fechaColombia) =>
        ComputeSha384(BuildCadenaCufe(
            data.NumeroCompleto, fechaColombia, data.SubtotalSinImpuestos,
            data.TotalIva19 + data.TotalIva5, data.TotalInc, data.Total,
            emisor.Nit, MapTipoDocCliente(data.ClienteTipoDocumento),
            data.ClienteNit ?? "222222222", emisor.PinSoftware, emisor.Ambiente));

    // Algoritmo SHA-384 DIAN: ValImp1=IVA(01), ValImp2=INC(04), ValImp3=ICA(03 — no implementado)
    private static string BuildCadenaCufe(
        string numero, DateTime fecha, decimal subtotal,
        decimal iva, decimal inc, decimal total,
        string nit, string tipoDocCliente, string nitCliente,
        string pin, string ambiente) =>
        $"{numero}{fecha:yyyyMMdd}{fecha:HH:mm:ss}{subtotal:F2}" +
        $"01{iva:F2}04{inc:F2}03{0:F2}" +
        $"{total:F2}{nit}{tipoDocCliente}{nitCliente}{pin}{ambiente}";

    private static string ComputeSha384(string input)
    {
        var bytes = SHA384.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string MapTipoDocCliente(string tipoDoc) => tipoDoc switch
    {
        "31" => "31",
        "13" => "13",
        "22" => "22",
        _ => "13"
    };

    private static string MapPerfilTributario(string perfil) => perfil switch
    {
        "GRAN_CONTRIBUYENTE" => "O-13",
        "REGIMEN_ORDINARIO" => "O-13",
        "REGIMEN_SIMPLE" => "O-47",
        "PERSONA_NATURAL" => "R-99-PN",
        _ => "R-99-PN"
    };
}
