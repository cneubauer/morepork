using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum AccessType
{
    [EnumMember(Value = "sftp")]
    Sftp,

    [EnumMember(Value = "ssh")]
    Ssh
}
