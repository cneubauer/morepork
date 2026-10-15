using System.Text.Json;
using WaaS.Common.ViewModel;

namespace WaaS.Common.Workflow;

record TokenInfo(string ReferenceId, string Token);
record TokensResponse(TokenInfo[] Tokens);

public record ListTokenItem(string Token, DateTime Created);
record ListTokensResponse(ListTokenItem[] Items, int Total, int Limit, int Offset);

public class PasswordService(HttpClient httpClient) : IPasswordService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // This method processes sensitive data and must not be temporal activity, unless the payloads are encrypted.
    // [Activity]
    public async Task CreatePasswordTokens(string tenant, ulong stackInstanceId, ulong systemInstanceId, IEnumerable<PasswordInfo> passwordInfos, CancellationToken cancellationToken)
    {
        var passwordsToConvert = passwordInfos
            .Where(passwordInfo => passwordInfo.Credential.Password is not null)
            .ToList();

        if (passwordsToConvert.Count == 0)
            return;
        
        // Keep the timeout short to avoid blocking too long
        httpClient.Timeout = TimeSpan.FromSeconds(5);

        var response = await httpClient.PostAsJsonAsync($"credential/v3/{tenant}/tokens", new
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
    }
 
    public async Task<ListTokenItem[]> ListPasswordTokens(string tenant, ulong stackInstanceId, ulong systemInstanceId, CancellationToken cancellationToken = default)
    {
        var tokens = await httpClient.GetFromJsonAsync<ListTokensResponse>(
            $"credential/v3/{tenant}/stack-instances/{stackInstanceId}/system-instances/{systemInstanceId}/tokens", 
            cancellationToken
        ) ?? throw new InvalidOperationException("Unexpected response from Password Store.");
        
        return tokens.Items;
    }

    public async Task DeletePasswordTokens(string tenant, IEnumerable<string> tokens)
    {
        var response = await httpClient.PutAsJsonAsync($"credential/v3/{tenant}/tokens/delete", new
        {
            tokens,
        });

        response.EnsureSuccessStatusCode();
    }
}