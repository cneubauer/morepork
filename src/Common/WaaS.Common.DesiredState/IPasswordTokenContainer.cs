namespace WaaS.Common.DesiredState;

public interface IPasswordTokenContainer
{
    IEnumerable<string> GetPasswordTokens();
}
