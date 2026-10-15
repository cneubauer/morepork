using System.Runtime.Serialization;

namespace SpaceMiddleware;

public enum ConnectionType
{
    [EnumMember(Value = "docroot")]
    Docroot,

    [EnumMember(Value = "redirect")]
    Redirect
}
