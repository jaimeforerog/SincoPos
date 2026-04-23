using System.Xml.Linq;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

public sealed partial class UblBuilderService
{
    private static XElement BuildUblExtensions(EmisorUblData emisor, string cufe) =>
        new(Ext + "UBLExtensions",
            new XElement(Ext + "UBLExtension",
                new XElement(Ext + "ExtensionURI", "urn:oasis:names:specification:ubl:dsig:enveloped:xades"),
                new XElement(Ext + "ExtensionContent",
                    new XComment("Firma digital insertada por FirmaDigitalService"))),
            new XElement(Ext + "UBLExtension",
                new XElement(Ext + "ExtensionURI", "urn:oasis:names:specification:ubl:dsig:enveloped:xades"),
                new XElement(Ext + "ExtensionContent",
                    new XElement(Sts + "DianExtensions",
                        new XElement(Sts + "InvoiceControl",
                            new XElement(Sts + "InvoiceAuthorization", emisor.NumeroResolucion),
                            new XElement(Sts + "AuthorizationPeriod",
                                new XElement(Cbc + "StartDate", emisor.FechaVigenciaDesde.ToString("yyyy-MM-dd")),
                                new XElement(Cbc + "EndDate", emisor.FechaVigenciaHasta.ToString("yyyy-MM-dd"))),
                            new XElement(Sts + "AuthorizedInvoices",
                                new XElement(Sts + "Prefix", emisor.Prefijo),
                                new XElement(Sts + "From", emisor.NumeroDesde),
                                new XElement(Sts + "To", emisor.NumeroHasta))),
                        new XElement(Sts + "InvoiceSource",
                            new XElement(Cbc + "IdentificationCode",
                                new XAttribute("listAgencyID", "6"),
                                new XAttribute("listAgencyName", "United Nations Economic Commission for Europe"),
                                new XAttribute("listSchemeURI", "urn:oasis:names:specification:ubl:codelist:gc:CountryIdentificationCode-2.1"),
                                "CO")),
                        new XElement(Sts + "SoftwareProvider",
                            new XElement(Sts + "ProviderID",
                                new XAttribute("schemeAgencyID", "195"),
                                new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                                new XAttribute("schemeID", emisor.Nit),
                                new XAttribute("schemeName", "31"),
                                emisor.Nit),
                            new XElement(Sts + "SoftwareID",
                                new XAttribute("schemeAgencyID", "195"),
                                new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                                emisor.IdSoftware)),
                        new XElement(Sts + "SoftwareSecurityCode",
                            new XAttribute("schemeAgencyID", "195"),
                            new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                            cufe),
                        new XElement(Sts + "AuthorizationProvider",
                            new XElement(Sts + "AuthorizationProviderID",
                                new XAttribute("schemeAgencyID", "195"),
                                new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                                new XAttribute("schemeID", "195"),
                                new XAttribute("schemeName", "31"),
                                "800197268")),
                        new XElement(Sts + "QRCode", $"https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey={cufe}")))));

    private static XElement BuildInvoiceControl(EmisorUblData emisor, VentaUblData data) =>
        new(Cac + "OrderReference",
            new XElement(Cbc + "ID", data.NumeroCompleto));

    private static XElement BuildAccountingSupplierParty(EmisorUblData emisor) =>
        new(Cac + "AccountingSupplierParty",
            new XElement(Cbc + "AdditionalAccountID", "1"),
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyName",
                    new XElement(Cbc + "Name", emisor.NombreComercial)),
                new XElement(Cac + "PhysicalLocation",
                    new XElement(Cac + "Address",
                        new XElement(Cbc + "CityName", emisor.CodigoMunicipio),
                        new XElement(Cbc + "CountrySubentity", emisor.CodigoDepartamento),
                        new XElement(Cbc + "CountrySubentityCode", emisor.CodigoDepartamento),
                        new XElement(Cac + "AddressLine",
                            new XElement(Cbc + "Line", emisor.Direccion)),
                        new XElement(Cac + "Country",
                            new XElement(Cbc + "IdentificationCode", "CO"),
                            new XElement(Cbc + "Name",
                                new XAttribute(XNamespace.Xml + "lang", "es"),
                                "Colombia")))),
                new XElement(Cac + "PartyTaxScheme",
                    new XElement(Cbc + "RegistrationName", emisor.RazonSocial),
                    new XElement(Cbc + "CompanyID",
                        new XAttribute("schemeAgencyID", "195"),
                        new XAttribute("schemeAgencyName", "CO, DIAN (Dirección de Impuestos y Aduanas Nacionales)"),
                        new XAttribute("schemeID", emisor.DigitoVerificacion),
                        new XAttribute("schemeName", "31"),
                        emisor.Nit),
                    new XElement(Cbc + "TaxLevelCode",
                        new XAttribute("listName", "48"),
                        MapPerfilTributario(emisor.PerfilTributario)),
                    new XElement(Cac + "RegistrationAddress",
                        new XElement(Cbc + "CityName", emisor.CodigoMunicipio),
                        new XElement(Cbc + "CountrySubentity", emisor.CodigoDepartamento),
                        new XElement(Cac + "Country",
                            new XElement(Cbc + "IdentificationCode", "CO"),
                            new XElement(Cbc + "Name",
                                new XAttribute(XNamespace.Xml + "lang", "es"),
                                "Colombia"))),
                    new XElement(Cac + "TaxScheme",
                        new XElement(Cbc + "ID", "01"),
                        new XElement(Cbc + "Name", "IVA"))),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", emisor.RazonSocial),
                    new XElement(Cbc + "CompanyID",
                        new XAttribute("schemeAgencyID", "195"),
                        new XAttribute("schemeID", emisor.DigitoVerificacion),
                        new XAttribute("schemeName", "31"),
                        emisor.Nit)),
                new XElement(Cac + "Contact",
                    new XElement(Cbc + "ElectronicMail", emisor.Email))));

    private static XElement BuildAccountingCustomerParty(VentaUblData data)
    {
        var nit = data.ClienteNit ?? "222222222";
        var dv = data.ClienteDV ?? "0";
        var nombre = data.ClienteNombre ?? "Consumidor Final";
        var tipoDoc = data.ClienteTipoDocumento;
        var perfil = MapPerfilTributario(data.ClientePerfilTributario);

        return new XElement(Cac + "AccountingCustomerParty",
            new XElement(Cbc + "AdditionalAccountID", tipoDoc == "31" ? "1" : "2"),
            new XElement(Cac + "Party",
                new XElement(Cac + "PartyName",
                    new XElement(Cbc + "Name", nombre)),
                new XElement(Cac + "PhysicalLocation",
                    new XElement(Cac + "Address",
                        new XElement(Cbc + "CityName", data.ClienteCodigoMunicipio ?? "11001"),
                        new XElement(Cbc + "CountrySubentity", "Bogotá"),
                        new XElement(Cac + "AddressLine",
                            new XElement(Cbc + "Line", data.ClienteDireccion ?? "No registra")),
                        new XElement(Cac + "Country",
                            new XElement(Cbc + "IdentificationCode", "CO"),
                            new XElement(Cbc + "Name",
                                new XAttribute(XNamespace.Xml + "lang", "es"),
                                "Colombia")))),
                new XElement(Cac + "PartyTaxScheme",
                    new XElement(Cbc + "RegistrationName", nombre),
                    new XElement(Cbc + "CompanyID",
                        new XAttribute("schemeAgencyID", "195"),
                        new XAttribute("schemeID", dv),
                        new XAttribute("schemeName", tipoDoc),
                        nit),
                    new XElement(Cbc + "TaxLevelCode",
                        new XAttribute("listName", "48"),
                        perfil),
                    new XElement(Cac + "TaxScheme",
                        new XElement(Cbc + "ID", "ZZ"),
                        new XElement(Cbc + "Name", "No aplica"))),
                new XElement(Cac + "PartyLegalEntity",
                    new XElement(Cbc + "RegistrationName", nombre),
                    new XElement(Cbc + "CompanyID",
                        new XAttribute("schemeAgencyID", "195"),
                        new XAttribute("schemeID", dv),
                        new XAttribute("schemeName", tipoDoc),
                        nit)),
                string.IsNullOrEmpty(data.ClienteEmail) ? null! : new XElement(Cac + "Contact",
                    new XElement(Cbc + "ElectronicMail", data.ClienteEmail))));
    }

    private static XElement BuildAccountingCustomerPartyFromDevolucion(DevolucionUblData data)
    {
        var proxy = new VentaUblData(
            "", "", 0, data.FechaEmision,
            0, 0, 0, 0, 0,
            data.ClienteNit, data.ClienteDV, data.ClienteNombre,
            data.ClienteDireccion, data.ClienteCodigoMunicipio, data.ClienteEmail,
            data.ClienteTipoDocumento, data.ClientePerfilTributario,
            new List<LineaUblData>());
        return BuildAccountingCustomerParty(proxy);
    }

    private static XElement BuildTaxTotals(VentaUblData data)
    {
        var elements = new List<XElement>();

        if (data.TotalIva19 > 0)
            elements.Add(BuildTaxTotal("01", "IVA", 19, data.TotalIva19, data.SubtotalSinImpuestos));
        if (data.TotalIva5 > 0)
            elements.Add(BuildTaxTotal("01", "IVA", 5, data.TotalIva5, data.SubtotalSinImpuestos));
        if (data.TotalInc > 0)
            elements.Add(BuildTaxTotal("04", "INC", 8, data.TotalInc, data.SubtotalSinImpuestos));
        if (elements.Count == 0)
            elements.Add(BuildTaxTotal("ZZ", "No aplica", 0, 0, data.SubtotalSinImpuestos));

        return new XElement("_dummy_", elements).Elements().First();
    }

    private static XElement BuildTaxTotalsFromDevolucion(DevolucionUblData data)
    {
        var proxy = new VentaUblData(
            "", "", 0, data.FechaEmision,
            data.SubtotalSinImpuestos, data.TotalIva19, data.TotalIva5, data.TotalInc, data.Total,
            null, null, null, null, null, null, "13", "REGIMEN_COMUN",
            new List<LineaUblData>());
        return BuildTaxTotals(proxy);
    }

    private static XElement BuildTaxTotal(string codigo, string nombre, decimal porcentaje,
        decimal montoImpuesto, decimal baseImponible) =>
        new(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount",
                new XAttribute("currencyID", "COP"),
                montoImpuesto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(Cac + "TaxSubtotal",
                new XElement(Cbc + "TaxableAmount",
                    new XAttribute("currencyID", "COP"),
                    baseImponible.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement(Cbc + "TaxAmount",
                    new XAttribute("currencyID", "COP"),
                    montoImpuesto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement(Cac + "TaxCategory",
                    new XElement(Cbc + "Percent", porcentaje.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxScheme",
                        new XElement(Cbc + "ID", codigo),
                        new XElement(Cbc + "Name", nombre)))));

    private static XElement BuildLegalMonetaryTotal(VentaUblData data) =>
        BuildLegalMonetaryTotalBase(data.SubtotalSinImpuestos, data.Total);

    private static XElement BuildLegalMonetaryTotalFromDevolucion(DevolucionUblData data) =>
        BuildLegalMonetaryTotalBase(data.SubtotalSinImpuestos, data.Total);

    private static XElement BuildLegalMonetaryTotalBase(decimal subtotal, decimal total)
    {
        var fmt = System.Globalization.CultureInfo.InvariantCulture;
        return new XElement(Cac + "LegalMonetaryTotal",
            new XElement(Cbc + "LineExtensionAmount", new XAttribute("currencyID", "COP"), subtotal.ToString("F2", fmt)),
            new XElement(Cbc + "TaxExclusiveAmount",  new XAttribute("currencyID", "COP"), subtotal.ToString("F2", fmt)),
            new XElement(Cbc + "TaxInclusiveAmount",  new XAttribute("currencyID", "COP"), total.ToString("F2", fmt)),
            new XElement(Cbc + "AllowanceTotalAmount", new XAttribute("currencyID", "COP"), "0.00"),
            new XElement(Cbc + "ChargeTotalAmount",   new XAttribute("currencyID", "COP"), "0.00"),
            new XElement(Cbc + "PayableAmount",       new XAttribute("currencyID", "COP"), total.ToString("F2", fmt)));
    }

    private static XElement BuildInvoiceLine(LineaUblData linea) =>
        new(Cac + "InvoiceLine",
            new XElement(Cbc + "ID", linea.NumeroLinea),
            new XElement(Cbc + "InvoicedQuantity",
                new XAttribute("unitCode", linea.UnidadMedida),
                new XAttribute("unitCodeListID", "UN/ECE rec 20"),
                linea.Cantidad.ToString("F4")),
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", "COP"),
                linea.Subtotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(Cac + "TaxTotal",
                new XElement(Cbc + "TaxAmount",
                    new XAttribute("currencyID", "COP"),
                    linea.MontoIva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement(Cac + "TaxSubtotal",
                    new XElement(Cbc + "TaxableAmount",
                        new XAttribute("currencyID", "COP"),
                        linea.Subtotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", "COP"),
                        linea.MontoIva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxCategory",
                        new XElement(Cbc + "Percent", linea.PorcentajeIva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(Cac + "TaxScheme",
                            new XElement(Cbc + "ID", "01"),
                            new XElement(Cbc + "Name", "IVA"))))),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Description", linea.DescripcionProducto),
                new XElement(Cac + "StandardItemIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "001"),
                        linea.CodigoProducto))),
            new XElement(Cac + "Price",
                new XElement(Cbc + "PriceAmount",
                    new XAttribute("currencyID", "COP"),
                    linea.PrecioUnitario.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement(Cbc + "BaseQuantity",
                    new XAttribute("unitCode", linea.UnidadMedida),
                    "1")));

    private static XElement BuildCreditNoteLine(LineaUblData linea) =>
        new(Cac + "CreditNoteLine",
            new XElement(Cbc + "ID", linea.NumeroLinea),
            new XElement(Cbc + "CreditedQuantity",
                new XAttribute("unitCode", linea.UnidadMedida),
                linea.Cantidad.ToString("F4")),
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", "COP"),
                linea.Subtotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(Cac + "TaxTotal",
                new XElement(Cbc + "TaxAmount",
                    new XAttribute("currencyID", "COP"),
                    linea.MontoIva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                new XElement(Cac + "TaxSubtotal",
                    new XElement(Cbc + "TaxableAmount",
                        new XAttribute("currencyID", "COP"),
                        linea.Subtotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", "COP"),
                        linea.MontoIva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(Cac + "TaxCategory",
                        new XElement(Cbc + "Percent", linea.PorcentajeIva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(Cac + "TaxScheme",
                            new XElement(Cbc + "ID", "01"),
                            new XElement(Cbc + "Name", "IVA"))))),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Description", linea.DescripcionProducto),
                new XElement(Cac + "StandardItemIdentification",
                    new XElement(Cbc + "ID",
                        new XAttribute("schemeID", "001"),
                        linea.CodigoProducto))),
            new XElement(Cac + "Price",
                new XElement(Cbc + "PriceAmount",
                    new XAttribute("currencyID", "COP"),
                    linea.PrecioUnitario.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))));
}
