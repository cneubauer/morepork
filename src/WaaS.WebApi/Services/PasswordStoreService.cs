using System.Reflection;
using ObjectCompare;
using WaaS.Common.ViewModel;

namespace WaaS.WebApi;

record PasswordStoreResponse(string Token);

public record PasswordTokenChange(PasswordType PasswordType, string? OldToken, string? NewToken) : IChange
{
    public string Path => "Credential.PasswordToken";
    public ChangeKind Kind => ChangeKind.Property;
}


public class PasswordService(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("PasswordStore");

    public async Task<IReadOnlyList<PasswordTokenChange>> ConvertCredentials(string tenant, ulong stackInstanceId, ulong systemInstanceId, IEnumerable<Credential> credentials)
    {
        var tasks = credentials
            .Select(credential => ConvertCredential(tenant, stackInstanceId, systemInstanceId, credential));

        var results = await Task.WhenAll(tasks);

        return results
            .Where(x => x is not null)
            .Cast<PasswordTokenChange>()
            .ToList()
            .AsReadOnly();
    }

    public async Task<PasswordTokenChange?> ConvertCredential(string tenant, ulong stackInstanceId, ulong systemInstanceId, Credential credential)
    {
        if (credential.Password is null)
            return null;

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

        var result = new PasswordTokenChange(systemType, credential.PasswordToken, newToken);
        
        credential.PasswordToken = newToken;
        credential.Reset();

        return result;
    }

    public async Task DeletePasswordToken(string tenant, PasswordType systemType, string token)
    {
        var response = await _httpClient.DeleteAsync($"/credential/v2/{tenant}/systemtype/{systemType}/token/{token}");
        response.EnsureSuccessStatusCode();
    }
}