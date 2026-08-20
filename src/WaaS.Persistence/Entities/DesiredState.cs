using System.Reflection;

namespace WaaS.Persistence;

/// <summary>
/// When creating an new Desired State instance, it will always produce a valid Desired State for processing.
/// </summary>
/// <typeparam name="T"></typeparam>
public class DesiredState<T> : IDesiredState<T> where T : IDesiredStateData, new()
{
    #region Required Properties
    
    public required ulong StackInstanceId { get; init; }
    public required short Tenant { get; init; }
    public required short Zone { get; init; }
    public required string TransactionId { get; init; }

    #endregion


    #region Auto set Properties

    public short Namespace { get; init; } = typeof(T)
        .GetCustomAttribute<DesiredStateDataAttribute>()?
        .Namespace
        ?? throw new InvalidOperationException($"Desired State '{typeof(T).FullName}' is missing the DesiredStateNamespaceAttribute.");
    public ulong Version { get; internal set; } = 0;
    public T Data { get; init; } = new T();
    public DateTime Created { get; init; } = DateTime.UtcNow;
    public bool Tombstoned { get; set; } = false;

    #endregion


    #region Optional Properties

    public ulong? SystemInstanceId { get; set; }
    public DateTime? Applied { get; set; }
    public DateTime? Expired { get; set; }
    public DateTime? NextCheck => Data.GetNextCheck();

    #endregion

    void IDesiredState.Tombstone()
    {
        Tombstoned = true;
    }

    public override string ToString()
    {
        return $"<DesiredState{{Namespace={Namespace};StackId={StackInstanceId};SystemId={SystemInstanceId};Version={Version}}}>";
    }
}