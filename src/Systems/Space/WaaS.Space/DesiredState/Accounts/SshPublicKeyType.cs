using System.Runtime.Serialization;

namespace WaaS.Space.DesiredState;

public enum SshPublicKeyType
{
    [EnumMember(Value = "ssh-rsa")]
    SSH_RSA = 1,

    [EnumMember(Value = "ssh-ed25519")]
    SSH_ED25519 = 2,

    [EnumMember(Value = "ecdsa-sha2-nistp256")]
    ECDSA_SHA2_NISTP256 = 3,

    [EnumMember(Value = "ecdsa-sha2-nistp384")]
    ECDSA_SHA2_NISTP384 = 4,

    [EnumMember(Value = "ecdsa-sha2-nistp521")]
    ECDSA_SHA2_NISTP521 = 5,
}