namespace WaaS.Space.Stretch.DesiredState;

public class PlatformAccessToken
{
    public string? Pubkey { get; set; }

    public Scope? Scope { get; set; } = new Scope();
}

public class Scope
{
    public int Version { get; set; } = 0;
}