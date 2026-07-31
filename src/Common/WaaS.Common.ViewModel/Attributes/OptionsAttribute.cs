using System.ComponentModel.DataAnnotations;

namespace WaaS.Common.ViewModel;

public class OptionsAttribute : ValidationAttribute
{
    private readonly string[] _validValues;
    public bool AllowNull = false;

    public OptionsAttribute(params string[] values)
    {
        _validValues = values;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null && !AllowNull)
            return new ValidationResult($"Value must be one of: {string.Join(", ", _validValues)}");

        if (value is null && AllowNull)
            return ValidationResult.Success;

        var val = value as string;
        var result = _validValues.Contains(val);

        if (!result)
            return new ValidationResult($"Value must be one of: {string.Join(", ", _validValues)}");

        return ValidationResult.Success;
    }
}

public class ItemOptionsAttribute : ValidationAttribute
{
    private readonly string[] _validValues;

    public ItemOptionsAttribute(params string[] values)
    {
        _validValues = values;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        var val = value as IEnumerable<string> ?? Enumerable.Empty<string>();
        var result = val.Where(x => !_validValues.Contains(x));

        if (result.Any())
            return new ValidationResult($"Values must be: {string.Join(", ", _validValues)}");

        return ValidationResult.Success;
    }
}
