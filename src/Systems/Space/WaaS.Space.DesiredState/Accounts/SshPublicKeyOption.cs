using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.DesiredState;

public class SshPublicKeyOption
{
    /// <summary>
    /// SSH public key option type. See also PublicKeyOptionType.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string Key { get; set; } = "";

    /// <summary>
    /// Configure your value as you would type it on the shell. Some options don't have values such as no-user-rc.
    /// </summary>
    [Required]
    [StringLength(70, MinimumLength = 1)]
    public string Value { get; set; } = "";
}