namespace WaaS.Space.DesiredState;

public class DataAccessDomainBinding
{
    public required string DomainName { get; set; }
    public override string ToString() => DomainName;
}
