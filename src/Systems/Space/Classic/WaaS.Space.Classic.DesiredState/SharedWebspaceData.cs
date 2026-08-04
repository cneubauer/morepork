using System.ComponentModel.DataAnnotations.Schema;
using WaaS.Persistence;
using WaaS.Space.DesiredState;

namespace WaaS.Space.Classic.DesiredState;

[DesiredStateData(Namespace = 3)]
public class SharedWebspaceData : IDesiredStateData, ISpaceData<SharedWebspace>
{
    public SharedWebspace Webspace { get; set; } = new SharedWebspace();
    
    /// <summary>
    /// This is the interface implementation for <see cref="ISpaceData{T}"/>
    /// </summary>
    [NotMapped]
    public SharedWebspace Space => Webspace;

    /// <summary>
    /// This is the interface implementation for <see cref="IDesiredStateData"/>
    /// </summary>
    public DateTime? GetNextCheck() => Webspace.CalculateNextCheckTimestamp();
}