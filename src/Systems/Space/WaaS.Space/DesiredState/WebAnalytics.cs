using System.Text.Json.Serialization;
using WaaS.Common.DesiredState;

namespace WaaS.Space.DesiredState;

public class WebAnalytics : ICredential
{
    [JsonPropertyName("waId")]
    public required string WebAnalyticsId { get; set; }

    public string? SecurePasswordToken { get; set; }
}