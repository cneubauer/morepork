using System.ComponentModel.DataAnnotations;

namespace WaaS.Common.ViewModel;

/// <summary>
/// Configuration for temporary resources. One expiration property is allowed only: either ExpireAt or ExpireIn.
/// </summary>
public class TemporaryInfo : IValidatableObject
{
    /// <summary>
    /// Timestamp when the temporary resource will expire.
    /// </summary>
    /// <example>2425-07-03T11:07:00Z</example>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// Time in seconds from UtcNow when the resource will expire. ExpireIn initializes ExpireAt.
    /// </summary>
    /// <example>3600</example>
    public double? ExpireIn { get; set; }

    public DateTime? GetExpireAt()
    {
        if (ExpireAt != null)
        {
            return ExpireAt.Value;
        }

        if (ExpireIn != null)
        {
            return DateTime.UtcNow.AddSeconds(ExpireIn.Value);
        }

        return null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpireAt != null && ExpireIn != null)
        {
            yield return new ValidationResult(
                $"One expiration property is allowed only: either ExpireAt or ExpireIn.",
                [nameof(ExpireAt)]
            );
        }
    }
}
