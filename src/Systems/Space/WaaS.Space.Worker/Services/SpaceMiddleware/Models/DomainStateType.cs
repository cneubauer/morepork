using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum DomainStateType
{
    [EnumMember(Value = "enabled")]
    Enabled,

    [EnumMember(Value = "locked")]
    Locked,
}
