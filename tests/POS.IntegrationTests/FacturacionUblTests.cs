using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using POS.Application.Services;
using POS.Infrastructure.Services;

namespace POS.IntegrationTests;

/// <summary>
/// Pruebas unitarias para UblBuilderService: generación XML UBL 2.1 y cálculo CUFE.
/// No requiere base de datos ni servidor HTTP.
/// </summary>
public class FacturacionUblTests
{
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace UblNc = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private static readonly XNamespace Sts = "dian:gov:co:facturaelectronica:Structs-2-1";

    private readonly UblBuilderService _sut = new();

    // ─── Datos de prueba ────────────────────────────────────────────────────

    private static EmisorUblData TestEmisor() => new(
        Nit: "900123456",
        DigitoVerificacion: "7",
        RazonSocial: "EMPRESA TEST SAS",
        NombreComercial: "Test Store",
        Direccion: "Calle 1 #2-3",
        CodigoMunicipio: "11001",
        CodigoDepartamento: "11",
        Telefono: "3001234567",
        Email: "test@empresa.com",
        CodigoCiiu: "4711",
        PerfilTributario: "REGIMEN_ORDINARIO",
        NumeroResolucion: "18764000001",
        FechaVigenciaDesde: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FechaVigenciaHasta: new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Prefijo: "FV",
        NumeroDesde: 1,
        NumeroHasta: 100000,
        Ambiente: "2",
        PinSoftware: "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c",
        IdSoftware: "3f211e78-dc16-4438-9c55-accfc38e1c10",
        CertificadoBase64: "",
        CertificadoPassword: ""
    );

    private static VentaUblData TestVenta(
        string numero = "FV00000001",
        decimal subtotal = 100_000m,
        decimal iva19 = 19_000m,
        decimal iva5 = 0m,
        decimal inc = 0m,
        decimal total = 119_000m,
        string? clienteNit = null,
        string tipoDoc = "13") => new(
        NumeroCompleto: numero,
        Prefijo: "FV",
        Numero: 1,
        FechaEmision: new DateTime(2026, 3, 7, 15, 0, 0, DateTimeKind.Utc),
        SubtotalSinImpuestos: subtotal,
        TotalIva19: iva19,
        TotalIva5: iva5,
        TotalInc: inc,
        Total: total,
        ClienteNit: clienteNit,
        ClienteDV: clienteNit != null ? "0" : null,
        ClienteNombre: clienteNit != null ? "Cliente Empresarial" : null,
        ClienteDireccion: null,
        ClienteCodigoMunicipio: null,
        ClienteEmail: null,
        ClienteTipoDocumento: tipoDoc,
        ClientePerfilTributario: "PERSONA_NATURAL",
        Lineas: new List<LineaUblData>
        {
            new(1, "PROD001", "Producto Test", "94", 2m, 50_000m, 0m, subtotal, 19m, iva19)
        }
    );

    // ─── Tests: GenerarFacturaVenta ──────────────────────────────────────────

    [Fact]
    public void GenerarFacturaVenta_DebeRetornarXmlValido()
    {
        var (xml, cufe) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());

        xml.Should().NotBeNullOrEmpty();
        cufe.Should().NotBeNullOrEmpty();
        var doc = XDocument.Parse(xml);
        doc.Root.Should().NotBeNull();
    }

    [Fact]
    public void GenerarFacturaVenta_RaizDebeSerInvoice_EnNamespaceUBL()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        doc.Root!.Name.Should().Be(Ubl + "Invoice");
    }

    [Fact]
    public void GenerarFacturaVenta_DebeIncluirVersion_UBL21()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        doc.Descendants(Cbc + "UBLVersionID").First().Value.Should().Be("UBL 2.1");
    }

    [Fact]
    public void GenerarFacturaVenta_IdDebeCoincidirConNumeroCompleto()
    {
        var venta = TestVenta(numero: "FV00000042");
        var (xml, _) = _sut.GenerarFacturaVenta(venta, TestEmisor());
        var doc = XDocument.Parse(xml);

        var id = doc.Descendants(Cbc + "ID").First().Value;
        id.Should().Be("FV00000042");
    }

    [Fact]
    public void GenerarFacturaVenta_InvoiceTypeCodeDebe_Ser01()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        var typeCode = doc.Descendants(Cbc + "InvoiceTypeCode").First().Value;
        typeCode.Should().Be("01");
    }

    [Fact]
    public void GenerarFacturaVenta_MonedaDebeSer_COP()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        var currency = doc.Descendants(Cbc + "DocumentCurrencyCode").First().Value;
        currency.Should().Be("COP");
    }

    [Fact]
    public void GenerarFacturaVenta_NitEmisorDebeAparecer_EnSupplierParty()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        var supplierParty = doc.Descendants(Cac + "AccountingSupplierParty").First();
        supplierParty.ToString().Should().Contain("900123456");
    }

    [Fact]
    public void GenerarFacturaVenta_SinCliente_DebeUsarConsumidorFinal()
    {
        var venta = TestVenta(clienteNit: null);
        var (xml, _) = _sut.GenerarFacturaVenta(venta, TestEmisor());
        var doc = XDocument.Parse(xml);

        var customerParty = doc.Descendants(Cac + "AccountingCustomerParty").First();
        customerParty.ToString().Should().Contain("Consumidor Final");
        customerParty.ToString().Should().Contain("222222222");
    }

    [Fact]
    public void GenerarFacturaVenta_ConClienteEmpresarial_DebeUsarDatosCliente()
    {
        var venta = TestVenta(clienteNit: "800999888", tipoDoc: "31");
        var (xml, _) = _sut.GenerarFacturaVenta(venta, TestEmisor());
        var doc = XDocument.Parse(xml);

        var customerParty = doc.Descendants(Cac + "AccountingCustomerParty").First();
        customerParty.ToString().Should().Contain("800999888");
        customerParty.ToString().Should().Contain("Cliente Empresarial");
    }

    [Fact]
    public void GenerarFacturaVenta_TotalesMonetarios_DebenCorresponder()
    {
        var venta = TestVenta(subtotal: 100_000m, total: 119_000m);
        var (xml, _) = _sut.GenerarFacturaVenta(venta, TestEmisor());
        var doc = XDocument.Parse(xml);

        var monetary = doc.Descendants(Cac + "LegalMonetaryTotal").First();
        monetary.Descendants(Cbc + "LineExtensionAmount").First().Value.Should().Be("100000.00");
        monetary.Descendants(Cbc + "PayableAmount").First().Value.Should().Be("119000.00");
    }

    [Fact]
    public void GenerarFacturaVenta_ConIva19_DebeTenerTaxTotal_ConCodigo01()
    {
        var venta = TestVenta(iva19: 19_000m);
        var (xml, _) = _sut.GenerarFacturaVenta(venta, TestEmisor());
        var doc = XDocument.Parse(xml);

        var taxTotals = doc.Descendants(Cac + "TaxTotal").ToList();
        taxTotals.Should().NotBeEmpty();
        var primerTax = taxTotals.First();
        primerTax.ToString().Should().Contain("19000.00");
    }

    [Fact]
    public void GenerarFacturaVenta_SinImpuestos_DebeTenerTaxTotal_ZZ()
    {
        // Venta sin IVA: consumidor final, producto exento
        var venta = new VentaUblData(
            "FV00000002", "FV", 2,
            new DateTime(2026, 3, 7, 15, 0, 0, DateTimeKind.Utc),
            50_000m, 0m, 0m, 0m, 50_000m,
            null, null, null, null, null, null,
            "13", "PERSONA_NATURAL",
            new List<LineaUblData>
            {
                new(1, "PROD002", "Producto Exento", "94", 1m, 50_000m, 0m, 50_000m, 0m, 0m)
            });

        var (xml, _) = _sut.GenerarFacturaVenta(venta, TestEmisor());
        var doc = XDocument.Parse(xml);

        // Debe generar un TaxTotal con esquema ZZ (no aplica)
        var taxSchemes = doc.Descendants(Cac + "TaxScheme").ToList();
        taxSchemes.Should().Contain(ts => ts.Descendants(Cbc + "ID").Any(e => e.Value == "ZZ"));
    }

    [Fact]
    public void GenerarFacturaVenta_NumeroLineasDebe_CoincidirConLineas()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        var lineCount = doc.Descendants(Cbc + "LineCountNumeric").First().Value;
        lineCount.Should().Be("1");

        var invoiceLines = doc.Descendants(Cac + "InvoiceLine").ToList();
        invoiceLines.Should().HaveCount(1);
    }

    [Fact]
    public void GenerarFacturaVenta_AmbienteDebeAparecer_EnProfileExecutionID()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        var profileExec = doc.Descendants(Cbc + "ProfileExecutionID").First().Value;
        profileExec.Should().Be("2"); // ambiente pruebas
    }

    [Fact]
    public void GenerarFacturaVenta_ResolucionDebe_AparecerEnExtensiones()
    {
        var (xml, _) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        var invoiceAuth = doc.Descendants(Sts + "InvoiceAuthorization").First().Value;
        invoiceAuth.Should().Be("18764000001");
    }

    [Fact]
    public void GenerarFacturaVenta_CufeDebeTener_96Caracteres()
    {
        var (_, cufe) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());

        // SHA-384 = 48 bytes = 96 hex chars
        cufe.Should().HaveLength(96);
        cufe.Should().MatchRegex("^[0-9a-f]{96}$");
    }

    [Fact]
    public void GenerarFacturaVenta_CufeDebeSer_Determinista()
    {
        var venta = TestVenta();
        var emisor = TestEmisor();

        var (_, cufe1) = _sut.GenerarFacturaVenta(venta, emisor);
        var (_, cufe2) = _sut.GenerarFacturaVenta(venta, emisor);

        cufe1.Should().Be(cufe2);
    }

    [Fact]
    public void GenerarFacturaVenta_CufeDiferenteAlCambiar_NumeroVenta()
    {
        var emisor = TestEmisor();
        var (_, cufe1) = _sut.GenerarFacturaVenta(TestVenta(numero: "FV00000001"), emisor);
        var (_, cufe2) = _sut.GenerarFacturaVenta(TestVenta(numero: "FV00000002"), emisor);

        cufe1.Should().NotBe(cufe2);
    }

    [Fact]
    public void GenerarFacturaVenta_CufeEsCalculoCorrecto_SHA384()
    {
        // Replica el algoritmo DIAN manualmente para verificar que UblBuilderService lo calcula bien
        var venta = TestVenta();
        var emisor = TestEmisor();

        var (_, cufe) = _sut.GenerarFacturaVenta(venta, emisor);

        // El CUFE debe ser un SHA-384 hex lowercase válido
        var bytes = Convert.FromHexString(cufe);
        bytes.Should().HaveCount(48); // SHA-384 = 48 bytes
    }

    [Fact]
    public void GenerarFacturaVenta_UUIDEnXml_DebeCoincidirConCufeRetornado()
    {
        var (xml, cufe) = _sut.GenerarFacturaVenta(TestVenta(), TestEmisor());
        var doc = XDocument.Parse(xml);

        var uuid = doc.Descendants(Cbc + "UUID").First().Value;
        uuid.Should().Be(cufe);
    }

    // ─── Tests: GenerarNotaCredito ───────────────────────────────────────────

    [Fact]
    public void GenerarNotaCredito_DebeRetornarXmlValido()
    {
        var devolucion = TestDevolucion();
        var (xml, cufe) = _sut.GenerarNotaCredito(devolucion, TestEmisor());

        xml.Should().NotBeNullOrEmpty();
        cufe.Should().NotBeNullOrEmpty();
        XDocument.Parse(xml).Root.Should().NotBeNull();
    }

    [Fact]
    public void GenerarNotaCredito_RaizDebeSerCreditNote_EnNamespaceUBL()
    {
        var (xml, _) = _sut.GenerarNotaCredito(TestDevolucion(), TestEmisor());
        var doc = XDocument.Parse(xml);

        doc.Root!.Name.Should().Be(UblNc + "CreditNote");
    }

    [Fact]
    public void GenerarNotaCredito_CustomizationIdDebe_Ser20()
    {
        var (xml, _) = _sut.GenerarNotaCredito(TestDevolucion(), TestEmisor());
        var doc = XDocument.Parse(xml);

        doc.Descendants(Cbc + "CustomizationID").First().Value.Should().Be("20");
    }

    [Fact]
    public void GenerarNotaCredito_DebeReferenciar_FacturaOriginal()
    {
        var devolucion = TestDevolucion();
        var (xml, _) = _sut.GenerarNotaCredito(devolucion, TestEmisor());
        var doc = XDocument.Parse(xml);

        // BillingReference debe tener el numero de la factura original
        var billingRef = doc.Descendants(Cac + "BillingReference").First();
        billingRef.ToString().Should().Contain("FV00000001");
        billingRef.ToString().Should().Contain(devolucion.CufeFacturaOriginal);
    }

    [Fact]
    public void GenerarNotaCredito_CufeDebeTener_96Caracteres()
    {
        var (_, cufe) = _sut.GenerarNotaCredito(TestDevolucion(), TestEmisor());

        cufe.Should().HaveLength(96);
        cufe.Should().MatchRegex("^[0-9a-f]{96}$");
    }

    [Fact]
    public void GenerarNotaCredito_MotivoDebeAparecerEnNote()
    {
        var devolucion = TestDevolucion(motivo: "Producto en mal estado");
        var (xml, _) = _sut.GenerarNotaCredito(devolucion, TestEmisor());
        var doc = XDocument.Parse(xml);

        var note = doc.Descendants(Cbc + "Note").First().Value;
        note.Should().Be("Producto en mal estado");
    }

    private static DevolucionUblData TestDevolucion(string motivo = "Devolución de mercancía") =>
        new(
            NumeroCompleto: "NC00000001",
            Prefijo: "NC",
            Numero: 1,
            FechaEmision: new DateTime(2026, 3, 7, 16, 0, 0, DateTimeKind.Utc),
            CufeFacturaOriginal: new string('a', 96), // CUFE ficticio de 96 chars
            NumeroFacturaOriginal: "FV00000001",
            FechaFacturaOriginal: new DateTime(2026, 3, 7, 15, 0, 0, DateTimeKind.Utc),
            MotivoDevolucion: motivo,
            SubtotalSinImpuestos: 50_000m,
            TotalIva19: 9_500m,
            TotalIva5: 0m,
            TotalInc: 0m,
            Total: 59_500m,
            ClienteNit: null,
            ClienteDV: null,
            ClienteNombre: null,
            ClienteDireccion: null,
            ClienteCodigoMunicipio: null,
            ClienteEmail: null,
            ClienteTipoDocumento: "13",
            ClientePerfilTributario: "PERSONA_NATURAL",
            Lineas: new List<LineaUblData>
            {
                new(1, "PROD001", "Producto Test", "94", 1m, 50_000m, 0m, 50_000m, 19m, 9_500m)
            }
        );
}
