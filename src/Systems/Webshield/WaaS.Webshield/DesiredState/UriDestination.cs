namespace WaaS.Webshield.DesiredState;

public class UriDestination
{
    public DestinationType Type { get; set; }
    public string Target { get; set; } = "";
    public SniMode SniMode { get; set; }
}
