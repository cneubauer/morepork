using System.Runtime.Serialization;

namespace WaaS.Space.DesiredState;

public enum PublicKeyOptionType
{
    [EnumMember(Value = "command")]
    COMMAND = 1,

    [EnumMember(Value = "principals")]
    PRINCIPALS = 2,

    [EnumMember(Value = "cert-authority")]
    CERT_AUTHORITY = 3,

    [EnumMember(Value = "no-user-rc")]
    NO_USER_RC = 4,
}