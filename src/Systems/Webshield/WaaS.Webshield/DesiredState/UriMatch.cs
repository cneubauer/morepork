namespace WaaS.Webshield.DesiredState;

public class UriMatch
{
    public string Prefix { get; set; } = "";
    public UriMatchType Type { get; set; }
    public UriProtocol Protocol { get; set; }
}
