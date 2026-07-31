namespace WaaS.Webshield.DesiredState;

public class Certificate
{
    public uint CertificateId { get; set; }
    public string CertificateData { get; set; } = "";
    public string? PrivateKey { get; set; }
    public List<string> CertificateChain { get; set; } = [];
    public byte[]? EncryptedPrivateKey { get; set; }
    public byte[]? OcspStapling { get; set; }
}
