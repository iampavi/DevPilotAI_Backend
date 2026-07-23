using DevPilotAI.Application.DTOs.Project;
using FluentValidation;

namespace DevPilotAI.Application.Validators.Project;

public class UpdateProjectSettingsDtoValidator : AbstractValidator<UpdateProjectSettingsDto>
{
    public UpdateProjectSettingsDtoValidator()
    {
        RuleFor(x => x.MaxFileSizeInBytes)
            .GreaterThan(0).WithMessage("Maximum file size must be greater than 0 bytes.");

        RuleFor(x => x.ExcludedFolders)
            .NotNull().WithMessage("Excluded folders list cannot be null.");

        RuleFor(x => x.ExcludedExtensions)
            .NotNull().WithMessage("Excluded extensions list cannot be null.");
    }
}
