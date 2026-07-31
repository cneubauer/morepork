using System.ComponentModel.DataAnnotations;

namespace MyNamespace;

public class DesiredState
{
    public required ulong StackInstanceId {get; set; }
    public required ulong SystemInstanceId {get; set; }
    public required Webspace Webspace {get; set; }
}
