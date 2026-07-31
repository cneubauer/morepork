namespace WaaS.Common.ViewModel;

public struct ProcessingError
{
    // see also ErrorCodeType enum
    /// <summary>
    /// The numeric error code identifying the type of processing error.
    /// </summary>
    /// <example>409</example>
    public int? Code { get; set; }

    /// <summary>
    /// A human-readable description of the error.
    /// </summary>
    /// <example>Domain already bound to another Stack Instance.</example>
    public string Message { get; set; }
}