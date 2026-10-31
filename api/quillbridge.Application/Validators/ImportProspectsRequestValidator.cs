using FluentValidation;
using quillbridge.Application.DTOs.Prospects;

namespace quillbridge.Application.Validators;

public class ImportProspectsRequestValidator : AbstractValidator<ImportProspectsRequest>
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    public ImportProspectsRequestValidator()
    {
        RuleFor(r => r.File)
            .NotNull().WithMessage("Please upload a CSV file.")
            .DependentRules(() =>
            {
                RuleFor(r => r.File!.Length)
                    .GreaterThan(0).WithMessage("The uploaded file is empty.")
                    .LessThanOrEqualTo(MaxFileSize).WithMessage("The file must not be larger than 10 MB.");

                RuleFor(r => r.File!.FileName)
                    .Must(name => Path.GetExtension(name).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("Only .csv files can be imported.");
            });
    }
}
