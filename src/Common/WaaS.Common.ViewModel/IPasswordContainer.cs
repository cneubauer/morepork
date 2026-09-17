namespace WaaS.Common.ViewModel;

public record PasswordInfo(string ReferenceId, Credential Credential, PasswordType PasswordType)
{
    public PasswordInfo(Credential credential, PasswordType passwordType) : this(Guid.NewGuid().ToString(), credential, passwordType) {}
}

public interface IPasswordContainer
{
    IEnumerable<PasswordInfo> GetPasswordInfos();
}