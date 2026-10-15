using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum SpaceStateType
{
    [EnumMember(Value = "init")]
    Init,
    [EnumMember(Value = "enabled")]
    Enabled,
    [EnumMember(Value = "locked")]
    Locked,
    [EnumMember(Value = "deleted")]
    Deleted,
    [EnumMember(Value = "readonly")]
    Readonly,
    [EnumMember(Value = "hardlocked")]
    Hardlocked,
}
