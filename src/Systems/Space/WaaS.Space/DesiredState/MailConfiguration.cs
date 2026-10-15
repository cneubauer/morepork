using WaaS.Common.DesiredState;

namespace WaaS.Space.DesiredState;

public class MailConfiguration : ICredential
{
    public string Host { get; set; } = "";
    public uint Hostport { get; set; }
    public string Username { get; set; } = "";
    public string? SecurePasswordToken { get; set; }
    public string? DefaultSender { get; set; }
    public string? DefaultEnvelopeFromPolicy { get; set; }
}