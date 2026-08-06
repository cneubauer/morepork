using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum AccountStateType
{
    [EnumMember(Value = "enabled")]
    Enabled,

    [EnumMember(Value = "locked")]
    Locked,

    [EnumMember(Value = "force_enabled")]
    ForceEnabled,
}
