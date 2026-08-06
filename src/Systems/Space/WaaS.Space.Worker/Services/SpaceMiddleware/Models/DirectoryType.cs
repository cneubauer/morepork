using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum DirectoryType
{
    [EnumMember(Value = "user")]
    User = 1,

    [EnumMember(Value = "managed")]
    Managed = 2
}
