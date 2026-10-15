using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class Account : ResourceBase
{
    /// <summary>
    /// IMMUTABLE READONLY
    /// </summary>
    [JsonPropertyName("account_id")]
    [ReadOnly(true)]
    public ulong? Id { get; set; }

    /// <summary>
    /// IMMUTABLE READONLY IMPORTABLE
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// WRITABLE set (list) "ssh|sftp"
    /// </summary>
    [JsonPropertyName("access_type")]
    public IEnumerable<string>? AccessTypes { get; set; }

    [JsonPropertyName("credentials")]
    public Credential? Credentials { get; set; }

    [JsonPropertyName("target")]
    public FileSystemTarget? Target { get; set; }

    [JsonPropertyName("homedir_pubkeys")]
    public bool? HomeDirPubKeys { get; set; } = true;

    [JsonPropertyName("environment")]
    public Environment? Environment { get; set; }

    [JsonPropertyName("sftp_view")]
    public string? SftpView { get; set; }

    [JsonPropertyName("ssh_view")]
    public string? SshView { get; set; }

    [JsonPropertyName("account_type")]
    public string? AccountType { get; set; }
}
