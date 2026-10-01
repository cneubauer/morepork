using System.Text.Json;

namespace WaaS.WebApi;

/// <summary>
/// Serializes enum member names using the lowercase API representation.
/// </summary>
public sealed class LowercaseJsonNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) => name.ToLowerInvariant();
}
