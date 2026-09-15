using System.Reflection;
using WaaS.Common.DesiredState;
using WaaS.Common.ViewModel;

namespace WaaS.Common.Workflow;

record PasswordStoreResponse(string Token);

public class PasswordService(HttpClient httpClient)
{
    public async Task ConvertCredentials(string tenant, ulong stackInstanceId, ulong systemInstanceId, ICredentials credentials)
    {
        var tasks = credentials
            .GetCredentials()
            .Where(credential => credential.Password is not null)
            .Select(credential => ConvertCredential(tenant, stackInstanceId, systemInstanceId, credential));

        await Task.WhenAll(tasks);
    }

    public async Task ConvertCredential(string tenant, ulong stackInstanceId, ulong systemInstanceId, Credential credential)
    {
        if (credential.Password is null)
            return;

        var systemType = credential
            .GetType()
            .GetProperty(nameof(Credential.Password))?
            .GetCustomAttribute<PasswordTypeAttribute>()?
            .PasswordType
            ?? throw new InvalidOperationException("Password type could not be determined.");

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

        credential.PasswordToken = newToken;
        credential.Reset();
    }

    public async Task DeletePasswordToken(string tenant, PasswordType systemType, string token)
    {
        var response = await httpClient.DeleteAsync($"credential/v2/{tenant}/systemtype/{systemType}/token/{token}");
        response.EnsureSuccessStatusCode();
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