using System.Reflection;

namespace WaaS.Persistence;

/// <summary>
/// When creating an new Desired State instance, it will always produce a valid Desired State for processing.
/// </summary>
/// <typeparam name="T"></typeparam>
public class DesiredState<T> : IDesiredState<T> where T : IDesiredStateData, new()
{
    public static DesiredState<T> Create(
        Tenant tenant,
        IStackInstance stackInstance,
        ulong systemInstanceId,
        string transactionId
    )
    {
        return new DesiredState<T>
        {
            StackInstanceId = stackInstance.Id,
            Tenant = tenant.Id,
            Zone = stackInstance.Zone,
            TransactionId = transactionId,
            SystemInstanceId = systemInstanceId,
        };
    }

    #region Required Properties
    
    public required ulong StackInstanceId { get; init; }
    public required short Tenant { get; init; }
    public required short Zone { get; init; }
    public required string TransactionId { get; init; }
    public required ulong SystemInstanceId { get; init; }

    #endregion


    #region Auto set Properties

    public short Namespace { get; init; } = typeof(T)
        .GetCustomAttribute<DesiredStateDataAttribute>()?
        .Namespace
        ?? throw new InvalidOperationException($"Desired State '{typeof(T).FullName}' is missing the DesiredStateNamespaceAttribute.");
    public ulong Version { get; init; } = 0;
    public T Data { get; init; } = new T();
    public DateTime Created { get; init; } = default;
    public bool Tombstoned { get; set; } = false;

    #endregion


    #region Optional Properties

    public DateTime? Applied { get; set; }
    public DateTime? Expired { get; set; }
    public DateTime? NextCheck => Data.GetNextCheck();

    #endregion

    public void Tombstone()
    {
        Data.Tombstone();
        Tombstoned = true;
    }

    public override string ToString()
    {
        return $"<DesiredState{{Namespace={Namespace};StackId={StackInstanceId};SystemId={SystemInstanceId};Version={Version}}}>";
    }
}
