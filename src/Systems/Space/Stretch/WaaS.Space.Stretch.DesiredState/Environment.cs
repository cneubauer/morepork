namespace WaaS.Space.Stretch.DesiredState;

public class Environment
{
    public string Image { get; set; } = "";
    public string Version { get; set; } = "";
    public string EnvironmentProfile { get; set; } = "";

    public override string ToString() => Image;
}