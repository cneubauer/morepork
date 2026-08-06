using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum RegionType
{
    [EnumMember(Value = "europe")]
    Europe = 1,
    [EnumMember(Value = "america")]
    America = 2,
}
