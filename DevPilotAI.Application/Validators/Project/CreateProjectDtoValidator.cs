using DevPilotAI.Application.DTOs.Project;
using FluentValidation;

namespace DevPilotAI.Application.Validators.Project;

public class CreateProjectDtoValidator : AbstractValidator<CreateProjectDto>
{
    public CreateProjectDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(100).WithMessage("Project name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Project description must not exceed 500 characters.");

        RuleFor(x => x.SourceLocation)
            .MaximumLength(2000).WithMessage("Source location must not exceed 2000 characters.");

        RuleFor(x => x.ProjectType)
            .IsInEnum().WithMessage("A valid project type is required.");
    }
}
