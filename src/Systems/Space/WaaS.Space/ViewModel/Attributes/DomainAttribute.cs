using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WaaS.Space.ViewModel;

public partial class DomainAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(value?.ToString()))
            return ValidationResult.Success;

        if (value is not string domain)
            return ValidationResult.Success;

        if (!BasicFqdnRegex().IsMatch(domain))
            return new ValidationResult($"Value must match /{BasicFqdnRegex()}/");

        return ValidationResult.Success;
    }

    // See https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-source-generators
    [GeneratedRegex(@"^((?!-)[a-zA-Z0-9-]{1,63}(?<!-)\.)+[a-zA-Z]{2,63}\.?$", RegexOptions.IgnoreCase, "en-DE")]
    public static partial Regex BasicFqdnRegex();

    [GeneratedRegex(@"^(http|https):\/\/.*", RegexOptions.IgnoreCase, "en-DE")]
    public static partial Regex UriProtocolRegex();
}
