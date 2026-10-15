using WaaS.Common.ViewModel;

namespace WaaS.Common.Workflow;

public interface IPasswordService
{
    Task CreatePasswordTokens(string tenantName, ulong stackInstanceId, ulong systemInstanceId, IEnumerable<PasswordInfo> passwordInfos, CancellationToken cancellationToken);
    Task<ListTokenItem[]> ListPasswordTokens(string tenant, ulong stackInstanceId, ulong systemInstanceId, CancellationToken cancellationToken = default);
    Task DeletePasswordTokens(string tenant, IEnumerable<string> tokens);
}