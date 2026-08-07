using WaaS.Persistence;

namespace WaaS.Webshield.DesiredState;

[DesiredStateData(Namespace = 1)] // NamespaceType.Proxy
public class WebshieldData : Webshield, IDesiredStateData
{
    public DateTime? GetNextCheck() => null;
}
