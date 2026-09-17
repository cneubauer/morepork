using System.Text.Json;
using WaaS.Common.ViewModel;

namespace WaaS.Common.Workflow;

record TokenInfo(string ReferenceId, string Token);
record TokensResponse(TokenInfo[] Tokens);

public class PasswordService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

    };

    public async Task<IEnumerable<string>> ConvertCredentials(string tenant, ulong stackInstanceId, ulong systemInstanceId, IEnumerable<PasswordInfo> passwordInfos)
    {
        var passwordsToConvert = passwordInfos
            .Where(passwordInfo => passwordInfo.Credential.Password is not null);

        var response = await httpClient.PutAsJsonAsync($"credential/v3/{tenant}/tokens", new
        {
            passwordInfos = passwordsToConvert.Select(x => new
            {
                referenceId = x.ReferenceId,
                password = x.Credential.Password,
                systemType = x.PasswordType,
                owner = new
                {
                    stackInstanceId,
                    systemInstanceId,
                }
            }),
        }, _jsonOptions);

        response.EnsureSuccessStatusCode();
        
        var tokenResult = await response.Content.ReadFromJsonAsync<TokensResponse>()
            ?? throw new InvalidOperationException("Unexpected response from Password Store.");

        foreach (var passwordInfo in passwordsToConvert)
        {
            var token = tokenResult
                .Tokens
                .FirstOrDefault(x => x.ReferenceId == passwordInfo.ReferenceId)?
                .Token
                ?? throw new InvalidOperationException($"Token for reference ID '{passwordInfo.ReferenceId}' not found.");
            
            passwordInfo.Credential.PasswordToken = token;
            passwordInfo.Credential.Reset();
        }

        return tokenResult.Tokens.Select(x => x.Token);
    }
    
    public async Task CleanupPasswordTokens(string tenant, IEnumerable<string> excludeTokens)
    {
        var response = await httpClient.PutAsJsonAsync($"credential/v3/{tenant}/tokens/cleanup", new
        {
            exclude = excludeTokens,
        });

        response.EnsureSuccessStatusCode();
    }
}