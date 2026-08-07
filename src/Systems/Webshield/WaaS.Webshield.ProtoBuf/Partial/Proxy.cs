namespace WaaS.Webshield.ProtoBuf;

public partial class DesiredStateProxy
{
    public string GetRoutingKey()
    {
        if (header is null) throw new InvalidOperationException("Desired state header not set.");
        var zone = header.stateZone switch
        {
            StateHeader.Zone.DE => "De",
            StateHeader.Zone.US => "Us",
            StateHeader.Zone.ES => "Es",
            StateHeader.Zone.UK => "Uk",
            StateHeader.Zone.MM => "Mm",
            StateHeader.Zone.GLOBALCDN => "GlobalCdn",
            _ => throw new InvalidOperationException($"Unknown zone: {header.stateZone}")
        };
        return $"DesiredState.Proxy.{zone}";
    }
}
