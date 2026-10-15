using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

public class AllowedPortsAttribute(params uint[] allowedPorts) : ValidationAttribute
{
    private readonly uint[] _allowedPorts = allowedPorts;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return new ValidationResult($"Port must not be null. Choose between one of: {string.Join(", ", _allowedPorts)}");

        var val = Convert.ToUInt32(value);
        var result = _allowedPorts.Contains(val);

        if (!result)
            return new ValidationResult($"Port must be one one of: {string.Join(", ", _allowedPorts)}");

        return ValidationResult.Success;
    }
}