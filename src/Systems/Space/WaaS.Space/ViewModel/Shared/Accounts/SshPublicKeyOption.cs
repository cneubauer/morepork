using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

public class SshPublicKeyOption : IValidatableObject
{
    /// <summary>
    /// The SSH authorized_keys option type.
    /// </summary>
    /// <example>principals</example>
    [Required]
    [Options("command", "principals", "cert-authority", "no-user-rc")]
    public string? KeyType { get; set; }

    /// <summary>
    /// The value for the option, if required.
    /// </summary>
    /// <example>admin,developer</example>
    [MaxLength(256)]
    public string? Value { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KeyType == "principals" && string.IsNullOrEmpty(Value))
        {
            yield return new ValidationResult(
                $"SSH option value is expected for {KeyType}",
                [nameof(KeyType)]);
        }

        if (KeyType == "no-user-rc" && !string.IsNullOrEmpty(Value))
        {
            yield return new ValidationResult(
                $"SSH option value is not allowed for {KeyType}",
                [nameof(KeyType)]);
        }
    }
}
