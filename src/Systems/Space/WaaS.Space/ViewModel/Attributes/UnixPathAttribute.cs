using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WaaS.Space.ViewModel;

public class UnixPathAttribute : ValidationAttribute
{
    private readonly string _match = @"^[^\/\x00-\x1f:]{0,255}(\/+[^\/\x00-\x1f:]{1,255})*\/*$(?!\n)";
    private readonly string _notMatch = @"(^|\/)\.\.(\/|$)";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var path = value as string;

        if (path is null)
            return ValidationResult.Success;

        if (!Regex.IsMatch(path, _match) || Regex.IsMatch(path, _notMatch))
            return new ValidationResult($"Value must match /{_match}/ and not /{_notMatch}/");

        return ValidationResult.Success;
    }
}