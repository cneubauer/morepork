// TODO: Move to WaaS.SharedWebspaceManager.WebApi

// using System.ComponentModel.DataAnnotations.Schema;
// using WaaS.DesiredState.Repository;
// 
// namespace WaaS.Space.Classic.DesiredState;

// [DesiredStateData(Namespace = 3)]
// public class SharedWebspaceData : IDesiredStateData, ISpaceData<SharedWebspace>
// {
//     public required SharedWebspace Webspace { get; set; }
    
//     /// <summary>
//     /// This is the interface implementation for <see cref="ISpaceData{T}"/>
//     /// </summary>
//     [NotMapped]
//     public SharedWebspace Space => Webspace;

//     /// <summary>
//     /// This is the interface implementation for <see cref="IDesiredStateData"/>
//     /// </summary>
//     public DateTime? GetNextCheck() => Webspace.CalculateNextCheckTimestamp();
// }