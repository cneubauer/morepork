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

    [MinLength(1)]
    public virtual string? Password { get; set; }

    /// <summary>
    /// A pre-hashed password token managed by the password store.
    /// </summary>
    /// <example>a3f1b2c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b</example>
    [JsonPropertyName("securePasswordToken")]
    public string? PasswordToken { get; set; }

    public void Reset()
    {
        Password = null;
    }
}