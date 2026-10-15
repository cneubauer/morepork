using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>Web Analytics assignment for a mapping. <c>WaProfileId</c> and <c>ExternalReference</c> are mutually exclusive.</summary>
public class WebshieldAnalytics : IValidatableObject
{
    /// <summary>The <c>waProfileId</c> from a valid Web Analytics profile. Mutually exclusive with <c>ExternalReference</c>.</summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    public string? WaProfileId { get; set; }

    /// <summary>The external reference from a valid Web Analytics profile. Mutually exclusive with <c>WaProfileId</c>.</summary>
    /// <example>7c9e6679-7425-40de-944b-e07fc1f90ae7</example>
    public string? ExternalReference { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(WaProfileId) && !string.IsNullOrEmpty(ExternalReference))
            yield return new ValidationResult("Only one of WaProfileId and ExternalReference can be set.",
                [nameof(WaProfileId), nameof(ExternalReference)]);
    }
}
