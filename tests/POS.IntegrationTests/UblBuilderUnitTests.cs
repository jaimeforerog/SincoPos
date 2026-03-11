using System.Xml.Linq;
using POS.Application.Services;
using POS.Infrastructure.Services;

namespace POS.IntegrationTests;

/// <summary>
/// Tests unitarios del UblBuilderService.
/// Verifica la estructura XML generada para cumplimiento con DIAN UBL 2.1.
/// </summary>
public class UblBuilderUnitTests
{
    private readonly UblBuilderService _builder = new();
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    private EmisorUblData BaseEmisor() => new(
        Nit: "900123456",
        DigitoVerificacion: "7",
        RazonSocial: "Empresa de Prueba SAS",
        NombreComercial: "Sincopos Test",
        Direccion: "Calle 123",
        CodigoMunicipio: "11001",
        CodigoDepartamento: "11",
        Telefono: "3001234567",
        Email: "factura@test.com",
        CodigoCiiu: "6201",
        PerfilTributario: "REGIMEN_COMUN",
        NumeroResolucion: "18760000001",
        FechaVigenciaDesde: DateTime.Today.AddDays(-30),
        FechaVigenciaHasta: DateTime.Today.AddYears(1),
        Prefijo: "SETT",
        NumeroDesde: 1,
        NumeroHasta: 5000000,
        Ambiente: "2", // Pruebas
        PinSoftware: "12345",
        IdSoftware: "guid-software",
        CertificadoBase64: "dGVzdC1jZXJ0",
        CertificadoPassword: "password"
    );

    private VentaUblData BaseVenta() => new(
        NumeroCompleto: "SETT-101",
        Prefijo: "SETT",
        Numero: 101,
        FechaEmision: DateTime.UtcNow,
        SubtotalSinImpuestos: 100000m,
        TotalIva19: 19000m,
        TotalIva5: 0m,
        TotalInc: 0m,
        Total: 119000m,
        ClienteNit: "12345678",
        ClienteDV: "9",
        ClienteNombre: "Juan Perez",
        ClienteDireccion: "Av Siempre Viva",
        ClienteCodigoMunicipio: "11001",
        ClienteEmail: "juan@perez.com",
        ClienteTipoDocumento: "13", // CC
        ClientePerfilTributario: "PERSONA_NATURAL",
        Lineas: new List<LineaUblData>
        {
            new(1, "P001", "Prod 1", "94", 1m, 100000m, 0m, 100000m, 19m, 19000m)
        }
    );

    [Fact]
    public void GenerarFactura_DebeIncluirCUFE_Y_NodosBase()
    {
        var emisor = BaseEmisor();
        var venta = BaseVenta();

        var (xml, cufe) = _builder.GenerarFacturaVenta(venta, emisor);
        var doc = XDocument.Parse(xml);

        // Verificar CUFE
        Assert.NotEmpty(cufe);
        Assert.Equal(cufe, doc.Descendants(Cbc + "UUID").First().Value);

        // Verificar Nodos UBL requeridos
        Assert.Equal("UBL 2.1", doc.Descendants(Cbc + "UBLVersionID").First().Value);
        Assert.Equal("SETT-101", doc.Descendants(Cbc + "ID").First().Value);
    }

    [Fact]
    public void GenerarFactura_ClienteSinNIT_DebeUsarConsumidorFinal()
    {
        var emisor = BaseEmisor();
        var venta = BaseVenta() with
        {
            ClienteNit = null,
            ClienteNombre = null,
            ClienteTipoDocumento = "13"
        };

        var (xml, _) = _builder.GenerarFacturaVenta(venta, emisor);
        var doc = XDocument.Parse(xml);

        var party = doc.Descendants(Cac + "AccountingCustomerParty").First();
        var nit = party.Descendants(Cbc + "CompanyID").First().Value;
        var nombre = party.Descendants(Cbc + "Name").First().Value;

        Assert.Equal("222222222", nit);
        Assert.Equal("Consumidor Final", nombre);
    }

    [Fact]
    public void GenerarFactura_LineasYImpuestos_DebeMapearCorrectamente()
    {
        var emisor = BaseEmisor();
        var venta = BaseVenta();

        var (xml, _) = _builder.GenerarFacturaVenta(venta, emisor);
        var doc = XDocument.Parse(xml);

        // 1. Verificar Línea
        var line = doc.Descendants(Cac + "InvoiceLine").First();
        Assert.Equal("100000.00", line.Descendants(Cbc + "LineExtensionAmount").First().Value);
        Assert.Equal("P001", line.Descendants(Cac + "Item").First().Descendants(Cbc + "ID").First().Value);

        // 2. Verificar Impuesto Total
        var taxTotal = doc.Descendants(Cac + "TaxTotal").First();
        Assert.Equal("19000.00", taxTotal.Descendants(Cbc + "TaxAmount").First().Value);
        
        var taxSubtotal = taxTotal.Descendants(Cac + "TaxSubtotal").First();
        Assert.Equal("19.00", taxSubtotal.Descendants(Cbc + "Percent").First().Value);
        Assert.Equal("01", taxSubtotal.Descendants(Cac + "TaxScheme").First().Descendants(Cbc + "ID").First().Value);
    }
}
