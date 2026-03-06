using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using POS.Application.Services;

namespace POS.Infrastructure.Services;

public class FirmaDigitalService : IFirmaDigitalService
{
    public string FirmarXml(string xmlSinFirmar, string certificadoBase64, string password)
    {
        if (string.IsNullOrEmpty(certificadoBase64))
            throw new InvalidOperationException("No hay certificado digital configurado para este emisor.");

        // Cargar certificado PKCS#12
        var certBytes = Convert.FromBase64String(certificadoBase64);
        using var cert = X509CertificateLoader.LoadPkcs12(
            certBytes,
            password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);

        var rsaKey = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado no tiene clave privada RSA.");

        // Cargar el XML
        var doc = new XmlDocument { PreserveWhitespace = false };
        doc.LoadXml(xmlSinFirmar);

        // Crear firma XMLDSIG
        var signedXml = new SignedXml(doc)
        {
            SigningKey = rsaKey
        };
        signedXml.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;

        // Reference 1: documento completo
        var refDoc = new Reference { Uri = "" };
        refDoc.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        refDoc.AddTransform(new XmlDsigExcC14NTransform());
        refDoc.DigestMethod = SignedXml.XmlDsigSHA256Url;
        signedXml.AddReference(refDoc);

        // KeyInfo: incluir el certificado X.509
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signedXml.KeyInfo = keyInfo!;

        // Calcular firma
        signedXml.ComputeSignature();
        var signatureElement = signedXml.GetXml();

        // Insertar la firma dentro del primer ExtensionContent (reservado para firma)
        var nsManager = new XmlNamespaceManager(doc.NameTable);
        nsManager.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");

        var extensionContent = doc.SelectSingleNode("//ext:UBLExtensions/ext:UBLExtension[1]/ext:ExtensionContent", nsManager);
        if (extensionContent != null)
        {
            // Limpiar el comentario placeholder
            extensionContent.InnerXml = "";
            var importedNode = doc.ImportNode(signatureElement, true);
            extensionContent.AppendChild(importedNode);
        }
        else
        {
            // Fallback: agregar la firma al final del documento raíz
            var importedNode = doc.ImportNode(signatureElement, true);
            doc.DocumentElement!.AppendChild(importedNode);
        }

        return doc.OuterXml;
    }
}
