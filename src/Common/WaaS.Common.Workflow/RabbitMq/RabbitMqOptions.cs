namespace WaaS.Common.Workflow;

public class RabbitMqOptions
{
    public string Hostname { get; set; } = "";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "";
    public string Queue { get; set; } = "";
    public string[] RoutingKeys { get; set; } = [];
}
