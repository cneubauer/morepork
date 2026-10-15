using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.Stretch.ViewModel;

// based on https://git.ionos.org/WC/waas-password-store/src/branch/master/PasswordStore.Model/HashType.cs
public enum PasswordHashType
{
    SHA256 = 1,
    SHA512 = 2
}

public class PasswordHash
{
    /// <summary>
    /// The hashing algorithm used.
    /// </summary>
    /// <example>SHA512</example>
    [Required]
    public PasswordHashType? HashType { get; set; }

    /// <summary>
    /// The hashed password value.
    /// </summary>
    /// <example>$6$rounds=5000$salt$hash...</example>
    [Required]
    public string? Hash { get; set; }
}
