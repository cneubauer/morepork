namespace WaaS.Webshield.DesiredState;

public class Webshield
{
    public List<Mapping> Mappings { get; set; } = [];
    public List<Certificate> Certificates { get; set; } = [];
    public List<string> PublicIPs { get; set; } = [];
    public List<string> CdnIPs { get; set; } = [];
    /// <summary>
    /// Tracks the state version per WebshieldType to avoid redundant publishes.
    /// Key is WebshieldType cast to int.
    /// </summary>
    public Dictionary<int, ulong> SubVersions { get; set; } = [];
}
