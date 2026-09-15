using System.Reflection;
using WaaS.Common.DesiredState;
using WaaS.Common.ViewModel;

namespace WaaS.Common.Workflow;

record PasswordStoreResponse(string Token);

public class PasswordService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("PasswordStore");

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

        var systemType = credential.Password
            .GetType()
            .GetCustomAttribute<PasswordTypeAttribute>(true)?
            .PasswordType
            ?? throw new InvalidOperationException("Password type could not be determined.");

        var response = await _httpClient.PutAsJsonAsync($"credential/v2/{tenant}/systemtype/{systemType}/token", new
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
        var response = await _httpClient.DeleteAsync($"/credential/v2/{tenant}/systemtype/{systemType}/token/{token}");
        response.EnsureSuccessStatusCode();
    }
    
    public async Task CleanupPasswordTokens(string tenant, ulong stackInstanceId, ulong systemInstanceId, ICredentialContainer container)
    {
        var remainingPasswordTokens = container
            .GetCredentials()
            .Where(credential => credential.SecurePasswordToken is not null)
            .Select(x => x.SecurePasswordToken!);
        
        var response = await _httpClient.PutAsJsonAsync($"/credential/v2/{tenant}/stack-instance/{stackInstanceId}/system-instance/{systemInstanceId}/cleanup", new
        {
            exclude = remainingPasswordTokens,
        });

        response.EnsureSuccessStatusCode();
    }
}