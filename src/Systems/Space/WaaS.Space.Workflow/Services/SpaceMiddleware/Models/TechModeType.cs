using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum TechModeType
{
    [EnumMember(Value = "enabled")]
    Enabled,
    [EnumMember(Value = "disabled")]
    Disabled,
    [EnumMember(Value = "disabled_for_move")]
    DisabledForMove,
}
