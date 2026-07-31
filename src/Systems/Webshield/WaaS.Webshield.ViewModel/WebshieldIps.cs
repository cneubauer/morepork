namespace WaaS.Webshield.ViewModel;

/// <summary>IP addresses assigned to the Webshield nodes.</summary>
public class WebshieldIps
{
    /// <summary>IPv4 addresses of the Webshield nodes.</summary>
    public string[] IPv4 { get; set; } = [];

    /// <summary>IPv6 addresses of the Webshield nodes, if available.</summary>
    public string[]? IPv6 { get; set; }
}
