using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>Web Analytics assignment for a mapping. <c>WaProfileId</c> and <c>ExternalReference</c> are mutually exclusive.</summary>
public class WebshieldAnalytics : IValidatableObject
{
    /// <summary>The <c>waProfileId</c> from a valid Web Analytics profile. Mutually exclusive with <c>ExternalReference</c>.</summary>
    public string? WaProfileId { get; set; }

    /// <summary>The external reference from a valid Web Analytics profile. Mutually exclusive with <c>WaProfileId</c>.</summary>
    public string? ExternalReference { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(WaProfileId) && !string.IsNullOrEmpty(ExternalReference))
            yield return new ValidationResult("Only one of WaProfileId and ExternalReference can be set.",
                [nameof(WaProfileId), nameof(ExternalReference)]);
    }
}
