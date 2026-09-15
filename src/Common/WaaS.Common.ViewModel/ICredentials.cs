namespace WaaS.Common.ViewModel;

public interface ICredentials
{
    IEnumerable<Credential> GetCredentials();
}