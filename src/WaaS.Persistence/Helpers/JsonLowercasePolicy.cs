using System.Text.Json;

namespace WaaS.Persistence;

internal sealed class JsonLowercasePolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        return name.ToLowerInvariant();
    }
}