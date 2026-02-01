using BingoSim.Application.DTOs;
using FluentValidation;

namespace BingoSim.Application.Validators;

public class CreateActivityDefinitionRequestValidator : AbstractValidator<CreateActivityDefinitionRequest>
{
    public CreateActivityDefinitionRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("Activity key is required.")
            .MaximumLength(100)
            .WithMessage("Activity key cannot exceed 100 characters.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Activity name is required.")
            .MaximumLength(200)
            .WithMessage("Activity name cannot exceed 200 characters.");

        RuleFor(x => x.ModeSupport)
            .NotNull()
            .WithMessage("Mode support is required.")
            .SetValidator(new ActivityModeSupportDtoValidator());

        RuleFor(x => x.Attempts)
            .NotNull()
            .WithMessage("Attempts list cannot be null.")
            .NotEmpty()
            .WithMessage("At least one attempt definition is required.");

        RuleForEach(x => x.Attempts)
            .SetValidator(new ActivityAttemptDefinitionDtoValidator());

        RuleFor(x => x.GroupScalingBands)
            .NotNull()
            .WithMessage("Group scaling bands list cannot be null.");

        RuleForEach(x => x.GroupScalingBands)
            .SetValidator(new GroupSizeBandDtoValidator());
    }
}
