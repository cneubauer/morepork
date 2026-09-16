namespace WaaS.Common.ViewModel;

public record PasswordInfo(Credential Credential, PasswordType PasswordType);

public interface IPasswordContainer
{
    IEnumerable<PasswordInfo> GetPasswordInfos();
}