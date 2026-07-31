namespace WaaS.Common.ViewModel;

public enum PasswordType
{
    SharedWebspaceLinux = 100,
    StretchSpace = 110,
    SharedWebspaceWindows = 150,
    DatabaseMySql = 200,
    DatabaseMariaDB = 220,
    DatabaseMSSql = 250,
    Smtp = 300,
    WebAnalytics = 350
}

[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class PasswordTypeAttribute(PasswordType passwordType) : Attribute
{
    public PasswordType PasswordType { get; } = passwordType;
}
