using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

public class MailConfiguration : Credential, IValidatableObject
{
    /// <summary>
    /// The hostname or IP address of the outgoing mail relay server.
    /// </summary>
    /// <example>smtp.example.com</example>
    [Required]
    [Domain]
    public required string Host { get; set; } = "localhost";

    /// <summary>
    /// The port for sending mails
    /// </summary>
    /// <example>587</example>
    [DefaultValue(25u)]
    [AllowedPorts(25, 465, 587)]
    public uint? HostPort { get; set; } = 25;

    /// <summary>
    /// The username for authenticating with the mail server.
    /// </summary>
    /// <example>some@mail.com</example>
    [RegularExpression(@"^[0-9A-Za-z@._+-]+$(?!\n)")]
    [MaxLength(511)]
    public string? Username { get; set; }

    /// <summary>
    /// The password for authenticating with the mail server.
    /// </summary>
    /// <example>*****</example>
    [PasswordType(PasswordType.Smtp)]
    public override string? Password { get; set; }

    /// <summary>
    /// The default sender address used in the From header of outgoing emails.
    /// Required when <see cref="DefaultEnvelopeFromPolicy"/> is <c>default_sender</c>.
    /// </summary>
    /// <example>noreply@example.com</example>
    [EmailAddress]
    public string? DefaultSender { get; set; }

    /// <summary>
    /// Controls how the envelope sender (Return-Path) is set for outgoing emails.
    /// <c>auto</c>: the mail server determines the envelope sender automatically.
    /// <c>default_sender</c>: uses <see cref="DefaultSender"/> as the envelope sender.
    /// </summary>
    /// <example>auto</example>
    [Options("auto", "default_sender", AllowNull = true)]
    public string DefaultEnvelopeFromPolicy { get; set; } = "auto";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Set DefaultEnvelopeFromPolicy to default_sender, when DefaultSender is set
        // if (!string.IsNullOrEmpty(DefaultSender) && DefaultEnvelopeFromPolicy is null)
        //     DefaultEnvelopeFromPolicy = "default_sender";

        // Set DefaultEnvelopeFromPolicy to auto, when DefaultSender is NOT set
        // if (string.IsNullOrEmpty(DefaultSender) && DefaultEnvelopeFromPolicy is null)
        //     DefaultEnvelopeFromPolicy = "auto";

        if (!string.IsNullOrEmpty(DefaultSender) && DefaultEnvelopeFromPolicy == "auto")
        {
            yield return new ValidationResult(
                $"Value must not be set when DefaultEnvelopeFromPolicy is auto",
                [nameof(DefaultSender)]);
        }

        if (string.IsNullOrEmpty(DefaultSender) && DefaultEnvelopeFromPolicy == "default_sender")
        {
            yield return new ValidationResult(
                $"Value has to be set, when DefaultEnvelopeFromPolicy is default_sender",
                [nameof(DefaultSender)]);
        }

        if (!string.IsNullOrEmpty(DefaultSender) && string.IsNullOrEmpty(DefaultEnvelopeFromPolicy))
        {
            yield return new ValidationResult(
                $"Value has to be set, when DefaultSender is set",
                [nameof(DefaultEnvelopeFromPolicy)]);
        }
    }
}
