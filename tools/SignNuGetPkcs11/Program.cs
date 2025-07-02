 /*
 * Tool to sign NuGet Package using a PKCS#11 library
 *
 * Digicert's smctl tool doesn't support signing NuGet packages on a Linux build system (only Windows), so this tool uses their smpkcs11.so to generate the signature instead.
 * It requires a code siging cert from Digicert with an authentication cert and Digicert ONE api key. It also requires the smctl package from their site (Linux or MacOS).
 *
 * Make sure the following environment variables are set:
 *
 *               PKCS11_LIB: location of the smpkcs11.so library (or smpkcs11.dylib on macOS)
 *               SM_API_KEY: Digicert ONE API key token
 *      SM_CLIENT_CERT_FILE: Location of PKCS#12 file containing the client authentication certificate
 *  SM_CLIENT_CERT_PASSWORD: Password for the PKCS#12 file
 *                  SM_HOST: The hostname of the Digicert ONE service e.g. https://clientauth.one.digicert.com/
 *
 *  Optional diagnostics:
 *            SM_LOG_OUTPUT: stdout
 *             SM_LOG_LEVEL: TRACE
 *
 * You can use "smctl healthcheck" to verify that smpkcs11.so lib can authenticate correctly.
 */

using System.CommandLine;
using System.Security.Cryptography.X509Certificates;
using Net.Pkcs11Interop.X509Store;
using NuGet.Common;
using NuGet.Packaging.Signing;
using SignNuGetPkcs11;

Pkcs11X509Certificate? GetCertAndChainForThumbprint(Pkcs11X509Store pkcs11Store, string thumbprint, out List<X509Certificate2> certs)
{
    Pkcs11X509Certificate? certToSignWith = null;
    certs = new List<X509Certificate2>();

    foreach (var slot in pkcs11Store.Slots)
    {
        Console.WriteLine($"Pkcs11 Slot: label=[{slot.Token?.Info.Label ?? ""}] manufacturer=[{slot.Info.Manufacturer}] desc=[{slot.Info.Description}]");

        if (slot.Token == null)
            continue;

        var tokenInfo = slot.Token.Info;
        Console.WriteLine($"Pkcs11 Token: manufacturer=[{tokenInfo.Manufacturer}] model=[{tokenInfo.Model}] serial=[{tokenInfo.SerialNumber}] label=[{tokenInfo.Label}] initialized=[{tokenInfo.Initialized}]");

        if (!slot.Token.Info.Initialized)
            continue;

        foreach (var cert in slot.Token.Certificates)
        {
            var certInfo = cert.Info.ParsedCertificate;
            certs.Add(certInfo);
            Console.WriteLine($"\n    Cert\n    s=[{certInfo.Subject}]\n    i=[{certInfo.Issuer}]\n    sn=[{certInfo.SerialNumber}]\n    nb=[{certInfo.NotBefore}]\n    na=[{certInfo.NotAfter}]\n    thumb=[{certInfo.Thumbprint}]\n    type=[{cert.Info.KeyType}]");

            if (cert.Info.ParsedCertificate.Thumbprint.ToUpper().Equals(thumbprint.ToUpper()))
            {
                certToSignWith = cert;

                if (!certToSignWith.HasPrivateKeyObject)
                    throw new Exception("Cert has no priviate key reference " + thumbprint);

                if (!certToSignWith.HasPublicKeyObject)
                    throw new Exception("Cert has no public key " + thumbprint);

                Console.WriteLine("    <-- Using this certificate for signing -->");
            }
        }
    }

    return certToSignWith;
}

var sigHashAlgorithm = HashAlgorithmName.SHA256;
var timestampHashAlgorithm = HashAlgorithmName.SHA256;
IPinProvider pinProvider = new PinProvider();

Option<FileInfo> pkcs11LibOption = new("--pkcs11-lib") { Description = "Path to the PKCS#11 library (e.g. smpkcs11.so on Linux, smpkcs11.dylib on macOS).", Required = true, };
Option<FileInfo> fileOption = new("--file") { Description = "NuGet package file to sign. This file will be overwritten with the signed package.", Required = true };
Option<string> fingerprintOption = new("--fingerprint") { Description = "The thumbprint of the certificate to use for signing.", Required = true };
Option<string> timestampUrlOption = new("--timestamp-url") { Description = "The URL of the RFC3161 timestamp server to use for signing.", DefaultValueFactory = _ => "http://timestamp.digicert.com" };

RootCommand rootCommand = new("Sign NuGet Package using a PKCS#11 library");
rootCommand.Options.Add(pkcs11LibOption);
rootCommand.Options.Add(fileOption);
rootCommand.Options.Add(fingerprintOption);
rootCommand.Options.Add(timestampUrlOption);

var parseResult = rootCommand.Parse(args);
var exitCode = await parseResult.InvokeAsync();

if (exitCode != 0)
{
    Environment.ExitCode = exitCode;
    Console.WriteLine("Error parsing command line arguments.");
    return;
}

if (!parseResult.GetRequiredValue(pkcs11LibOption).Exists) throw new ArgumentException($"PKCS#11 library not found: {parseResult.GetRequiredValue(pkcs11LibOption).FullName}");
if (!parseResult.GetRequiredValue(fileOption).Exists) throw new ArgumentException($"NuGet package not found: {parseResult.GetRequiredValue(fileOption).FullName}");

var pkcs11Lib = parseResult.GetRequiredValue(pkcs11LibOption).FullName;
var pkgPath = parseResult.GetRequiredValue(fileOption).FullName;
var timestampUrl = parseResult.GetRequiredValue(timestampUrlOption);
var certFingerprint = parseResult.GetRequiredValue(fingerprintOption);

using var pkcs11Store = new Pkcs11X509Store(pkcs11Lib, pinProvider);
Console.WriteLine($"Pkcs11 Lib: path=[{pkcs11Store.Info.LibraryPath}] manufacturer=[{pkcs11Store.Info.Manufacturer}] desc=[{pkcs11Store.Info.Description}]");

List<X509Certificate2> chain;
var certToSignWith = GetCertAndChainForThumbprint(pkcs11Store, certFingerprint, out chain);

if (certToSignWith == null)
    throw new Exception("Certificate not found: " + fingerprintOption);

var destFilePath = Path.GetRandomFileName();

Console.WriteLine($"Signing package {pkgPath} with cert fingerprint {certFingerprint} [{timestampUrl}]...");
var sigProvider = new Pkcs11SignatureProvider(certToSignWith, chain, new Rfc3161TimestampProvider(new Uri(timestampUrl)));
var req = new AuthorSignPackageRequest(certToSignWith.Info.ParsedCertificate, sigHashAlgorithm, timestampHashAlgorithm);
using (var options = SigningOptions.CreateFromFilePaths(pkgPath, destFilePath, true, sigProvider, new SignLogger()))
{
    await SigningUtility.SignAsync(options, req, CancellationToken.None);
}
File.Copy(destFilePath, pkgPath, overwrite: true);
Console.WriteLine("Done.");