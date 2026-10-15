using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace WaaS.Space.Stretch.ViewModel;

public class StretchHttpPathAttribute : ValidationAttribute
{
    // enforces at least one directory level
    private readonly string _match = @"^/*[^\/\x00-\x1f:]{1,255}(/+[^\/\x00-\x1f:]{0,255})*$";

    private readonly List<string> _notMatches = [
        @"(^|\/)\.\.(\/|$)", // rejects ".." as a path component anywhere
        @"^\.?(/+\.?)*$",    // rejects equivalents of "/", e.g. "", ".", "./", "/.", "/.//./" and so on
        @"^/*etc(\/|$)"      // rejects "etc" on the first directory level
    ];

    public StretchHttpPathAttribute()
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string path)
            return ValidationResult.Success;

        var invalid = !Regex.IsMatch(path, _match);

        if (!invalid)
            foreach (var notMatch in _notMatches)
                if (Regex.IsMatch(path, notMatch))
                {
                    invalid = true;
                    break;
                }

        if (invalid)
            return new ValidationResult($"Certain path values are not allowed, e.g. '', '.', '..', './', '/.', '/etc' and so on");

        return ValidationResult.Success;
    }
}