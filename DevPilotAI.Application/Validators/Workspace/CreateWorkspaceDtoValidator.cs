using DevPilotAI.Application.DTOs.Workspace;
using FluentValidation;

namespace DevPilotAI.Application.Validators.Workspace;

public class CreateWorkspaceDtoValidator : AbstractValidator<CreateWorkspaceDto>
{
    public CreateWorkspaceDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workspace name is required.")
            .MaximumLength(100).WithMessage("Workspace name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Workspace description must not exceed 500 characters.");
    }
}
