using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WaaS.Common.ViewModel;

public interface ICredential
{
    string? Password { get; }
    string? PasswordToken { get; }
    void Reset();
}

public class Credential : ICredential
{
    /// <summary>
    /// The plaintext password. Write-only — never returned in responses.
    /// </summary>
    /// <example>*****</example>
    [MinLength(1)]
    public virtual string? Password { get; set; }

    /// <summary>
    /// A pre-hashed password token managed by the password store.
    /// </summary>
    /// <example>03axxx755ddfab6b8b0dc5e005926a99</example>
    [JsonPropertyName("securePasswordToken")]
    public string? PasswordToken { get; set; }

    public void Reset()
    {
        Password = null;
    }
}