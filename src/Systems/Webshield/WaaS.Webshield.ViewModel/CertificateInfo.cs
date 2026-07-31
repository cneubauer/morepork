namespace WaaS.Webshield.ViewModel;

/// <summary>The SSL certificate for this specific domain.</summary>
public class CertificateInfo
{
    /// <summary>
    /// The certificate fingerprint. Calculated by the server. Can be used to identify or remove a certificate.
    /// </summary>
    /// <example>542e5e83d1a91bxxd0b28b278x5e5996991d65</example>
    public string? Id { get; set; }

    /// <summary>
    /// The certificate in PEM format. Leave unset to keep the existing certificate.
    /// </summary>
    /// <example>-----BEGIN CERTIFICATE-----\nMIIFvbqHqkTPQn [...]</example>
    public string? Certificate { get; set; }

    /// <summary>
    /// The RSA private key in PEM format. Must be set when <c>Certificate</c> is set. Never returned by the server.
    /// </summary>
    /// <example>-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQ [...]</example>
    public string? Key { get; set; }
}
