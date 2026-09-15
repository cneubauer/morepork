namespace WaaS.Common.DesiredState;

public interface ICredentialContainer
{
    IEnumerable<ICredential> GetCredentials();
}

/// <summary>
/// An interface for storing credentials. We do never store passwords directly, but use the Password Store instead.
/// </summary>
public interface ICredential
{
    /// <summary>
    /// The password token used with Password Store.
    /// </summary>
    string? SecurePasswordToken { get; set; }
}