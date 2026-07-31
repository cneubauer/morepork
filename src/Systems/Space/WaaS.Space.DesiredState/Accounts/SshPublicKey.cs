using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.DesiredState;

public class SshPublicKey
{
    /// <summary>
    /// SSH public key data.
    /// </summary>
    [Required]
    [RegularExpression(@"^[A-Za-z0-9+\/]+=*$(?!\n)")]
    [StringLength(2764, MinimumLength = 68)]
    public required string Data { get; set; }

    /// <summary>
    /// SSH public key type. See also SshPublicKeyType.
    /// </summary>
    [Required]
    public required string KeyType { get; set; }

    /// <summary>
    /// Additional public key options
    /// see https://confluence.united-internet.org/display/WaaS/Managed+SSH+Public+Keys#ManagedSSHPublicKeys-APIRepresentationofSSHPub-Keys
    /// </summary>
    [MaxLength(10)]
    public List<SshPublicKeyOption> Options { get; set; } = [];
}