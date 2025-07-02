namespace SignNuGetPkcs11;

using System;
using System.Collections.Generic;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using Net.Pkcs11Interop.X509Store;

internal class Pkcs11SignatureProvider(
    Pkcs11X509Certificate signingCert,
    List<X509Certificate2> chain,
    ITimestampProvider timestampProvider)
    : ISignatureProvider
{
    private readonly ITimestampProvider _tsProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));

    public async Task<PrimarySignature> CreatePrimarySignatureAsync(SignPackageRequest request, SignatureContent signatureContent, ILogger logger, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signatureContent);
        ArgumentNullException.ThrowIfNull(logger);
        if (request.SignatureType != SignatureType.Author)
            throw new NotSupportedException("SignatureType is not supported in this implementation: " + request.SignatureType);

        // primary author signature
        CmsSigner cmsSigner = request.Certificate.Extensions[Oids.SubjectKeyIdentifier] == null ?
            new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, request.Certificate, signingCert.GetPrivateKey()) :
            new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, request.Certificate, signingCert.GetPrivateKey());

        cmsSigner.IncludeOption = X509IncludeOption.None;
        cmsSigner.DigestAlgorithm = request.SignatureHashAlgorithm.ConvertToOid();
        cmsSigner.Certificates.AddRange(chain.ToArray());

        foreach (var attr in SigningUtility.CreateSignedAttributes(request, chain)) {
            cmsSigner.SignedAttributes.Add(attr);
        }

        var contentInfo = new ContentInfo(signatureContent.GetBytes());
        var cms = new SignedCms(contentInfo);
        cms.ComputeSignature(cmsSigner, true);
        var authorSignature = PrimarySignature.Load(cms);

        // timestamp the primary author signature
        var req = new TimestampRequest(
            signingSpecifications: SigningSpecifications.V1,
            hashedMessage: request.TimestampHashAlgorithm.ComputeHash(authorSignature.GetSignatureValue()),
            hashAlgorithm: request.TimestampHashAlgorithm,
            target: SignaturePlacement.PrimarySignature
        );

        return await _tsProvider.TimestampSignatureAsync(authorSignature, req, logger, token);
    }

    public Task<PrimarySignature> CreateRepositoryCountersignatureAsync(RepositorySignPackageRequest request, PrimarySignature primarySignature, ILogger logger, CancellationToken token)
    {
        throw new NotImplementedException("Repository signatures are not supported in this implementation.");
    }
}

