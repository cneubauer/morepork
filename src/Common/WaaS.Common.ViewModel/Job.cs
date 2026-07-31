using System.ComponentModel;

namespace WaaS.Common.ViewModel;

public struct Job
{
    /// <summary>
    /// The unique identifier of the job.
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    [ReadOnly(true)]
    public Guid Id { get; set; }

    /// <summary>
    /// An external reference ID, which can be passed by the client to identify the job.
    /// </summary>
    [ReadOnly(true)]
    public string? ReferenceId { get; set; }

    /// <summary>
    /// The correlation ID used for log tracing across related operations.
    /// </summary>
    /// <example>7c9e6679-7425-40de-944b-e07fc1f90ae7</example>
    [ReadOnly(true)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The time the job was last executed.
    /// </summary>
    /// <example>2024-06-15T10:30:00Z</example>
    [ReadOnly(true)]
    public DateTime? LastExecution { get; set; }

    /// <summary>
    /// The time, when the job will be executed
    /// </summary>
    /// <example>2024-06-15T10:35:00Z</example>
    [ReadOnly(true)]
    public DateTime? NextExecution { get; set; }

    /// <summary>
    /// The time, when the job has been finished
    /// </summary>
    /// <example>2024-06-15T10:30:05Z</example>
    [ReadOnly(true)]
    public DateTime? Finished { get; set; }

    /// <summary>
    /// The time, when the job has been completed with all its dependencies
    /// </summary>
    /// <example>2024-06-15T10:30:10Z</example>
    [ReadOnly(true)]
    public DateTime? Completed { get; set; }

    /// <summary>
    /// The times the job has been executed
    /// </summary>
    /// <example>3</example>
    [ReadOnly(true)]
    public int? ExecutionCount { get; set; }

    /// <summary>
    /// The ID of the parent job
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    [ReadOnly(true)]
    public Guid? ParentId { get; set; }

    /// <summary>
    /// The name of the action, which will be executed.
    /// </summary>
    /// <example>PublishAction</example>
    [ReadOnly(true)]
    public string? ActionName { get; set; }

    /// <summary>
    /// The last occured error
    /// </summary>
    [ReadOnly(true)]
    public ProcessingError? Error { get; set; }

    /// <summary>
    /// The current status of the job
    /// </summary>
    /// <example>Completed</example>
    [ReadOnly(true)]
    public string? Status { get; set; }

    /// <summary>
    /// The time the job was created
    /// </summary>
    /// <example>2024-06-15T10:29:00Z</example>
    [ReadOnly(true)]
    public DateTime? Created { get; set; }

    /// <summary>
    /// Field to save additional information about the job
    /// </summary>
    /// <example>Space successfully provisioned.</example>
    [ReadOnly(true)]
    public string? Message { get; set; }
}