using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;
using WaaS.Space.ViewModel;

namespace WaaS.Space.Stretch.ViewModel;

public class Account : Space.ViewModel.Account
{
    /// <summary>
    /// The environment to use, when user logs in.
    /// </summary>
    [Required]
    public Environment Environment { get; set; } = new();

    /// <summary>
    /// The path information the user has access to.
    /// </summary>
    [Required]
    public TargetDirectory TargetPath { get; set; } = new();

    /// <summary>
    /// The password for the account.
    /// </summary>
    /// <example>*****</example>
    [PasswordType(PasswordType.StretchSpace)]
    public override string? Password { get; set; }

    /// <summary>
    /// Only allowed to set on import. The allowed hashes to be set are configured in tenant profile.
    /// </summary>
    [MaxLength(1)]
    public List<PasswordHash>? PasswordHashes { get; set; }

    /// <summary>
    /// Filesystem view for SFTP access.
    /// </summary>
    /// <example>filtered</example>
    [Options("full", "chrooted", "filtered", AllowNull = true)]
    public string? SftpView { get; set; } = "filtered";

    /// <summary>
    /// Filesystem view for SSH access.
    /// </summary>
    /// <example>filtered</example>
    [Options("full", "filtered", AllowNull = true)]
    public string? SshView { get; set; } = "filtered";

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext))
            yield return result;

        if (!AccountType!.Equals("standard"))
        {
            if (SshView != "full")
                yield return new ValidationResult(
                    $"The sshView of an account which is not of the type standard can only be 'full'",
                    [nameof(AccountType), nameof(SshView)]);

            if (SftpView != "full")
                yield return new ValidationResult(
                    $"The sftpView of an account which is not of the type standard can only be 'full'",
                    [nameof(AccountType), nameof(SftpView)]);
        }

        if (!AccountType!.Equals("standard"))
        {
            if (!TargetPath.Path.Equals("/"))
                yield return new ValidationResult(
                    $"The access path of an account which is not of the type standard can only be '/'",
                    [nameof(AccountType), nameof(TargetPath.Path)]);
        }
    }
}