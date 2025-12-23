using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using MineCraftManagementService.Models;

namespace MineCraftManagementService;

public class MineCraftServerOptionsValidation : IValidateOptions<MineCraftServerOptions>
{
    public ValidateOptionsResult Validate(string? name, MineCraftServerOptions options)
    {
        List<ValidationResult> validationResults = [];

        if (string.IsNullOrWhiteSpace(options.ServerPath))
        {
            validationResults.Add(new ValidationResult($"{nameof(MineCraftServerOptions.ServerPath)} is required."));
        }
        else if (!Directory.Exists(options.ServerPath))
        {
            validationResults.Add(new ValidationResult($"{nameof(MineCraftServerOptions.ServerPath)} directory does not exist: {options.ServerPath}"));
        }

        if (string.IsNullOrWhiteSpace(options.ServerExecutableName))
        {
            validationResults.Add(new ValidationResult($"{nameof(MineCraftServerOptions.ServerExecutableName)} is required."));
        }

        if (options.MaxMemoryMB < 512)
        {
            validationResults.Add(new ValidationResult($"{nameof(MineCraftServerOptions.MaxMemoryMB)} must be at least 512 MB."));
        }

        if (options.StartTimeoutSeconds < 10)
        {
            validationResults.Add(new ValidationResult($"{nameof(MineCraftServerOptions.StartTimeoutSeconds)} must be at least 10 seconds."));
        }

        if (options.StopTimeoutSeconds < 5)
        {
            validationResults.Add(new ValidationResult($"{nameof(MineCraftServerOptions.StopTimeoutSeconds)} must be at least 5 seconds."));
        }

        if (validationResults.Count > 0)
        {
            var failures = validationResults.Where(v => v.ErrorMessage is not null).Select(v => v.ErrorMessage!);
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}
