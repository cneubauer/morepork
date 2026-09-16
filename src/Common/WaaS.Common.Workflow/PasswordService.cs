using WaaS.Common.ViewModel;

namespace WaaS.Common.Workflow;

record PasswordStoreResponse(string Token);

public class PasswordService(HttpClient httpClient)
{
    public async Task ConvertCredentials(string tenant, ulong stackInstanceId, ulong systemInstanceId, IEnumerable<PasswordInfo> passwordInfos)
    {
        var tasks = passwordInfos
            .Where(passwordInfo => passwordInfo.Credential.Password is not null)
            .Select(passwordInfo => ConvertCredential(tenant, stackInstanceId, systemInstanceId, passwordInfo.Credential, passwordInfo.PasswordType));

        await Task.WhenAll(tasks);
    }

    public async Task ConvertCredential(string tenant, ulong stackInstanceId, ulong systemInstanceId, Credential credential, PasswordType systemType)
    {
        if (credential.Password is null)
            return;

        var response = await httpClient.PutAsJsonAsync($"credential/v2/{tenant}/systemtype/{systemType}/token", new
        {
            passwordInfo = new
            {
                password = credential.Password,
            },
            ownerData = new
            {
                stackInstanceId,
                systemInstanceId,
            }
        });

        response.EnsureSuccessStatusCode();
        
        var tokenResult = await response.Content.ReadFromJsonAsync<PasswordStoreResponse>();

        var newToken = tokenResult?.Token ?? throw new InvalidOperationException("Unexpected response from Password Store.");

        // We set the token and reset the password here to ensure it is no longer stored in memory and avoid passing it around unnecessarily.
        credential.PasswordToken = newToken;
        credential.Reset();
    }
    
    public async Task CleanupPasswordTokens(string tenant, ulong stackInstanceId, ulong systemInstanceId, IEnumerable<string> excludeTokens)
    {
        var response = await httpClient.PutAsJsonAsync($"credential/v2/{tenant}/stack-instance/{stackInstanceId}/system-instance/{systemInstanceId}/cleanup", new
        {
            exclude = excludeTokens,
        });

        response.EnsureSuccessStatusCode();
    }
}