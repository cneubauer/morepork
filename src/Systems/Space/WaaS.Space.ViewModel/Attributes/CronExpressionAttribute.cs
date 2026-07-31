using Cronos;
using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

public class CronExpressionAttribute : ValidationAttribute
{
    public CronExpressionAttribute()
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string schedule)
            return new ValidationResult($"Schedule must not be null.");

        try
        {
            //CronFormat.Standard : Default crontab pattern without seconds
            CronExpression.Parse(schedule.ToLower(), CronFormat.Standard);
        }
        catch (Exception)
        {
            return new ValidationResult($"The given cron expression '{schedule}' has an invalid format or is not supported.", [nameof(schedule)]);
        }

        return ValidationResult.Success;
    }
}