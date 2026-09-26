using System.Text.Json;
using WaaS.Common.ViewModel;

namespace WaaS.Common.Workflow;

record TokenInfo(string ReferenceId, string Token);
record TokensResponse(TokenInfo[] Tokens);

public class PasswordActivities(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // This method processes sensitive data and must not be temporal activity, unless the payloads are encrypted.
    // [Activity]
    public async Task<IEnumerable<string>> ConvertCredentials(string tenant, ulong stackInstanceId, ulong systemInstanceId, IEnumerable<PasswordInfo> passwordInfos, CancellationToken cancellationToken = default)
    {
        var passwordsToConvert = passwordInfos
            .Where(passwordInfo => passwordInfo.Credential.Password is not null)
            .ToList();

        if (passwordsToConvert.Count == 0)
            return [];

        var response = await httpClient.PutAsJsonAsync($"credential/v3/{tenant}/tokens?transactional=true", new
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
        }, _jsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();
        
        var tokenResult = await response.Content.ReadFromJsonAsync<TokensResponse>(cancellationToken: cancellationToken)
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

    [Activity]
    public async Task CommitPasswordTokens(string tenant, IEnumerable<string> tokens)
    {
        var response = await httpClient.PutAsJsonAsync($"credential/v3/{tenant}/tokens/commit", new
        {
            tokens,
        });

        response.EnsureSuccessStatusCode();
    }

    [Activity]
    public async Task DeletePasswordTokens(string tenant, IEnumerable<string> tokens)
    {
        var response = await httpClient.PutAsJsonAsync($"credential/v3/{tenant}/tokens/delete", new
        {
            tokens,
        });

        response.EnsureSuccessStatusCode();
    }
}