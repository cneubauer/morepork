using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

public class SshPublicKey : IValidatableObject
{
    /// <summary>
    /// SSH public key data.
    /// </summary>
    /// <example>AAAAB3NzaC1yc2EAAAADAQABAAAB...</example>
    [Required]
    [RegularExpression(@"^[A-Za-z0-9+\/]+=*$(?!\n)")]
    [StringLength(2764, MinimumLength = 68)]
    public required string Data { get; set; }

    /// <summary>
    /// SSH public key type.
    /// </summary>
    /// <example>ssh-ed25519</example>
    [Required]
    [Options("ssh-rsa", "ssh-ed25519", "ecdsa-sha2-nistp256", "ecdsa-sha2-nistp384", "ecdsa-sha2-nistp521")]
    public required string KeyType { get; set; }


    /// <summary>
    /// Additional public key options
    /// see https://confluence.united-internet.org/display/WaaS/Managed+SSH+Public+Keys#ManagedSSHPublicKeys-APIRepresentationofSSHPub-Keys
    /// </summary>
    public List<SshPublicKeyOption>? Options { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Options is null)
            yield break;

        var duplicateKeys = Options
            .GroupBy(x => x.KeyType)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key);

        if (duplicateKeys.Any())
        {
            yield return new ValidationResult(
                $"Duplicate SSH option detected for {string.Join(", ", duplicateKeys)}",
                [nameof(Options)]);
        }
    }
}
