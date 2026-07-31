namespace WaaS.Space.Stretch.DesiredState;

public class HomedirAlias
{
    public string? Type { get; set; }

    public AliasTarget? Target { get; set; }

    public LinkPath? LinkPath { get; set; }
}

public class AliasTarget
{
    public string? Type { get; set; }

    public string? Path { get; set; }
}

public class LinkPath
{
    public string? Type { get; set; }

    public string? Path { get; set; }
}