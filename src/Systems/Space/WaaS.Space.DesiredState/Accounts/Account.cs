using WaaS.Common.DesiredState;

namespace WaaS.Space.DesiredState;

public class Account : WaasResource, ICredential
{
    public ulong? AccountId { get; set; }
    public string Username { get; set; } = "";
    public string State { get; set; } = "unknown";
    public string AccessType { get; set; } = "";
    public bool ForceEnabled { get; set; } = false;
    public TargetDirectory TargetPath { get; set; } = new();
    public string? SecurePasswordToken { get; set; }
    public bool HomeDirPubKeys { get; set; } = true;
    public List<SshPublicKey> SshPublicKeys { get; set; } = [];
    public ExpirationInfo? Temporary { get; set; }

    /// <summary>
    /// GPHWAAS-7264: a list of locks
    /// </summary>
    public List<LockItem> LockItems { get; set; } = [];

    /// <summary>
    /// Optional data of the client for reference purposes.
    /// </summary>
    public string? ExtReference { get; set; }

    /// <summary>
    /// Type of the account to identify the special accounts within the platform. 
    /// </summary>
    public string AccountType { get; set; } = "standard";
    public Metadata? Metadata { get; set; }

    public DateTime? CalculateNextCheckTimestamp() => Temporary?.Expires;

}